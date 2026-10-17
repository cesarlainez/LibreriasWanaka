namespace LibreriaOCR;

/// <summary>Una región de la imagen marcada como probable escritura a mano.</summary>
public sealed class HandwritingRegion
{
    /// <summary>Número de página (1-based).</summary>
    public int PageNumber { get; init; }

    /// <summary>Coordenadas de la caja en píxeles de la imagen original.</summary>
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Puntaje de "manuscrito" (0–100). A mayor valor, más probable.</summary>
    public float Score { get; init; }

    /// <summary>Detalle de las métricas (solo si se activó <see cref="HandwritingOptions.IncludeDiagnostics"/>).</summary>
    public string? Diagnostics { get; init; }
}

/// <summary>Resultado de la detección de manuscrito para una página.</summary>
public sealed class HandwritingPage
{
    public int PageNumber { get; init; }

    /// <summary>True si la página tiene al menos una región manuscrita.</summary>
    public bool HasHandwriting { get; init; }

    /// <summary>Puntaje máximo encontrado en la página (0–100).</summary>
    public float MaxScore { get; init; }

    /// <summary>
    /// Confianza de OCR de la página (0–100) si se corroboró con OCR; -1 si no se calculó.
    /// </summary>
    public float OcrConfidence { get; init; } = -1f;

    /// <summary>Regiones manuscritas detectadas en la página.</summary>
    public IReadOnlyList<HandwritingRegion> Regions { get; init; } = Array.Empty<HandwritingRegion>();
}

/// <summary>Resultado completo de la detección de escritura a mano en un documento.</summary>
public sealed class HandwritingResult
{
    /// <summary>(a) True si el documento contiene escritura a mano.</summary>
    public bool HasHandwriting { get; init; }

    /// <summary>Puntaje máximo encontrado en todo el documento (0–100).</summary>
    public float MaxScore { get; init; }

    /// <summary>Resultado por página.</summary>
    public IReadOnlyList<HandwritingPage> Pages { get; init; } = Array.Empty<HandwritingPage>();

    /// <summary>(b) Todas las regiones manuscritas del documento (de todas las páginas).</summary>
    public IReadOnlyList<HandwritingRegion> Regions => Pages.SelectMany(p => p.Regions).ToList();

    /// <summary>Nombre del archivo, si se conoce.</summary>
    public string? FileName { get; init; }

    /// <summary>Tiempo total del análisis.</summary>
    public TimeSpan Duration { get; init; }
}
