using System.Diagnostics;
using LibreriaOCR.Engines;
using LibreriaOCR.Internal;
using SkiaSharp;

namespace LibreriaOCR;

/// <summary>
/// Detección heurística de escritura a mano (sin IA) en documentos (PDF, PNG, JPG o TIFF).
/// Devuelve, por página, las regiones probablemente manuscritas (b) y una bandera global (a).
/// <para>
/// Es una heurística: conviene calibrar <see cref="HandwritingOptions.ScoreThreshold"/> con documentos reales.
/// Funciona mejor con firmas y campos llenados a mano que con texto corrido manuscrito.
/// </para>
/// </summary>
public sealed class HandwritingDetector : IDisposable
{
    private readonly HandwritingOptions _options;
    private TesseractOcrEngine? _engine;

    public HandwritingDetector(HandwritingOptions? options = null)
        => _options = options ?? new HandwritingOptions();

    /// <summary>Analiza un archivo en disco.</summary>
    public HandwritingResult Detect(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("La ruta del archivo está vacía.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("No se encontró el archivo.", filePath);

        return Detect(File.ReadAllBytes(filePath), Path.GetFileName(filePath), cancellationToken);
    }

    /// <summary>Analiza un documento en memoria.</summary>
    public HandwritingResult Detect(byte[] data, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (data is null || data.Length == 0)
            throw new ArgumentException("El documento está vacío.", nameof(data));

        var stopwatch = Stopwatch.StartNew();
        var format = FileTypeDetector.Detect(data, fileName);
        var pages = new List<HandwritingPage>();

        switch (format)
        {
            case DocumentFormat.Pdf:
                int pageCount;
                try { pageCount = PdfProcessor.GetPageCount(data); }
                catch (Exception ex) { throw new OcrException("No se pudo leer el PDF.", ex); }

                for (var i = 0; i < pageCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] png;
                    try { png = PdfProcessor.RenderPageToPng(data, i, _options.PdfDpi); }
                    catch (Exception ex) { throw new OcrException($"No se pudo rasterizar la página {i + 1} del PDF.", ex); }

                    using var bmp = Decode(png);
                    if (bmp is not null)
                        pages.Add(AnalyzePage(bmp, i + 1, cancellationToken));
                }
                break;

            case DocumentFormat.Png:
            case DocumentFormat.Jpeg:
            case DocumentFormat.Tiff:
                using (var bmp = DecodeImage(data))
                {
                    if (bmp is null)
                        throw new OcrException("No se pudo decodificar la imagen para el análisis de manuscrito.");
                    pages.Add(AnalyzePage(bmp, 1, cancellationToken));
                }
                break;

            default:
                throw new FormatoNoSoportadoException(
                    $"Formato no soportado para '{fileName ?? "(sin nombre)"}'. " +
                    "Formatos válidos: PDF, PNG, JPG/JPEG y TIF/TIFF.");
        }

        stopwatch.Stop();

        var totalRegions = pages.Sum(p => p.Regions.Count);
        return new HandwritingResult
        {
            HasHandwriting = totalRegions >= _options.MinRegionsForDocument,
            MaxScore = pages.Count > 0 ? pages.Max(p => p.MaxScore) : 0f,
            Pages = pages,
            FileName = fileName,
            Duration = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Analiza una página: geometría para hallar candidatos y, si los hay, OCR para (1) confirmar que es
    /// manuscrito (confianza baja) y (2) delimitar las regiones/líneas con las palabras de baja confianza.
    /// </summary>
    private HandwritingPage AnalyzePage(SKBitmap bmp, int pageNumber, CancellationToken ct)
    {
        var geo = HandwritingAnalyzer.AnalyzeGeometry(bmp, pageNumber, _options);

        // Sin candidatos geométricos: página sin escritura a mano (no se gasta OCR).
        if (geo.Regions.Count == 0)
            return MakePage(pageNumber, new List<HandwritingRegion>(), geo.MaxScore, -1f);

        // Sin corroboración: se devuelve lo que dice la geometría.
        if (!_options.CorroborateWithOcr)
            return MakePage(pageNumber, geo.Regions, geo.MaxScore, -1f);

        IReadOnlyList<OcrWord> words;
        try
        {
            using var data = bmp.Encode(SKEncodedImageFormat.Png, 90);
            words = GetEngine().RecognizeWords(data.ToArray(), ct);
        }
        catch
        {
            // OCR no disponible: se conserva el resultado por geometría.
            return MakePage(pageNumber, geo.Regions, geo.MaxScore, -1f);
        }

        var meanConfidence = MeanConfidence(words);

        // Texto legible (confianza alta) => impreso / logo, no manuscrito.
        if (meanConfidence >= 0 && meanConfidence >= _options.MaxOcrConfidenceForHandwriting)
            return MakePage(pageNumber, new List<HandwritingRegion>(), geo.MaxScore, meanConfidence);

        // Manuscrito: se delimitan las regiones con las palabras de baja confianza y se suman las de geometría.
        var wordRegions = HandwritingAnalyzer.BuildWordRegions(
            words, geo.Colorful, geo.Width, geo.Height, geo.Scale, pageNumber, _options);

        var regions = new List<HandwritingRegion>(wordRegions);
        foreach (var g in geo.Regions)
            if (!regions.Any(r => Overlaps(r, g)))
                regions.Add(g);

        if (regions.Count == 0)
            regions = geo.Regions;

        var maxScore = regions.Count > 0 ? regions.Max(r => r.Score) : geo.MaxScore;
        return MakePage(pageNumber, regions, maxScore, meanConfidence);
    }

    private static float MeanConfidence(IReadOnlyList<OcrWord> words)
    {
        double sum = 0;
        var count = 0;
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text) || word.Confidence < 0) continue;
            sum += word.Confidence;
            count++;
        }
        return count > 0 ? (float)(sum / count) : -1f;
    }

    private static bool Overlaps(HandwritingRegion a, HandwritingRegion b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top) return false;

        var intersection = (double)(right - left) * (bottom - top);
        var smaller = Math.Min((double)a.Width * a.Height, (double)b.Width * b.Height);
        return smaller > 0 && intersection >= smaller * 0.5;
    }

    private static HandwritingPage MakePage(int pageNumber, List<HandwritingRegion> regions, float maxScore, float ocrConfidence) => new()
    {
        PageNumber = pageNumber,
        HasHandwriting = regions.Count > 0,
        MaxScore = maxScore,
        OcrConfidence = ocrConfidence,
        Regions = regions
    };

    private TesseractOcrEngine GetEngine() => _engine ??= new TesseractOcrEngine(new OcrOptions { Languages = _options.Languages });

    public void Dispose() => _engine?.Dispose();

    /// <summary>Decodifica una imagen cualquiera. Usa Leptonica (soporta TIFF) y cae a SkiaSharp si hace falta.</summary>
    private static SKBitmap? DecodeImage(byte[] data)
    {
        var png = Leptonica.ConvertToPng(data);
        if (png is not null)
        {
            var viaLeptonica = Decode(png);
            if (viaLeptonica is not null) return viaLeptonica;
        }
        return Decode(data); // PNG/JPG directos si Leptonica no estuviera disponible
    }

    private static SKBitmap? Decode(byte[] bytes)
    {
        try { return SKBitmap.Decode(bytes); }
        catch { return null; }
    }
}
