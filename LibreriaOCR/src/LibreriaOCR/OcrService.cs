using System.Collections.Concurrent;
using System.Diagnostics;
using LibreriaOCR.Engines;
using LibreriaOCR.Internal;

namespace LibreriaOCR;

/// <summary>
/// Servicio de OCR de alto nivel. Recibe un documento (PDF, PNG, JPG o TIFF) y devuelve su texto.
/// <para>
/// Es thread-safe: se puede compartir una sola instancia (por ejemplo, como singleton en una
/// aplicación web) y procesar varias peticiones en paralelo gracias a un pool interno de motores.
/// </para>
/// </summary>
public sealed class OcrService : IDisposable
{
    private readonly OcrOptions _options;
    private readonly Func<IOcrEngine> _engineFactory;
    private readonly ConcurrentBag<IOcrEngine> _pool = new();
    private readonly SemaphoreSlim _gate;
    private bool _disposed;

    /// <summary>Crea el servicio con el motor Tesseract por defecto.</summary>
    public OcrService(OcrOptions? options = null)
        : this(null, options)
    {
    }

    /// <summary>
    /// Crea el servicio con una fábrica de motores personalizada
    /// (por ejemplo, para usar un motor de OCR distinto de Tesseract).
    /// </summary>
    public OcrService(Func<IOcrEngine>? engineFactory, OcrOptions? options = null)
    {
        _options = options ?? new OcrOptions();
        _engineFactory = engineFactory ?? (() => new TesseractOcrEngine(_options));

        var max = _options.MaxConcurrency ?? Environment.ProcessorCount;
        if (max < 1) max = 1;
        _gate = new SemaphoreSlim(max, max);

        // Creamos un motor de inmediato para fallar rápido si falta el idioma o la carpeta tessdata.
        _pool.Add(_engineFactory());
    }

    // ------------------------------------------------------------------ API pública

    /// <summary>Reconoce el texto de un archivo en disco.</summary>
    public OcrResult Recognize(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("La ruta del archivo está vacía.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("No se encontró el archivo.", filePath);

        var bytes = File.ReadAllBytes(filePath);
        return Recognize(bytes, Path.GetFileName(filePath), cancellationToken);
    }

    /// <summary>Reconoce el texto de un documento leído desde un <see cref="Stream"/>.</summary>
    public OcrResult Recognize(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Recognize(ms.ToArray(), fileName, cancellationToken);
    }

    /// <summary>Reconoce el texto de un documento en memoria.</summary>
    /// <param name="data">Bytes del documento (PDF, PNG, JPG o TIFF).</param>
    /// <param name="fileName">Nombre del archivo (opcional; ayuda a detectar el formato).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public OcrResult Recognize(byte[] data, string? fileName = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (data is null || data.Length == 0)
            throw new ArgumentException("El documento está vacío.", nameof(data));

        var stopwatch = Stopwatch.StartNew();
        var format = FileTypeDetector.Detect(data, fileName);

        var (pages, sourceKind) = format switch
        {
            DocumentFormat.Pdf => ProcessPdf(data, cancellationToken),
            DocumentFormat.Png or DocumentFormat.Jpeg or DocumentFormat.Tiff => ProcessImage(data, cancellationToken),
            _ => throw new FormatoNoSoportadoException(
                $"Formato no soportado para '{fileName ?? "(sin nombre)"}'. " +
                "Formatos válidos: PDF, PNG, JPG/JPEG y TIF/TIFF.")
        };
        stopwatch.Stop();

        return BuildResult(pages, sourceKind, stopwatch.Elapsed, fileName);
    }

    /// <summary>Versión asíncrona (ejecuta el OCR en un hilo del pool de tareas).</summary>
    public Task<OcrResult> RecognizeAsync(string filePath, CancellationToken cancellationToken = default)
        => Task.Run(() => Recognize(filePath, cancellationToken), cancellationToken);

    /// <summary>Versión asíncrona a partir de los bytes del documento.</summary>
    public Task<OcrResult> RecognizeAsync(byte[] data, string? fileName = null, CancellationToken cancellationToken = default)
        => Task.Run(() => Recognize(data, fileName, cancellationToken), cancellationToken);

