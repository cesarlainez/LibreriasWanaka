namespace LibreriaQR;

/// <summary>Error general de la libreria de generacion de QR.</summary>
public class QrException : Exception
{
    public QrException(string message) : base(message) { }

    public QrException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Se lanza cuando el contenido de un QR no es valido (por ejemplo, una URL vacia,
/// una WiFi con cifrado pero sin contrasena, o coordenadas fuera de rango).
/// </summary>
public sealed class QrContenidoInvalidoException : QrException
{
    public QrContenidoInvalidoException(string message) : base(message) { }
}

/// <summary>Se lanza cuando el logo no se puede leer o decodificar como imagen.</summary>
public sealed class QrLogoInvalidoException : QrException
{
    public QrLogoInvalidoException(string message) : base(message) { }

    public QrLogoInvalidoException(string message, Exception innerException) : base(message, innerException) { }
}
