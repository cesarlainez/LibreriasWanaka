using MarkItDown.Converters.Docx;
using MarkItDown.Converters.Pdf;
using MarkItDown.Core;

namespace MarkItDown;

/// <summary>
/// Punto de entrada de la librería: recibe un archivo (ruta o stream) y devuelve su
/// contenido convertido a Markdown, eligiendo el convertidor según la extensión.
/// </summary>
/// <example>
/// <code>
/// var converter = new MarkItDownConverter();
/// var resultado = converter.Convert(@"C:\docs\informe.docx");
/// File.WriteAllText("informe.md", resultado.Markdown);
/// </code>
/// </example>
public sealed class MarkItDownConverter
{
    // Copy-on-write: los lectores toman un snapshot del array (siempre consistente)
    // y RegisterConverter publica un array nuevo, por lo que la instancia puede usarse
    // como singleton entre hilos sin bloqueos en las conversiones.
    private volatile IDocumentConverter[] _converters;
    private readonly object _writeLock = new();

    /// <summary>Crea el convertidor con los formatos integrados: Word (.docx) y PDF (.pdf).</summary>
    public MarkItDownConverter()
        : this(new IDocumentConverter[] { new DocxToMarkdownConverter(), new PdfToMarkdownConverter() })
    {
    }

    /// <summary>Crea el convertidor con un conjunto personalizado de convertidores (útil con inyección de dependencias).</summary>
    public MarkItDownConverter(IEnumerable<IDocumentConverter> converters)
    {
        _converters = converters?.ToArray() ?? throw new ArgumentNullException(nameof(converters));
    }

    /// <summary>Extensiones soportadas por los convertidores registrados.</summary>
    public IReadOnlyCollection<string> SupportedExtensions =>
        _converters.SelectMany(c => c.SupportedExtensions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Registra un convertidor adicional. Tiene prioridad sobre los existentes,
    /// lo que permite reemplazar el manejo de una extensión.
    /// </summary>
    public void RegisterConverter(IDocumentConverter converter)
    {
        if (converter is null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        lock (_writeLock)
        {
            var current = _converters;
            var updated = new IDocumentConverter[current.Length + 1];
            updated[0] = converter;
            Array.Copy(current, 0, updated, 1, current.Length);
            _converters = updated;
        }
    }

    /// <summary>Convierte el archivo indicado a Markdown.</summary>
    /// <exception cref="FileNotFoundException">Si el archivo no existe.</exception>
    /// <exception cref="UnsupportedFileFormatException">Si la extensión no está soportada.</exception>
    /// <exception cref="MarkdownConversionException">Si el archivo está dañado o no puede leerse.</exception>
    public ConversionResult Convert(string filePath, ConversionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Debe indicar la ruta del archivo.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"No se encontró el archivo: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return Convert(stream, Path.GetExtension(filePath), options);
    }

    /// <summary>Convierte el contenido de un stream a Markdown.</summary>
    /// <param name="stream">Stream posicionado al inicio del documento. No se cierra.</param>
    /// <param name="fileNameOrExtension">Nombre del archivo o su extensión (ej. "informe.pdf", ".pdf" o "pdf").</param>
    /// <param name="options">Opciones de conversión (opcional).</param>
    public ConversionResult Convert(Stream stream, string fileNameOrExtension, ConversionOptions? options = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var extension = NormalizeExtension(fileNameOrExtension);
        var snapshot = _converters;
        var converter = snapshot.FirstOrDefault(c => c.CanConvert(extension));
        if (converter is null)
        {
            throw new UnsupportedFileFormatException(extension, BuildUnsupportedMessage(extension));
        }

        return converter.Convert(stream, options);
    }

    /// <summary>Convierte el archivo a Markdown como texto (atajo de <see cref="Convert(string, ConversionOptions?)"/>).</summary>
    public string ConvertToMarkdown(string filePath, ConversionOptions? options = null)
    {
        return Convert(filePath, options).Markdown;
    }

    /// <summary>Convierte un archivo y guarda el Markdown en la ruta de salida indicada (UTF-8).</summary>
    /// <returns>El resultado de la conversión (incluye advertencias).</returns>
    public ConversionResult ConvertToFile(string inputPath, string outputPath, ConversionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Debe indicar la ruta de salida.", nameof(outputPath));
        }

        var result = Convert(inputPath, options);
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, result.Markdown);
        return result;
    }

    private string BuildUnsupportedMessage(string extension)
    {
        if (extension == ".doc")
        {
            return "Los archivos .doc (Word 97-2003, formato binario) no están soportados. " +
                   "Abra el documento en Word y guárdelo como .docx.";
        }

        var supported = string.Join(", ", SupportedExtensions.OrderBy(e => e, StringComparer.Ordinal));
        return $"La extensión \"{extension}\" no está soportada. Formatos disponibles: {supported}.";
    }

    private static string NormalizeExtension(string fileNameOrExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrExtension))
        {
            return string.Empty;
        }

        var value = fileNameOrExtension.Trim();
        var fromPath = Path.GetExtension(value);
        if (!string.IsNullOrEmpty(fromPath))
        {
            return fromPath.ToLowerInvariant();
        }

        value = value.ToLowerInvariant();
        return value.StartsWith(".") ? value : "." + value;
    }
}
