using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkItDown.Core;

/// <summary>
/// Servicio de limpieza post-extracción "ligera y conservadora" para Markdown generado
/// a partir de PDFs, pensado para reducir tokens antes de enviarlo a un LLM.
///
/// Principio rector: INTERVENCIÓN MÍNIMA. Solo estandariza estructura visual y elimina
/// ruido inequívoco; ante la duda, deja el texto intacto. Nunca une párrafos ni reescribe
/// contenido, y CONSERVA los números de página (los normaliza a un separador con trazabilidad).
/// </summary>
/// <remarks>
/// El servicio es inmutable y seguro para reutilizarse entre hilos.
/// Ejemplo:
/// <code>
/// var cleaner = new DocumentToMarkdownCleaner();
/// string limpio = cleaner.Clean(markdownCrudo);
/// </code>
/// </remarks>
public sealed partial class DocumentToMarkdownCleaner
{
    /// <summary>
    /// Plantilla por defecto del marcador de página. Usa <c>{0}</c> para el número.
    /// Va rodeada de líneas en blanco a propósito: sin ellas, un <c>---</c> pegado a una
    /// línea de texto se interpretaría como subrayado setext (encabezado) en lugar de
    /// como regla horizontal. Para un LLM que recibe Markdown crudo el efecto es inocuo,
    /// pero así el separador es correcto también si el documento se renderiza.
    /// </summary>
    public const string DefaultPageMarkerTemplate = "\n---\n\n**[Página {0}]**\n\n---\n";

    /// <summary>
    /// Plantilla usada cuando el indicador de página es lo PRIMERO del documento.
    /// Omite la regla horizontal superior a propósito: un <c>---</c> en la primerísima
    /// línea sería interpretado como delimitador de front matter YAML (Jekyll/Hugo/Pandoc)
    /// y ocultaría el número de página al renderizar.
    /// </summary>
    public const string DefaultLeadingPageMarkerTemplate = "**[Página {0}]**\n\n---\n";

    private readonly CleanerOptions _options;
    private readonly Regex? _additionalKeywordRegex;

    /// <summary>Crea el limpiador con las opciones por defecto (todos los pasos activos).</summary>
    public DocumentToMarkdownCleaner()
        : this(new CleanerOptions())
    {
    }

    /// <summary>Crea el limpiador con opciones personalizadas.</summary>
    public DocumentToMarkdownCleaner(CleanerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Las palabras clave adicionales son dinámicas (dependen de la configuración),
        // por lo que no pueden usar [GeneratedRegex]; se compilan una sola vez aquí.
        var keywords = _options.AdditionalNoiseKeywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => Regex.Escape(k.Trim()))
            .ToArray();

