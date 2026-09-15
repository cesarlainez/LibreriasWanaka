namespace LibreriaOCR;

/// <summary>Error general de la librería de OCR.</summary>
public class OcrException : Exception
{
    public OcrException(string message) : base(message) { }

    public OcrException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Se lanza cuando el formato del archivo no está soportado.</summary>
public sealed class FormatoNoSoportadoException : OcrException
{
    public FormatoNoSoportadoException(string message) : base(message) { }
}
