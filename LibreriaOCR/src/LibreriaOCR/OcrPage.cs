namespace LibreriaOCR;

/// <summary>Resultado del OCR (o extracción de texto) de una sola página.</summary>
public sealed class OcrPage
{
    /// <summary>Número de página, empezando en 1.</summary>
    public int PageNumber { get; init; }

    /// <summary>Texto reconocido en la página.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Confianza media del OCR para la página, en porcentaje (0–100).
    /// Vale -1 cuando el texto se extrajo de la capa digital del PDF (sin OCR).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>True si el texto provino de la capa digital del PDF en lugar de OCR.</summary>
    public bool FromEmbeddedText { get; init; }
}
