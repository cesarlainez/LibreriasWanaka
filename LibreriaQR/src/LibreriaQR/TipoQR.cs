namespace LibreriaQR;

/// <summary>
/// Los 13 tipos de contenido que un lector de QR interpreta y ejecuta de forma nativa
/// (abrir el navegador, marcar un telefono, conectar a una WiFi, etc.).
/// Cada valor tiene su clase de contenido correspondiente en el namespace
/// <c>LibreriaQR.Contenidos</c>.
/// </summary>
public enum TipoQR
{
    /// <summary>Enlace web (URL). Contenido: <see cref="Contenidos.ContenidoUrl"/>.</summary>
    EnlaceWeb = 1,

    /// <summary>Texto plano. Contenido: <see cref="Contenidos.ContenidoTexto"/>.</summary>
    TextoPlano = 2,

    /// <summary>Llamada telefonica (<c>tel:</c>). Contenido: <see cref="Contenidos.ContenidoTelefono"/>.</summary>
    LlamadaTelefonica = 3,

    /// <summary>Mensaje SMS. Contenido: <see cref="Contenidos.ContenidoSms"/>.</summary>
    MensajeSms = 4,

    /// <summary>Red Wi-Fi. Contenido: <see cref="Contenidos.ContenidoWifi"/>.</summary>
    RedWifi = 5,

    /// <summary>Tarjeta de contacto completa (vCard). Contenido: <see cref="Contenidos.ContenidoVCard"/>.</summary>
    TarjetaVCard = 6,

    /// <summary>Tarjeta de contacto simple (MECARD). Contenido: <see cref="Contenidos.ContenidoMeCard"/>.</summary>
    TarjetaMeCard = 7,

    /// <summary>Correo electronico (<c>mailto:</c>). Contenido: <see cref="Contenidos.ContenidoCorreo"/>.</summary>
    CorreoElectronico = 8,

    /// <summary>Ubicacion GPS (coordenadas / mapas). Contenido: <see cref="Contenidos.ContenidoUbicacion"/>.</summary>
    UbicacionGps = 9,

    /// <summary>Evento de calendario (iCalendar / VEVENT). Contenido: <see cref="Contenidos.ContenidoEvento"/>.</summary>
    EventoCalendario = 10,

    /// <summary>Codigo de autenticacion 2FA (otpauth). Contenido: <see cref="Contenidos.ContenidoAutenticacion2Fa"/>.</summary>
    Autenticacion2Fa = 11,

    /// <summary>Enlace directo a app de mensajeria (WhatsApp / Telegram). Contenido: <see cref="Contenidos.ContenidoMensajeria"/>.</summary>
    MensajeriaApp = 12,

    /// <summary>Direccion de criptomoneda (Bitcoin, Ethereum, etc.). Contenido: <see cref="Contenidos.ContenidoCripto"/>.</summary>
    Criptomoneda = 13,
}
