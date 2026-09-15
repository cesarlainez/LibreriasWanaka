using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MarkItDown.Converters.Common;
using MarkItDown.Core;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace MarkItDown.Converters.Docx;

/// <summary>
/// Convierte documentos Word (.docx) a Markdown usando DocumentFormat.OpenXml.
/// Soporta encabezados (por nivel de esquema o estilo), negrita, cursiva, tachado,
/// hipervínculos, listas con viñetas y numeradas (incluyendo anidación), tablas e imágenes.
/// </summary>
public sealed class DocxToMarkdownConverter : IDocumentConverter
{
    private static readonly Regex HeadingStyleIdRegex =
        new(@"^(?:heading|t[ií]?tulo|ttulo|titre|berschrift|überschrift)\s*([1-9])$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".docx" };

    public bool CanConvert(string extension) => Normalize(extension) == ".docx";

    public ConversionResult Convert(Stream input, ConversionOptions? options = null)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        options ??= new ConversionOptions();

        // OpenXml necesita un stream con búsqueda; copiamos para no depender del stream del llamador.
        using var buffer = new MemoryStream();
        try
        {
            input.CopyTo(buffer);
            buffer.Position = 0;
            using var document = WordprocessingDocument.Open(buffer, isEditable: false);
            return ConvertDocument(document, options);
        }
        catch (MarkdownConversionException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // La superficie de excepciones de OpenXml/zip con archivos dañados es abierta;
            // el contrato de la librería es lanzar siempre MarkdownConversionException.
            throw new MarkdownConversionException(
                "No se pudo leer el archivo .docx. Verifique que no esté dañado ni protegido con contraseña.", ex);
        }
    }

    private static ConversionResult ConvertDocument(WordprocessingDocument document, ConversionOptions options)
    {
        var mainPart = document.MainDocumentPart;
        var body = mainPart?.Document?.Body;
        if (mainPart is null || body is null)
        {
            throw new MarkdownConversionException("El archivo .docx no contiene un cuerpo de documento válido.");
        }

        var ctx = new DocxContext(mainPart, options);
        var builder = new MarkdownDocumentBuilder();

        foreach (var element in body.ChildElements)
        {
            ProcessBlockElement(element, builder, ctx);
        }

        // Definiciones de notas al pie / al final referenciadas en el cuerpo.
        foreach (var (marker, text) in ctx.NoteDefinitions)
        {
            builder.AddBlock($"{marker}: {text}");
        }

        var markdown = builder.Build();
        var title = document.PackageProperties?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = ctx.FirstHeadingText;
        }

