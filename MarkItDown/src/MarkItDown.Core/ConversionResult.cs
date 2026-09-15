namespace MarkItDown.Core;

/// <summary>Resultado de una conversión a Markdown.</summary>
public sealed class ConversionResult
{
    public ConversionResult(string markdown, string? title = null, IReadOnlyList<string>? warnings = null)
    {
        Markdown = markdown ?? string.Empty;
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        Warnings = warnings ?? Array.Empty<string>();
    }

    /// <summary>Contenido convertido a Markdown (GitHub Flavored Markdown).</summary>
    public string Markdown { get; }

    /// <summary>Título del documento (metadatos o primer encabezado), si se pudo determinar.</summary>
    public string? Title { get; }

    /// <summary>Advertencias no fatales generadas durante la conversión (ej. imágenes omitidas).</summary>
    public IReadOnlyList<string> Warnings { get; }
}