        if (keywords.Length > 0)
        {
            _additionalKeywordRegex = new Regex(
                @"\b(?:" + string.Join("|", keywords) + @")\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }
    }

    /// <summary>
    /// Limpia el Markdown aplicando, en orden: normalización de fin de línea,
    /// normalización de paginación, eliminación de artefactos de OCR y compactación
    /// de líneas en blanco. Devuelve cadena vacía si la entrada es nula o vacía.
    /// </summary>
    public string Clean(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var text = NormalizeLineEndings(markdown!);

        if (_options.NormalizePagination)
        {
            text = NormalizePagination(text);
        }

        if (_options.RemoveOcrArtifacts)
        {
            text = RemoveOcrArtifacts(text);
        }

        if (_options.CompactBlankLines)
        {
            text = CompactBlankLines(text);
        }

        // Recorta solo los saltos de línea de los extremos (no toca la sangría de la
        // primera línea, que podría ser un bloque de código indentado). Seguro y ahorra tokens.
        return text.Trim('\n');
    }

    // ---------------------------------------------------------------------
    // 0. Normalización de fin de línea (requisito para que las reglas basadas
    //    en \n sean predecibles; no altera contenido visible).
    // ---------------------------------------------------------------------

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    // ---------------------------------------------------------------------
    // 1. Normalización de paginación (trazabilidad).
    //    Solo se convierte una línea si, tras quitar ruido reconocido (fechas de
    //    impresión, palabras como "Carátula"), lo ÚNICO que queda es el marcador de
    //    página. Así jamás se toca una referencia en medio de una frase
    //    (p. ej. "...vea la Página 5 del manual...").
    // ---------------------------------------------------------------------

    private string NormalizePagination(string text)
    {
        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length + 32);

        // Un marcador que aparece antes de cualquier contenido real se emite sin la regla
        // horizontal superior, para que el documento no COMIENCE con "---".
        var hasContent = false;

        for (var i = 0; i < lines.Length; i++)
        {
            if (TryConvertPaginationLine(lines[i], atStart: !hasContent, out var marker))
            {
                builder.Append(marker);
                hasContent = true;
            }
            else
            {
                builder.Append(lines[i]);
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    hasContent = true;
                }
            }

            if (i < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private bool TryConvertPaginationLine(string line, bool atStart, out string marker)
    {
        marker = string.Empty;

        // Rechazo barato: una línea de paginación necesita al menos un dígito.
        if (!ContainsDigit(line))
        {
            return false;
        }

        // Quitamos ruido conocido y comprobamos si el residuo es EXACTAMENTE un
        // marcador de página. Si queda cualquier otro texto, la línea se deja intacta.
        var candidate = StripAdjacentNoise(line);
        var match = PaginationLineRegex().Match(candidate);
        if (!match.Success)
        {
            return false;
        }

        var template = atStart ? _options.LeadingPageMarkerTemplate : _options.PageMarkerTemplate;
        marker = string.Format(CultureInfo.InvariantCulture, template, match.Groups[1].Value);
        return true;
    }

    private string StripAdjacentNoise(string line)
    {
        // Se reemplaza por espacio (no por vacío) para no pegar tokens; el regex de
        // paginación admite espacios sobrantes en los extremos.
        var stripped = PrintDateNoiseRegex().Replace(line, " ");
        stripped = CaratulaNoiseRegex().Replace(stripped, " ");

        if (_additionalKeywordRegex is not null)
        {
            stripped = _additionalKeywordRegex.Replace(stripped, " ");
        }

        return stripped;
    }

    private static bool ContainsDigit(string value)
    {
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9')
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // 2. Eliminación de artefactos de OCR (muy segura).
    // ---------------------------------------------------------------------

    private static string RemoveOcrArtifacts(string text)
    {
        text = EmptyHtmlCommentRegex().Replace(text, string.Empty);
        text = LongUnderscoreRunRegex().Replace(text, string.Empty);
        return text;
    }

    // ---------------------------------------------------------------------
    // 3. Compactación de líneas en blanco.
    //    Las líneas con solo espacios se tratan como vacías para que la regla de
    //    "3+ saltos -> 2 saltos" pueda unirlas; nunca se unen párrafos con contenido.
    // ---------------------------------------------------------------------

    private static string CompactBlankLines(string text)
    {
        text = WhitespaceOnlyLineRegex().Replace(text, string.Empty);
        text = ExcessiveBlankLinesRegex().Replace(text, "\n\n");
        return text;
    }

    // ---------------------------------------------------------------------
    // Expresiones regulares compiladas con el generador de .NET 8.
    // ---------------------------------------------------------------------

    /// <summary>
    /// Marca una línea que es EXACTAMENTE un indicador de página (tras quitar ruido):
    /// "Página 5", "Pagina 5", "Pág. 5", "Pag 5", "P. 5", "Página 5 de 20", "Pág 5/20".
    /// Grupo 1 = número de la página actual. Anclada a línea completa para no tocar
    /// referencias dentro de un párrafo.
    ///
    /// El prefijo exige la raíz "pag"/"pág" (con o sin "ina") o bien "p" seguida de "."/":".
    /// Así una "p" suelta ("P 5", un código de plan/producto) NO se confunde con paginación
    /// y su contenido se conserva.
    /// </summary>
    [GeneratedRegex(
        @"^[ \t]*p(?:[áa]g(?:ina)?[.:]?|[.:])[ \t]*(\d{1,6})(?:[ \t]*(?:de|[/\-])[ \t]*\d{1,6})?[ \t]*\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PaginationLineRegex();

    /// <summary>
    /// Fecha (con hora opcional) que el OCR repite junto a la paginación, con etiqueta
    /// opcional "Impreso el" / "Fecha de impresión". Solo se elimina cuando queda pegada
    /// a un marcador de página; una fecha en su propia línea de contenido se conserva.
    /// </summary>
    [GeneratedRegex(
        @"(?:impreso(?:[ \t]+el)?|fecha[ \t]+de[ \t]+impresi[oó]n)?[ \t]*:?[ \t]*\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4}(?:[ \t]+\d{1,2}:\d{2}(?::\d{2})?(?:[ \t]*[ap]\.?[ \t]?m\.?)?)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PrintDateNoiseRegex();

    /// <summary>Palabra "Carátula" (con o sin tilde) que el OCR repite en cada salto de página.</summary>
    [GeneratedRegex(@"\bcar[áa]tula\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CaratulaNoiseRegex();

    /// <summary>Comentario HTML vacío: <c>&lt;!-- --&gt;</c> (solo espacios/saltos dentro).</summary>
    [GeneratedRegex(@"<!--\s*-->", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyHtmlCommentRegex();

    /// <summary>
    /// Secuencia de guiones bajos de más de 5 caracteres (es decir, 6 o más).
    /// Si prefiere incluir también las de exactamente 5, cambie a <c>_{5,}</c>.
    /// </summary>
    [GeneratedRegex(@"_{6,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongUnderscoreRunRegex();

    /// <summary>Línea compuesta únicamente por espacios o tabulaciones.</summary>
    [GeneratedRegex(@"^[ \t]+$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceOnlyLineRegex();

    /// <summary>Tres o más saltos de línea consecutivos.</summary>
    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex ExcessiveBlankLinesRegex();

    /// <summary>Opciones de configuración del limpiador. Valores por defecto conservadores.</summary>
    public sealed class CleanerOptions
    {
        /// <summary>Normalizar los indicadores de página a un separador con trazabilidad.</summary>
        public bool NormalizePagination { get; init; } = true;

        /// <summary>Eliminar comentarios HTML vacíos y secuencias largas de guiones bajos.</summary>
        public bool RemoveOcrArtifacts { get; init; } = true;

        /// <summary>Compactar 3+ saltos de línea (y líneas de solo espacios) en un doble salto.</summary>
        public bool CompactBlankLines { get; init; } = true;

        /// <summary>
        /// Palabras extra que el OCR repite junto a la paginación y que se pueden eliminar
        /// de forma segura (además de "Carátula"). Solo se quitan cuando la línea, sin ellas,
        /// queda reducida a un marcador de página.
        /// </summary>
        public IReadOnlyList<string> AdditionalNoiseKeywords { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Plantilla del marcador de página. Debe contener <c>{0}</c> para el número.
        /// Por defecto <see cref="DefaultPageMarkerTemplate"/>.
        /// </summary>
        public string PageMarkerTemplate { get; init; } = DefaultPageMarkerTemplate;

        /// <summary>
        /// Plantilla usada solo cuando el marcador es lo primero del documento (sin regla
        /// horizontal superior). Debe contener <c>{0}</c>. Por defecto
        /// <see cref="DefaultLeadingPageMarkerTemplate"/>.
        /// </summary>
        public string LeadingPageMarkerTemplate { get; init; } = DefaultLeadingPageMarkerTemplate;
    }
}
