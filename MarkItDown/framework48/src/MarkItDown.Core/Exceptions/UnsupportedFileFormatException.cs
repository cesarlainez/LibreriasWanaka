namespace MarkItDown.Core;

/// <summary>Se lanza cuando ningún convertidor registrado soporta la extensión del archivo.</summary>
public sealed class UnsupportedFileFormatException : MarkdownConversionException
{
    public UnsupportedFileFormatException(string extension, string message) : base(message)
    {
        Extension = extension;
    }

    /// <summary>Extensión que no pudo procesarse (normalizada, ej. ".xyz").</summary>
    public string Extension { get; }
}
