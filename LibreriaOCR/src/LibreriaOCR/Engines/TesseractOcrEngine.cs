using LibreriaOCR.Internal;
using TesseractOCR;
using TesseractOCR.Enums;

namespace LibreriaOCR.Engines;

/// <summary>
/// Motor de OCR basado en Tesseract. Una instancia NO es thread-safe:
/// <see cref="OcrService"/> mantiene un pool de instancias para usarla con seguridad en concurrencia.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly Engine _engine;
    private bool _disposed;

    public TesseractOcrEngine(OcrOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dataPath = ResolveTessDataPath(options.TessDataPath);
        var languages = string.IsNullOrWhiteSpace(options.Languages) ? "spa" : options.Languages.Trim();

        try
        {
            _engine = new Engine(dataPath, languages, EngineMode.Default);
        }
        catch (Exception ex)
        {
            throw new OcrException(
                $"No se pudo inicializar Tesseract (idioma='{languages}', tessdata='{dataPath}'). " +
                "Verifica que exista el archivo de idioma correspondiente (por ejemplo, 'spa.traineddata').", ex);
        }
    }

    public IReadOnlyList<OcrTextResult> Recognize(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (imageBytes is null || imageBytes.Length == 0)
            throw new ArgumentException("La imagen está vacía.", nameof(imageBytes));

        return FileTypeDetector.IsTiff(imageBytes)
            ? RecognizeTiff(imageBytes, cancellationToken)
            : RecognizeSingle(imageBytes, cancellationToken);
    }

    private IReadOnlyList<OcrTextResult> RecognizeSingle(byte[] imageBytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var image = TesseractOCR.Pix.Image.LoadFromMemory(imageBytes);
        using var page = _engine.Process(image);
        return new[] { new OcrTextResult(page.Text ?? string.Empty, ToPercent(page.MeanConfidence)) };
    }

    private IReadOnlyList<OcrTextResult> RecognizeTiff(byte[] tiffBytes, CancellationToken ct)
    {
        var results = new List<OcrTextResult>();

        // Leptonica (incluido con Tesseract) carga el TIFF multipágina; el enumerador libera cada imagen.
        using var array = TesseractOCR.Pix.Array.LoadMultiPageTiffFromMemory(tiffBytes);
        foreach (var image in array)
        {
            ct.ThrowIfCancellationRequested();
            using var page = _engine.Process(image);
            results.Add(new OcrTextResult(page.Text ?? string.Empty, ToPercent(page.MeanConfidence)));
        }

        if (results.Count == 0)
            throw new OcrException("El TIFF no contiene páginas legibles.");

        return results;
    }

    /// <summary>Tesseract entrega la confianza como fracción 0–1; la convertimos a porcentaje 0–100.</summary>
    private static float ToPercent(float meanConfidence) =>
        meanConfidence <= 1f ? meanConfidence * 100f : meanConfidence;

    private static string ResolveTessDataPath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Directory.Exists(configured))
                throw new OcrException($"La carpeta de tessdata indicada no existe: '{configured}'.");
            return configured;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(path))
            throw new OcrException(
                $"No se encontró la carpeta 'tessdata' en '{AppContext.BaseDirectory}'. " +
                "Asegúrate de que 'spa.traineddata' se copie al directorio de salida " +
                "o establece OcrOptions.TessDataPath.");
        return path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _engine.Dispose();
    }
}
