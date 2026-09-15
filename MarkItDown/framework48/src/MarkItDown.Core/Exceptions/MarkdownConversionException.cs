namespace MarkItDown.Core;

/// <summary>Error al convertir un documento a Markdown (archivo dañado, ilegible o protegido).</summary>
public class MarkdownConversionException : Exception
{
    public MarkdownConversionException(string message) : base(message)
    {
    }

    public MarkdownConversionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
