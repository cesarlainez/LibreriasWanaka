using System.Text;
using System.Text.RegularExpressions;
using MarkItDown.Converters.Common;
using MarkItDown.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Exceptions;

namespace MarkItDown.Converters.Pdf;

/// <summary>
/// Convierte documentos PDF a Markdown usando PdfPig (sin IA ni OCR).
/// Agrupa el texto en bloques con análisis de layout, respeta el orden de lectura
/// y detecta encabezados por tamaño de fuente, viñetas y listas numeradas por heurística.
/// </summary>
/// <remarks>
/// Un PDF no guarda estructura semántica (a diferencia de .docx), por lo que la
/// detección de encabezados y listas es heurística. Los PDF escaneados (solo imagen)
/// no contienen texto extraíble y producen un resultado vacío con advertencia.
/// </remarks>
public sealed class PdfToMarkdownConverter : IDocumentConverter
{
    // Solo el guion ASCII cuenta como viñeta: '–' y '—' al inicio de línea son casi
    // siempre rayas de diálogo, rangos o signos negativos, no marcadores de lista.
    private static readonly Regex BulletLine = new(@"^\s*[•◦▪●‣∙·○♦]\s*(\S.*)$", RegexOptions.Compiled);
    private static readonly Regex DashBulletLine = new(@"^\s*-\s+(\S.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberedLine = new(@"^\s*(\d{1,3})[.)]\s+(\S.*)$", RegexOptions.Compiled);
    private static readonly Regex WordToken = new(@"\p{L}{3,}", RegexOptions.Compiled);

    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".pdf" };

    public bool CanConvert(string extension) => Normalize(extension) == ".pdf";

    public ConversionResult Convert(Stream input, ConversionOptions? options = null)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        options ??= new ConversionOptions();

        using var buffer = new MemoryStream();
        try
        {
            input.CopyTo(buffer);
            buffer.Position = 0;
            using var document = PdfDocument.Open(buffer);
            return ConvertDocument(document, options);
        }
        catch (MarkdownConversionException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new MarkdownConversionException(
                "El PDF está protegido con contraseña y no puede convertirse.", ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Con archivos dañados PdfPig puede lanzar tipos arbitrarios (incluso internos);
            // el contrato de la librería es lanzar siempre MarkdownConversionException.
            throw new MarkdownConversionException(
                "No se pudo leer el archivo PDF. Verifique que no esté dañado.", ex);
        }
    }

    private static ConversionResult ConvertDocument(PdfDocument document, ConversionOptions options)
    {
        var warnings = new List<string>();
        var pages = new List<(int PageNumber, List<BlockInfo> Blocks)>();
        var allSizes = new List<double>();
        var boldLetters = 0L;
        var totalLetters = 0L;

        foreach (var page in document.GetPages())
        {
            var blocks = ExtractBlocks(page, warnings);
            foreach (var block in blocks)
            {
                allSizes.AddRange(block.LetterSizes);
                boldLetters += block.BoldLetterCount;
                totalLetters += block.TotalLetterCount;
            }

            pages.Add((page.Number, blocks));
        }

        if (totalLetters == 0)
        {
            warnings.Add("El PDF no contiene texto extraíble (probablemente es un documento escaneado). " +
                         "Esta librería no realiza OCR, por lo que el resultado está vacío.");
        }

        var bodySize = Median(allSizes);
        var documentBoldRatio = totalLetters > 0 ? (double)boldLetters / totalLetters : 0;

        if (options.DetectPdfHeadings && bodySize > 0)
        {
            AssignHeadingLevels(pages.SelectMany(p => p.Blocks), bodySize, documentBoldRatio);
        }

        // Vocabulario del documento para decidir si un guion de fin de línea es
        // silabación (se une) o parte de una palabra compuesta (se conserva).
        var vocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in pages.SelectMany(p => p.Blocks))
        {
            foreach (var line in block.Lines)
            {
                foreach (Match token in WordToken.Matches(line))
                {
                    vocabulary.Add(token.Value);
                }
            }
        }

        var builder = new MarkdownDocumentBuilder();
        var listState = new PdfListState();
        string? firstHeading = null;

        foreach (var (pageNumber, blocks) in pages)
        {
            if (options.IncludePageMarkers)
            {
                builder.AddBlock($"<!-- Página {pageNumber} -->");
            }

            foreach (var block in blocks)
            {
                if (block.HeadingLevel > 0)
                {
                    var text = MarkdownText.CollapseWhitespace(block.FullText);
                    firstHeading ??= text;
                    builder.AddBlock(new string('#', block.HeadingLevel) + " " + MarkdownText.EscapeInline(text));
                }
                else
                {
                    RenderBodyBlock(block, builder, vocabulary, listState);
                }
            }
        }

        var title = document.Information?.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = firstHeading;
        }

        return new ConversionResult(builder.Build(), title, warnings);
    }

    // ---------------------------------------------------------------------
    // Extracción de bloques con análisis de layout
    // ---------------------------------------------------------------------

    private static List<BlockInfo> ExtractBlocks(Page page, List<string> warnings)
    {
        var result = new List<BlockInfo>();

        try
        {
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
            var textBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
            var ordered = UnsupervisedReadingOrderDetector.Instance.Get(textBlocks);

            foreach (var textBlock in ordered)
            {
                var info = new BlockInfo();
                foreach (var line in textBlock.TextLines)
                {
                    var lineText = line.Text?.Trim();
                    if (string.IsNullOrEmpty(lineText))
                    {
                        continue;
                    }

                    info.Lines.Add(lineText!);
                    foreach (var word in line.Words)
                    {
                        info.WordCount++;
                        foreach (var letter in word.Letters)
                        {
                            info.TotalLetterCount++;
                            if (letter.PointSize > 0)
                            {
                                info.LetterSizes.Add(letter.PointSize);
                            }

                            if (letter.FontDetails?.IsBold == true)
                            {
                                info.BoldLetterCount++;
                            }
                        }
                    }
                }

                if (info.Lines.Count > 0)
                {
                    info.AverageSize = info.LetterSizes.Count > 0 ? info.LetterSizes.Average() : 0;
                    info.MostlyBold = info.TotalLetterCount > 0 &&
                                      info.BoldLetterCount >= info.TotalLetterCount * 0.8;
                    result.Add(info);
                }
            }

            return result;
        }
        catch (Exception)
        {
            // Si el análisis de layout falla en esta página, se recurre a extracción simple de texto.
            warnings.Add($"El análisis de estructura falló en la página {page.Number}; " +
                         "se usó extracción de texto simple para esa página.");
        }

        var fallback = new BlockInfo();
        foreach (var rawLine in page.Text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length > 0)
            {
                fallback.Lines.Add(line);
            }
        }

        return fallback.Lines.Count > 0 ? new List<BlockInfo> { fallback } : new List<BlockInfo>();
    }

    // ---------------------------------------------------------------------
    // Detección de encabezados por tamaño de fuente
    // ---------------------------------------------------------------------

    private static void AssignHeadingLevels(IEnumerable<BlockInfo> blocks, double bodySize, double documentBoldRatio)
    {
        var blockList = blocks.ToList();
        var sizeCandidates = new List<BlockInfo>();
        var boldCandidates = new List<BlockInfo>();

        foreach (var block in blockList)
        {
            if (block.Lines.Count == 0 || block.Lines.Count > 2 || block.WordCount > 20)
            {
                continue;
            }

            var text = block.FullText.TrimEnd();
            if (text.Length == 0 || ".:;,".IndexOf(text[text.Length - 1]) >= 0)
            {
                continue;
            }

            // Las viñetas nunca son encabezados; las líneas numeradas ("2. Metodología")
            // solo se descartan si su tamaño no las distingue del cuerpo, para no perder
            // los encabezados de sección numerados típicos de informes técnicos.
            if (BulletLine.IsMatch(text) || DashBulletLine.IsMatch(text) ||
                (NumberedLine.IsMatch(text) && block.AverageSize < bodySize * 1.15))
            {
                continue;
            }

            if (block.AverageSize >= bodySize * 1.15)
            {
                sizeCandidates.Add(block);
            }
            else if (block.MostlyBold && documentBoldRatio < 0.5 && block.Lines.Count == 1 &&
                     block.WordCount <= 12 && block.AverageSize >= bodySize * 0.95)
            {
                boldCandidates.Add(block);
            }
        }

        // Los tamaños distintos (redondeados a medio punto) definen la jerarquía: mayor tamaño → nivel 1.
        var distinctSizes = sizeCandidates
            .Select(b => Math.Round(b.AverageSize * 2) / 2)
            .Distinct()
            .OrderByDescending(s => s)
            .ToList();

        foreach (var block in sizeCandidates)
        {
            var rounded = Math.Round(block.AverageSize * 2) / 2;
            var level = distinctSizes.IndexOf(rounded) + 1;
            block.HeadingLevel = Math.Min(level, 6);
        }

        // Las líneas en negrita del mismo tamaño que el cuerpo quedan un nivel por debajo.
        var boldLevel = distinctSizes.Count == 0 ? 2 : Math.Min(distinctSizes.Count + 1, 6);
        foreach (var block in boldCandidates)
        {
            block.HeadingLevel = boldLevel;
        }
    }

    // ---------------------------------------------------------------------
    // Cuerpo: párrafos, viñetas y listas numeradas
    // ---------------------------------------------------------------------

    private static void RenderBodyBlock(BlockInfo block, MarkdownDocumentBuilder builder, HashSet<string> vocabulary, PdfListState listState)
    {
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            if (paragraph.Length > 0)
            {
                builder.AddBlock(MarkdownText.EscapeLineStart(MarkdownText.EscapeInline(paragraph.ToString().Trim())));
                paragraph.Clear();
            }
        }

        foreach (var line in block.Lines)
        {
            var bullet = BulletLine.Match(line);
            var isDash = false;
            if (!bullet.Success)
            {
                bullet = DashBulletLine.Match(line);
                isDash = bullet.Success;
            }

            // Un guion solo cuenta como viñeta si no es continuación de una frase:
            // "de 10" + "- 20 km" debe seguir siendo parte del párrafo.
            if (bullet.Success &&
                (!isDash || paragraph.Length == 0 || ".:;!?".IndexOf(paragraph[paragraph.Length - 1]) >= 0))
            {
                FlushParagraph();
                builder.AddListItem("- " + MarkdownText.EscapeInline(bullet.Groups[1].Value.Trim()), listState.KeyForBullet());
                continue;
            }

            var numbered = NumberedLine.Match(line);
            if (numbered.Success)
            {
                FlushParagraph();
                var number = int.Parse(numbered.Groups[1].Value);
                builder.AddListItem(
                    numbered.Groups[1].Value + ". " + MarkdownText.EscapeInline(numbered.Groups[2].Value.Trim()),
                    listState.KeyForNumbered(number));
                continue;
            }

            AppendWithDehyphenation(paragraph, line, vocabulary);
        }

        FlushParagraph();
    }

    /// <summary>
    /// Une líneas de un mismo párrafo, resolviendo palabras cortadas con guion al final de línea.
    /// El guion se elimina solo si es un guion suave (silabación inequívoca) o si la palabra
    /// fusionada aparece en otra parte del documento; en caso contrario se conserva
    /// (palabras compuestas como "socio-económico" cortadas justo en su guion).
    /// </summary>
    private static void AppendWithDehyphenation(StringBuilder paragraph, string line, HashSet<string> vocabulary)
    {
        if (paragraph.Length == 0)
        {
            paragraph.Append(line);
            return;
        }

        var last = paragraph[paragraph.Length - 1];

        // Guion suave (U+00AD): siempre es silabación.
        if (last == '­')
        {
            paragraph.Length--;
            paragraph.Append(line);
            return;
        }

        if ((last == '-' || last == '‐') && line.Length > 0 && char.IsLower(line[0]))
        {
            var leftFragment = LastWordBeforeHyphen(paragraph);
            var rightFragment = FirstWord(line);
            var merged = leftFragment + rightFragment;

            if (merged.Length > 0 && vocabulary.Contains(merged))
            {
                paragraph.Length--;
                paragraph.Append(line);
            }
            else
            {
                // Se conserva el guion pero se une sin espacio para no partir la palabra.
                paragraph.Append(line);
            }

            return;
        }

        paragraph.Append(' ').Append(line);
    }

    private static string LastWordBeforeHyphen(StringBuilder paragraph)
    {
        var end = paragraph.Length - 1;
        var start = end;
        while (start > 0 && char.IsLetter(paragraph[start - 1]))
        {
            start--;
        }

        var sb = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            sb.Append(paragraph[i]);
        }

        return sb.ToString();
    }

    private static string FirstWord(string line)
    {
        var end = 0;
        while (end < line.Length && char.IsLetter(line[end]))
        {
            end++;
        }

        return line.Substring(0, end);
    }

    /// <summary>
    /// Da identidad a las listas detectadas en el PDF para que dos listas distintas no se
    /// fusionen: una lista numerada que reinicia (o retrocede) su número abre una lista nueva.
    /// </summary>
    private sealed class PdfListState
    {
        private int _sequence;
        private string? _currentKey;
        private int _lastNumber;

        public string KeyForBullet()
        {
            if (_currentKey is null || !_currentKey.StartsWith("ul", StringComparison.Ordinal))
            {
                _currentKey = "ul" + ++_sequence;
            }

            return _currentKey;
        }

        public string KeyForNumbered(int number)
        {
            if (_currentKey is null || !_currentKey.StartsWith("ol", StringComparison.Ordinal) || number <= _lastNumber)
            {
                _currentKey = "ol" + ++_sequence;
            }

            _lastNumber = number;
            return _currentKey;
        }
    }

    // ---------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
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

    private sealed class BlockInfo
    {
        public List<string> Lines { get; } = new();

        public List<double> LetterSizes { get; } = new();

        public double AverageSize { get; set; }

        public int WordCount { get; set; }

        public long TotalLetterCount { get; set; }

        public long BoldLetterCount { get; set; }

        public bool MostlyBold { get; set; }

        public int HeadingLevel { get; set; }

        public string FullText => string.Join(" ", Lines);
    }
}
