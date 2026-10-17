namespace LibreriaOCR;

/// <summary>Opciones para la detección heurística de escritura a mano (sin IA).</summary>
public sealed class HandwritingOptions
{
    /// <summary>
    /// Umbral (0–100) a partir del cual una región se marca como manuscrita.
    /// Es el parámetro que conviene calibrar con documentos reales. Por defecto 55.
    /// </summary>
    public float ScoreThreshold { get; set; } = 55f;

    /// <summary>DPI al rasterizar páginas de PDF para el análisis. 150 suele bastar. Por defecto 150.</summary>
    public int PdfDpi { get; set; } = 150;

    /// <summary>Lado máximo (px) al que se reduce la imagen para acelerar el análisis. Por defecto 1600.</summary>
    public int MaxImageSide { get; set; } = 1600;

    /// <summary>
    /// Mínimo de componentes (letras/trazos) en una "palabra" para evaluarla. Grupos más pequeños se ignoran
    /// porque sus métricas son poco fiables. Por defecto 6.
    /// </summary>
    public int MinComponentsPerLine { get; set; } = 6;

    /// <summary>
    /// Mínimo de regiones manuscritas para considerar que el documento tiene escritura a mano (a).
    /// Por defecto 1.
    /// </summary>
    public int MinRegionsForDocument { get; set; } = 1;

    /// <summary>
    /// Idioma(s) de Tesseract usados por la corroboración con OCR (ver <see cref="CorroborateWithOcr"/>).
    /// Por defecto "spa".
    /// </summary>
    public string Languages { get; set; } = "spa";

    /// <summary>
    /// Si es true (por defecto), además de las palabras se buscan firmas y texto cursivo como trazos
    /// conectados grandes. La geometría por sí sola confunde firmas con logos/gráficos, por eso conviene
    /// mantener activada la corroboración con OCR (<see cref="CorroborateWithOcr"/>).
    /// </summary>
    public bool DetectCursiveStrokes { get; set; } = true;

    /// <summary>
    /// Si es true (por defecto), cuando la geometría encuentra trazos candidatos se ejecuta OCR en esa
    /// página para confirmar: la escritura a mano produce texto ilegible (confianza baja), mientras que un
    /// documento impreso con un logo mantiene la confianza alta. Así se descartan los logos/gráficos.
    /// Requiere tessdata; si el OCR no está disponible, se conserva el resultado solo por geometría.
    /// </summary>
    public bool CorroborateWithOcr { get; set; } = true;

    /// <summary>
    /// Confianza de OCR (0–100) por debajo de la cual una página con trazos candidatos se considera
    /// manuscrita. Por encima, se asume texto impreso (los trazos eran logos/gráficos). Por defecto 78.
    /// </summary>
    public float MaxOcrConfidenceForHandwriting { get; set; } = 78f;

    /// <summary>
    /// Confianza de OCR (0–100) por debajo de la cual una palabra individual se marca como manuscrita
    /// (para delimitar las regiones/líneas escritas a mano). Por defecto 55.
    /// </summary>
    public float WordHandwritingConfidence { get; set; } = 55f;

    /// <summary>
    /// Si es true, cada región incluye en <see cref="HandwritingRegion.Diagnostics"/> los valores de las
    /// métricas usadas (para calibrar el umbral). Por defecto false.
    /// </summary>
    public bool IncludeDiagnostics { get; set; }
}
