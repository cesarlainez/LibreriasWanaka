namespace LibreriaOCR;

/// <summary>Opciones de configuración del OCR.</summary>
public sealed class OcrOptions
{
    /// <summary>
    /// Idioma(s) de Tesseract. Por defecto "spa" (español).
    /// Se pueden combinar con '+', por ejemplo "spa+eng".
    /// Debe existir el archivo "&lt;código&gt;.traineddata" en la carpeta tessdata.
    /// </summary>
    public string Languages { get; set; } = "spa";

    /// <summary>
    /// Ruta a la carpeta 'tessdata' con los archivos .traineddata.
    /// Si es null, se usa la carpeta 'tessdata' ubicada junto al ensamblado.
    /// </summary>
    public string? TessDataPath { get; set; }

    /// <summary>Resolución (DPI) al rasterizar páginas de un PDF escaneado. Por defecto 300.</summary>
    public int PdfDpi { get; set; } = 300;

    /// <summary>
    /// Si es true (por defecto), en los PDF se extrae el texto digital ya presente
    /// y solo se hace OCR de las páginas que son imágenes escaneadas.
    /// </summary>
    public bool PreferEmbeddedPdfText { get; set; } = true;

    /// <summary>
    /// Mínimo de caracteres (sin contar espacios) que debe tener el texto digital de una
    /// página de PDF para aceptarlo sin OCR. Por debajo de ese valor, la página se procesa con OCR.
    /// </summary>
    public int MinEmbeddedCharsPerPage { get; set; } = 16;

    /// <summary>
    /// Máximo de motores de OCR simultáneos (grado de paralelismo).
    /// Si es null, se usa el número de procesadores. El OCR es intensivo en CPU.
    /// </summary>
    public int? MaxConcurrency { get; set; }

    /// <summary>
    /// Umbral (en píxeles: ancho×alto) por debajo del cual una imagen incrustada NO se pasa
    /// por OCR al usar <see cref="OcrService.RecognizeEmbeddedImages(byte[], System.Threading.CancellationToken)"/>.
    /// Por defecto 40.000 (aprox. 200×200): filtra íconos, viñetas y logotipos pequeños donde
    /// Tesseract solo produce ruido.
    /// </summary>
    public int MinEmbeddedImagePixels { get; set; } = 40_000;
}
