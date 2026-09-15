namespace LibreriaOCR;

/// <summary>
/// Motor de OCR de bajo nivel: recibe una imagen ya rasterizada y devuelve el texto.
/// Implementar esta interfaz permite cambiar de motor (por ejemplo, Tesseract → PaddleOCR)
/// sin modificar <see cref="OcrService"/>. Una instancia no tiene por qué ser thread-safe.
/// </summary>
public interface IOcrEngine : IDisposable
{
    /// <summary>
    /// Reconoce texto en una imagen codificada (PNG, JPG o TIFF).
    /// Si la imagen es un TIFF multipágina, devuelve un resultado por página;
    /// en caso contrario, devuelve un único resultado.
    /// </summary>
    IReadOnlyList<OcrTextResult> Recognize(byte[] imageBytes, CancellationToken cancellationToken = default);
}

/// <summary>Texto reconocido en una imagen junto con su confianza media (0–100).</summary>
/// <param name="Text">Texto reconocido.</param>
/// <param name="Confidence">Confianza media en porcentaje (0–100).</param>
public readonly record struct OcrTextResult(string Text, float Confidence);
