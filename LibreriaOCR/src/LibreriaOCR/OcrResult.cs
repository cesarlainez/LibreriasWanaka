namespace LibreriaOCR;

/// <summary>Resultado completo del OCR de un documento.</summary>
public sealed class OcrResult
{
    /// <summary>Texto completo del documento (todas las páginas unidas).</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Resultado desglosado por página.</summary>
    public IReadOnlyList<OcrPage> Pages { get; init; } = Array.Empty<OcrPage>();

    /// <summary>
    /// Confianza media (0–100) sobre las páginas procesadas con OCR.
    /// Vale -1 si ninguna página necesitó OCR (por ejemplo, un PDF totalmente digital).
    /// </summary>
    public float MeanConfidence { get; init; }

    /// <summary>Origen del texto (imagen, PDF digital, PDF escaneado o mixto).</summary>
    public OcrSourceKind SourceKind { get; init; }

    /// <summary>Cantidad de páginas del documento.</summary>
    public int PageCount => Pages.Count;

    /// <summary>Tiempo total de procesamiento.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Nombre del archivo de origen, si se conoce.</summary>
    public string? FileName { get; init; }

    /// <summary>Devuelve el texto completo del documento.</summary>
    public override string ToString() => Text;
}
