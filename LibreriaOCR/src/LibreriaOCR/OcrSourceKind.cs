namespace LibreriaOCR;

/// <summary>Indica de dónde provino el texto devuelto.</summary>
public enum OcrSourceKind
{
    /// <summary>Imagen (PNG, JPG o TIFF) procesada con OCR.</summary>
    Image,

    /// <summary>PDF con capa de texto digital; el texto se extrajo sin OCR.</summary>
    PdfText,

    /// <summary>PDF escaneado; todas las páginas se procesaron con OCR.</summary>
    PdfOcr,

    /// <summary>PDF mixto: algunas páginas con texto digital y otras con OCR.</summary>
    Mixed
}
