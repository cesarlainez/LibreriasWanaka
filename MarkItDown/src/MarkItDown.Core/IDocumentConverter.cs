namespace MarkItDown.Core;

/// <summary>
/// Contrato que implementa todo convertidor de documentos a Markdown.
/// Cada implementación soporta una o más extensiones de archivo.
/// </summary>
public interface IDocumentConverter
{
    /// <summary>Extensiones soportadas, en minúsculas y con punto (ej. ".docx").</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Indica si este convertidor puede procesar la extensión dada (con o sin punto, sin distinguir mayúsculas).</summary>
    bool CanConvert(string extension);

    /// <summary>
    /// Convierte el contenido del stream a Markdown.
    /// El stream debe estar posicionado al inicio del documento; no se cierra al terminar.
    /// </summary>
    /// <exception cref="MarkdownConversionException">Si el documento no puede leerse o está dañado.</exception>
    ConversionResult Convert(Stream input, ConversionOptions? options = null);
}