        return new ConversionResult(markdown, title, ctx.Warnings);
    }

    private static void ProcessBlockElement(OpenXmlElement element, MarkdownDocumentBuilder builder, DocxContext ctx)
    {
        switch (element)
        {
            case Paragraph paragraph:
                ProcessParagraph(paragraph, builder, ctx);
                break;
            case Table table:
                builder.AddBlock(RenderTable(table, ctx));
                break;
            case SdtBlock sdt:
                // Controles de contenido: procesamos lo que contienen.
                var content = sdt.SdtContentBlock;
                if (content is not null)
                {
                    foreach (var child in content.ChildElements)
                    {
                        ProcessBlockElement(child, builder, ctx);
                    }
                }

                break;
            case CustomXmlBlock customXml:
                // Bloques w:customXml (frecuentes en documentos convertidos desde .doc).
                foreach (var child in customXml.ChildElements)
                {
                    ProcessBlockElement(child, builder, ctx);
                }

                break;
        }
    }

    private static void ProcessParagraph(Paragraph paragraph, MarkdownDocumentBuilder builder, DocxContext ctx)
    {
        var inline = RenderInlines(paragraph, ctx);
        if (string.IsNullOrWhiteSpace(inline))
        {
            return;
        }

        var headingLevel = GetHeadingLevel(paragraph, ctx);
        if (headingLevel > 0)
        {
            var text = MarkdownText.CollapseWhitespace(inline);
            ctx.FirstHeadingText ??= MarkdownText.CollapseWhitespace(GetPlainText(paragraph));
            builder.AddBlock(new string('#', Math.Min(headingLevel, 6)) + " " + text);
            return;
        }

        if (TryGetListInfo(paragraph, ctx, out var ordered, out var level, out var numId))
        {
            // Un elemento anidado solo puede ir un nivel más adentro que el anterior;
            // sin este ajuste, "    - x" tras un párrafo normal sería un bloque de código.
            if (!builder.LastIsListItem)
            {
                ctx.LastListLevel = -1;
            }

            level = Math.Min(level, ctx.LastListLevel + 1);
            ctx.LastListLevel = level;

            var indent = new string(' ', 4 * level);
            var marker = ordered ? "1. " : "- ";
            builder.AddListItem(indent + marker + MarkdownText.CollapseWhitespace(inline), "num:" + numId);
            return;
        }

        builder.AddBlock(MarkdownText.EscapeLineStart(inline.Trim()));
    }

    // ---------------------------------------------------------------------
    // Encabezados
    // ---------------------------------------------------------------------

    private static int GetHeadingLevel(Paragraph paragraph, DocxContext ctx)
    {
        var direct = paragraph.ParagraphProperties?.OutlineLevel?.Val;
        if (direct is not null && direct.Value >= 0 && direct.Value <= 8)
        {
            return direct.Value + 1;
        }

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrEmpty(styleId))
        {
            return 0;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentId = styleId;
        while (currentId is not null && visited.Add(currentId))
        {
            var level = MatchHeadingName(currentId);
            if (level > 0)
            {
                return level;
            }

            var style = ctx.FindStyle(currentId);
            if (style is null)
            {
                break;
            }

            var outline = style.StyleParagraphProperties?.OutlineLevel?.Val;
            if (outline is not null && outline.Value >= 0 && outline.Value <= 8)
            {
                return outline.Value + 1;
            }

            var name = style.StyleName?.Val?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                level = MatchHeadingName(name!);
                if (level > 0)
                {
                    return level;
                }
            }

            currentId = style.BasedOn?.Val?.Value;
        }

        return 0;
    }

    private static int MatchHeadingName(string idOrName)
    {
        if (idOrName.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (idOrName.Equals("Subtitle", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        var match = HeadingStyleIdRegex.Match(idOrName.Trim());
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    // ---------------------------------------------------------------------
    // Listas
    // ---------------------------------------------------------------------

    private static bool TryGetListInfo(Paragraph paragraph, DocxContext ctx, out bool ordered, out int level, out int numId)
    {
        ordered = false;
        level = 0;
        numId = 0;

        var directNumPr = paragraph.ParagraphProperties?.NumberingProperties;
        var resolvedNumId = directNumPr?.NumberingId?.Val?.Value;
        int? ilvl = directNumPr?.NumberingLevelReference?.Val?.Value;

        // numId=0 explícito anula la numeración heredada del estilo.
        if (resolvedNumId is 0)
        {
            return false;
        }

        // Estilos integrados como "List Bullet"/"Lista con viñetas" llevan el numPr
        // en la definición del estilo, no en el párrafo.
        if (resolvedNumId is null)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            while (currentId is not null && visited.Add(currentId))
            {
                var style = ctx.FindStyle(currentId);
                if (style is null)
                {
                    break;
                }

                var styleNumPr = style.StyleParagraphProperties?.NumberingProperties;
                if (styleNumPr?.NumberingId?.Val?.Value is int styleNumId)
                {
                    if (styleNumId == 0)
                    {
                        return false;
                    }

                    resolvedNumId = styleNumId;
                    ilvl ??= styleNumPr.NumberingLevelReference?.Val?.Value;
                    break;
                }

                currentId = style.BasedOn?.Val?.Value;
            }
        }

        if (resolvedNumId is null)
        {
            return false;
        }

        numId = resolvedNumId.Value;
        level = Math.Max(ilvl ?? 0, 0);
        ordered = IsOrderedList(numId, level, ctx);
        return true;
    }

    private static bool IsOrderedList(int numId, int level, DocxContext ctx)
    {
        var numbering = ctx.Main.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
        {
            return false;
        }

        var instance = numbering.Elements<NumberingInstance>()
            .FirstOrDefault(n => n.NumberID?.Value == numId);
        if (instance is null)
        {
            return false;
        }

        var overrideLevel = instance.Elements<LevelOverride>()
            .FirstOrDefault(o => o.LevelIndex?.Value == level)?.Level;

        var levelDefinition = overrideLevel;
        if (levelDefinition is null)
        {
            var abstractId = instance.AbstractNumId?.Val?.Value;
            var abstractNum = numbering.Elements<AbstractNum>()
                .FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
            levelDefinition = abstractNum?.Elements<Level>()
                .FirstOrDefault(l => l.LevelIndex?.Value == level);
        }

        var format = levelDefinition?.NumberingFormat?.Val;
        if (format is null)
        {
            return false;
        }

        return !format.Value.Equals(NumberFormatValues.Bullet) && !format.Value.Equals(NumberFormatValues.None);
    }

    // ---------------------------------------------------------------------
    // Contenido en línea (runs, hipervínculos, imágenes)
    // ---------------------------------------------------------------------

    private sealed class InlineSegment
    {
        public string Text = string.Empty;
        public bool Bold;
        public bool Italic;
        public bool Strike;
        public string? Link;
        public bool IsLineBreak;
        public bool IsRaw;
    }

    private static string RenderInlines(Paragraph paragraph, DocxContext ctx)
    {
        var segments = new List<InlineSegment>();
        CollectInlineSegments(paragraph, segments, ctx, link: null);
        return RenderSegments(segments);
    }

    private static void CollectInlineSegments(OpenXmlElement parent, List<InlineSegment> segments, DocxContext ctx, string? link)
    {
        foreach (var child in parent.ChildElements)
        {
            switch (child)
            {
                case Run run:
                    AppendRun(run, segments, ctx, link);
                    break;
                case Hyperlink hyperlink:
                    var url = ResolveHyperlink(hyperlink, ctx);
                    CollectInlineSegments(hyperlink, segments, ctx, url ?? link);
                    break;
                case SdtRun sdtRun:
                    var content = sdtRun.SdtContentRun;
                    if (content is not null)
                    {
                        CollectInlineSegments(content, segments, ctx, link);
                    }

                    break;
                case InsertedRun ins:
                    CollectInlineSegments(ins, segments, ctx, link);
                    break;
                case SimpleField field:
                    // w:fldSimple (DATE, PAGE, REF, MERGEFIELD...): los runs hijos
                    // contienen el resultado cacheado del campo.
                    CollectInlineSegments(field, segments, ctx, link);
                    break;
                case CustomXmlElement customXml:
                    // w:customXml en línea (documentos convertidos desde .doc).
                    CollectInlineSegments(customXml, segments, ctx, link);
                    break;
                    // Los DeletedRun (control de cambios) se omiten a propósito.
            }
        }
    }

    private static string? ResolveHyperlink(Hyperlink hyperlink, DocxContext ctx)
    {
        var id = hyperlink.Id?.Value;
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var relationship = ctx.Main.HyperlinkRelationships.FirstOrDefault(r => r.Id == id);
        return relationship?.Uri?.ToString();
    }

    private static void AppendRun(Run run, List<InlineSegment> segments, DocxContext ctx, string? link)
    {
        var properties = run.RunProperties;
        var bold = IsOn(properties?.Bold);
        var italic = IsOn(properties?.Italic);
        var strike = IsOn(properties?.Strike);

        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case Text text:
                    AddText(segments, text.Text, bold, italic, strike, link);
                    break;
                case TabChar:
                    AddText(segments, " ", bold, italic, strike, link);
                    break;
                case NoBreakHyphen:
                    AddText(segments, "-", bold, italic, strike, link);
                    break;
                case Break:
                case CarriageReturn:
                    // Los saltos de página/columna también se emiten como salto de línea
                    // para no pegar el texto anterior con el posterior.
                    segments.Add(new InlineSegment { IsLineBreak = true });
                    break;
                case FootnoteReference footnote when footnote.Id?.Value is long footnoteId:
                    AddNoteReference(segments, ctx, footnoteId, endnote: false, link);
                    break;
                case EndnoteReference endnote when endnote.Id?.Value is long endnoteId:
                    AddNoteReference(segments, ctx, endnoteId, endnote: true, link);
                    break;
                case Drawing drawing:
                    var image = RenderImage(drawing, ctx);
                    if (image is not null)
                    {
                        segments.Add(new InlineSegment { Text = image, IsRaw = true, Link = link });
                    }

                    break;
                case Picture:
                    ctx.WarnOnce("Se omitió una imagen en formato antiguo (VML) que Markdown no puede representar.");
                    break;
            }
        }
    }

    private static bool IsOn(OnOffType? element)
    {
        return element is not null && (element.Val is null || element.Val.Value);
    }

    private static void AddNoteReference(List<InlineSegment> segments, DocxContext ctx, long id, bool endnote, string? link)
    {
        var text = GetNoteText(id, endnote, ctx);
        if (text is null)
        {
            return;
        }

        var marker = $"[^{(endnote ? "E" : "F")}{id}]";
        segments.Add(new InlineSegment { Text = marker, IsRaw = true, Link = link });
        ctx.AddNoteDefinition(marker, text);
    }

    private static string? GetNoteText(long id, bool endnote, DocxContext ctx)
    {
        OpenXmlElement? note = endnote
            ? ctx.Main.EndnotesPart?.Endnotes?.Elements<Endnote>().FirstOrDefault(e => e.Id?.Value == id)
            : ctx.Main.FootnotesPart?.Footnotes?.Elements<Footnote>().FirstOrDefault(f => f.Id?.Value == id);
        if (note is null)
        {
            return null;
        }

        var text = MarkdownText.CollapseWhitespace(string.Concat(note.Descendants<Text>().Select(t => t.Text)));
        return text.Length == 0 ? null : MarkdownText.EscapeInline(text);
    }

    private static void AddText(List<InlineSegment> segments, string text, bool bold, bool italic, bool strike, string? link)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var last = segments.Count > 0 ? segments[segments.Count - 1] : null;
        if (last is not null && !last.IsLineBreak && !last.IsRaw &&
            last.Bold == bold && last.Italic == italic && last.Strike == strike && last.Link == link)
        {
            last.Text += text;
            return;
        }

        segments.Add(new InlineSegment { Text = text, Bold = bold, Italic = italic, Strike = strike, Link = link });
    }

    private static string RenderSegments(List<InlineSegment> segments)
    {
        var sb = new StringBuilder();
        string? openLink = null;

        foreach (var segment in segments)
        {
            var targetLink = segment.IsLineBreak ? openLink : segment.Link;

            if (openLink is not null && targetLink != openLink)
            {
                CloseLink(sb, openLink);
                openLink = null;
            }

            if (targetLink is not null && openLink is null && !segment.IsLineBreak)
            {
                sb.Append('[');
                openLink = targetLink;
            }

            if (segment.IsLineBreak)
            {
                sb.Append("  \n");
                continue;
            }

            if (segment.IsRaw)
            {
                sb.Append(segment.Text);
                continue;
            }

            AppendFormatted(sb, MarkdownText.EscapeInline(segment.Text), segment.Bold, segment.Italic, segment.Strike);
        }

        if (openLink is not null)
        {
            CloseLink(sb, openLink);
        }

        return sb.ToString();
    }

    private static void CloseLink(StringBuilder sb, string url)
    {
        var safeUrl = url.IndexOfAny(new[] { ' ', '(', ')' }) >= 0 ? "<" + url + ">" : url;
        sb.Append("](").Append(safeUrl).Append(')');
    }

    private static void AppendFormatted(StringBuilder sb, string text, bool bold, bool italic, bool strike)
    {
        if ((!bold && !italic && !strike) || string.IsNullOrWhiteSpace(text))
        {
            sb.Append(text);
            return;
        }

        // Los marcadores de formato no pueden envolver espacios al inicio/final del texto.
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        var end = text.Length;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var open = (bold ? "**" : string.Empty) + (italic ? "*" : string.Empty) + (strike ? "~~" : string.Empty);
        var close = (strike ? "~~" : string.Empty) + (italic ? "*" : string.Empty) + (bold ? "**" : string.Empty);

        sb.Append(text, 0, start)
          .Append(open)
          .Append(text, start, end - start)
          .Append(close)
          .Append(text, end, text.Length - end);
    }

    // ---------------------------------------------------------------------
    // Imágenes
    // ---------------------------------------------------------------------

    private static string? RenderImage(Drawing drawing, DocxContext ctx)
    {
        var docProperties = drawing.Descendants<DW.DocProperties>().FirstOrDefault();
        var alt = docProperties?.Description?.Value;
        if (string.IsNullOrWhiteSpace(alt))
        {
            alt = docProperties?.Name?.Value;
        }

        if (string.IsNullOrWhiteSpace(alt))
        {
            alt = "imagen";
        }

        if (!ctx.Options.ExtractImages)
        {
            ctx.WarnOnce("El documento contiene imágenes que se omitieron. " +
                         "Active ConversionOptions.ExtractImages e indique ImageOutputDirectory para extraerlas.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(ctx.Options.ImageOutputDirectory))
        {
            ctx.WarnOnce("ExtractImages está activo pero no se indicó ImageOutputDirectory; las imágenes se omitieron.");
            return null;
        }

        var relId = drawing.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;
        if (string.IsNullOrEmpty(relId))
        {
            return null;
        }

        ImagePart? imagePart = null;
        try
        {
            imagePart = ctx.Main.GetPartById(relId!) as ImagePart;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Relación inexistente: se ignora la imagen.
        }

        if (imagePart is null)
        {
            return null;
        }

        var extension = GetImageExtension(imagePart.ContentType);
        var directory = ctx.Options.ImageOutputDirectory!;
        string fileName;

        try
        {
            Directory.CreateDirectory(directory);

            // FileMode.CreateNew evita sobrescribir imágenes de otra conversión que
            // comparta el mismo directorio (incluidas conversiones concurrentes).
            while (true)
            {
                fileName = $"imagen-{++ctx.ImageCount:000}{extension}";
                var filePath = Path.Combine(directory, fileName);
                try
                {
                    using var target = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write);
                    using var source = imagePart.GetStream();
                    source.CopyTo(target);
                    break;
                }
                catch (IOException) when (File.Exists(filePath))
                {
                    // Nombre ocupado: probar el siguiente índice.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new MarkdownConversionException(
                "No se pudo escribir la imagen extraída en ImageOutputDirectory.", ex);
        }

        var prefix = ctx.Options.ImageLinkPrefix;
        var linkPath = string.IsNullOrEmpty(prefix)
            ? fileName
            : prefix.TrimEnd('/') + "/" + fileName;

        return $"![{MarkdownText.EscapeInline(alt)}]({linkPath})";
    }

    private static string GetImageExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/svg+xml" => ".svg",
            "image/x-emf" or "image/emf" => ".emf",
            "image/x-wmf" or "image/wmf" => ".wmf",
            _ => ".bin",
        };
    }

    // ---------------------------------------------------------------------
    // Tablas
    // ---------------------------------------------------------------------

    private static string RenderTable(Table table, DocxContext ctx)
    {
        var rows = new List<List<string>>();
        foreach (var row in GetTableRows(table))
        {
            var cells = new List<string>();
            foreach (var cell in GetRowCells(row))
            {
                var parts = cell.Descendants<Paragraph>()
                    .Select(p => RenderInlines(p, ctx))
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                cells.Add(MarkdownText.ForTableCell(string.Join("<br>", parts)));

                // Las celdas combinadas horizontalmente ocupan columnas extra vacías.
                var span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                for (var i = 1; i < span; i++)
                {
                    cells.Add(string.Empty);
                }
            }

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = rows.Max(r => r.Count);
        var sb = new StringBuilder();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex];
            sb.Append('|');
            for (var col = 0; col < columnCount; col++)
            {
                var value = col < cells.Count ? cells[col] : string.Empty;
                sb.Append(' ').Append(value.Length == 0 ? " " : value).Append(" |");
            }

            sb.Append('\n');

            if (rowIndex == 0)
            {
                sb.Append('|');
                for (var col = 0; col < columnCount; col++)
                {
                    sb.Append(" --- |");
                }

                sb.Append('\n');
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Filas de la tabla, desenvolviendo las que están dentro de content controls (w:sdt).</summary>
    private static IEnumerable<TableRow> GetTableRows(Table table)
    {
        foreach (var child in table.ChildElements)
        {
            switch (child)
            {
                case TableRow row:
                    yield return row;
                    break;
                case SdtRow sdt when sdt.SdtContentRow is not null:
                    foreach (var inner in sdt.SdtContentRow.Elements<TableRow>())
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    /// <summary>Celdas de la fila, desenvolviendo las que están dentro de content controls (w:sdt).</summary>
    private static IEnumerable<TableCell> GetRowCells(TableRow row)
    {
        foreach (var child in row.ChildElements)
        {
            switch (child)
            {
                case TableCell cell:
                    yield return cell;
                    break;
                case SdtCell sdt when sdt.SdtContentCell is not null:
                    foreach (var inner in sdt.SdtContentCell.Elements<TableCell>())
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------

    private static string GetPlainText(Paragraph paragraph)
    {
        return string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
    }

    private static string Normalize(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var value = extension.Trim().ToLowerInvariant();
        return value.StartsWith(".") ? value : "." + value;
    }

    private sealed class DocxContext
    {
        private readonly List<Style>? _styles;
        private readonly HashSet<string> _warned = new();
        private readonly HashSet<string> _noteMarkers = new();

        public DocxContext(MainDocumentPart main, ConversionOptions options)
        {
            Main = main;
            Options = options;
            _styles = main.StyleDefinitionsPart?.Styles?.Elements<Style>().ToList();
        }

        public MainDocumentPart Main { get; }

        public ConversionOptions Options { get; }

        public List<string> Warnings { get; } = new();

        public List<(string Marker, string Text)> NoteDefinitions { get; } = new();

        public string? FirstHeadingText { get; set; }

        public int ImageCount;

        public int LastListLevel = -1;

        public void AddNoteDefinition(string marker, string text)
        {
            if (_noteMarkers.Add(marker))
            {
                NoteDefinitions.Add((marker, text));
            }
        }

        public Style? FindStyle(string styleId)
        {
            return _styles?.FirstOrDefault(s =>
                string.Equals(s.StyleId?.Value, styleId, StringComparison.OrdinalIgnoreCase));
        }

        public void WarnOnce(string message)
        {
            if (_warned.Add(message))
            {
                Warnings.Add(message);
            }
        }
    }
}