    /// <summary>Versión asíncrona a partir de un <see cref="Stream"/>.</summary>
    public Task<OcrResult> RecognizeAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        // Leemos el stream aquí (hilo llamante) para no depender de su ciclo de vida dentro de la tarea.
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        return Task.Run(() => Recognize(bytes, fileName, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Atajo para usos puntuales: crea un servicio, procesa el archivo y libera recursos.
    /// No usar en bucles de alto volumen (carga el idioma en cada llamada); para eso, reutiliza una instancia.
    /// </summary>
    public static OcrResult ExtractText(string filePath, OcrOptions? options = null)
    {
        using var service = new OcrService(options);
        return service.Recognize(filePath);
    }

    // ------------------------------------------------------------------ Procesamiento

    private (List<OcrPage> pages, OcrSourceKind kind) ProcessImage(byte[] data, CancellationToken ct)
    {
        var engine = RentEngine(ct);
        try
        {
            var recognized = engine.Recognize(data, ct); // 1 resultado (PNG/JPG) o N (TIFF multipágina)
            var pages = new List<OcrPage>(recognized.Count);
            for (var i = 0; i < recognized.Count; i++)
            {
                pages.Add(new OcrPage
                {
                    PageNumber = i + 1,
                    Text = recognized[i].Text,
                    Confidence = recognized[i].Confidence,
                    FromEmbeddedText = false
                });
            }
            return (pages, OcrSourceKind.Image);
        }
        finally
        {
            ReturnEngine(engine);
        }
    }

    private (List<OcrPage> pages, OcrSourceKind kind) ProcessPdf(byte[] data, CancellationToken ct)
    {
        string[] embedded;
        int pageCount;
        try
        {
            embedded = _options.PreferEmbeddedPdfText
                ? PdfProcessor.ExtractEmbeddedText(data)
                : Array.Empty<string>();
            pageCount = embedded.Length > 0 ? embedded.Length : PdfProcessor.GetPageCount(data);
        }
        catch (Exception ex)
        {
            throw new OcrException("No se pudo leer el PDF (¿archivo dañado o protegido con contraseña?).", ex);
        }

        var pages = new List<OcrPage>(pageCount);
        var anyOcr = false;
        var anyText = false;

        for (var i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var embeddedText = i < embedded.Length ? embedded[i] : null;
            if (embeddedText is not null && CountNonWhitespace(embeddedText) >= _options.MinEmbeddedCharsPerPage)
            {
                pages.Add(new OcrPage
                {
                    PageNumber = i + 1,
                    Text = embeddedText.Trim(),
                    Confidence = -1f,
                    FromEmbeddedText = true
                });
                anyText = true;
                continue;
            }

            byte[] png;
            try
            {
                png = PdfProcessor.RenderPageToPng(data, i, _options.PdfDpi);
            }
            catch (Exception ex)
            {
                throw new OcrException($"No se pudo rasterizar la página {i + 1} del PDF.", ex);
            }

            var engine = RentEngine(ct);
            try
            {
                var recognized = engine.Recognize(png, ct);
                var first = recognized.Count > 0 ? recognized[0] : new OcrTextResult(string.Empty, 0f);
                pages.Add(new OcrPage
                {
                    PageNumber = i + 1,
                    Text = first.Text,
                    Confidence = first.Confidence,
                    FromEmbeddedText = false
                });
                anyOcr = true;
            }
            finally
            {
                ReturnEngine(engine);
            }
        }

        var kind = anyOcr && anyText ? OcrSourceKind.Mixed
                 : anyOcr ? OcrSourceKind.PdfOcr
                 : OcrSourceKind.PdfText;
        return (pages, kind);
    }

    private static OcrResult BuildResult(List<OcrPage> pages, OcrSourceKind kind, TimeSpan duration, string? fileName)
    {
        var fullText = string.Join(Environment.NewLine + Environment.NewLine, pages.Select(p => p.Text));

        var ocrPages = pages.Where(p => !p.FromEmbeddedText).ToList();
        var meanConfidence = ocrPages.Count > 0 ? ocrPages.Average(p => p.Confidence) : -1f;

        return new OcrResult
        {
            Text = fullText,
            Pages = pages,
            MeanConfidence = meanConfidence,
            SourceKind = kind,
            Duration = duration,
            FileName = fileName
        };
    }

    private static int CountNonWhitespace(string text)
    {
        var count = 0;
        foreach (var c in text)
            if (!char.IsWhiteSpace(c)) count++;
        return count;
    }

    // ------------------------------------------------------------------ Pool de motores (thread-safe)

    private IOcrEngine RentEngine(CancellationToken ct)
    {
        _gate.Wait(ct);
        try
        {
            return _pool.TryTake(out var engine) ? engine : _engineFactory();
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    private void ReturnEngine(IOcrEngine engine)
    {
        _pool.Add(engine);
        _gate.Release();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_pool.TryTake(out var engine))
            engine.Dispose();

        _gate.Dispose();
    }
}
