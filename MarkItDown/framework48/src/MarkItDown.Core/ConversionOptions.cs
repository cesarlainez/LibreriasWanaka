namespace MarkItDown.Core;

/// <summary>Opciones que controlan cómo se genera el Markdown.</summary>
public sealed class ConversionOptions
{
    /// <summary>
    /// Si es true, las imágenes incrustadas (Word) se extraen como archivos en
    /// <see cref="ImageOutputDirectory"/> y se referencian con sintaxis <c>![alt](ruta)</c>.
    /// Si es false (predeterminado), las imágenes se omiten y se agrega una advertencia.
    /// </summary>
    public bool ExtractImages { get; set; }

    /// <summary>Carpeta donde se guardan las imágenes extraídas. Obligatoria si <see cref="ExtractImages"/> es true.</summary>
    public string? ImageOutputDirectory { get; set; }

    /// <summary>
    /// Prefijo para los enlaces de imagen en el Markdown (ej. "imagenes/").
    /// Si está vacío se usa solo el nombre del archivo.
    /// </summary>
    public string ImageLinkPrefix { get; set; } = string.Empty;

    /// <summary>Si es true, en PDF se inserta un comentario <c>&lt;!-- Página N --&gt;</c> al inicio de cada página.</summary>
    public bool IncludePageMarkers { get; set; }

    /// <summary>
    /// Si es true (predeterminado), en PDF se intenta detectar títulos por tamaño de fuente
    /// y convertirlos en encabezados Markdown. Si es false, todo el texto se emite como párrafos.
    /// </summary>
    public bool DetectPdfHeadings { get; set; } = true;
}
