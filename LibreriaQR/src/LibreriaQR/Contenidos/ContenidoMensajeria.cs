using System.Text;
using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>
/// (12) Enlace directo a una app de mensajeria (WhatsApp o Telegram). Al escanear, abre el chat.
/// </summary>
public sealed class ContenidoMensajeria : ContenidoQR
{
    /// <summary>App a la que apunta el QR.</summary>
    public AppMensajeria App { get; set; } = AppMensajeria.WhatsApp;

    /// <summary>
    /// Para WhatsApp: el numero con codigo de pais (solo digitos, sin '+'), p. ej. <c>50322500000</c>.
    /// Para Telegram: el nombre de usuario (con o sin '@'), p. ej. <c>acsa_sv</c>.
    /// </summary>
    public string Destino { get; set; } = string.Empty;

    /// <summary>Mensaje a precargar (opcional; WhatsApp lo soporta, Telegram lo ignora).</summary>
    public string Mensaje { get; set; } = string.Empty;

    public ContenidoMensajeria() { }

    public ContenidoMensajeria(AppMensajeria app, string destino, string mensaje = "")
    {
        App = app;
        Destino = destino;
        Mensaje = mensaje;
    }

    public override TipoQR Tipo => TipoQR.MensajeriaApp;

    public override string ConstruirPayload()
    {
        var destino = Exigir(Destino, nameof(Destino));

        if (App == AppMensajeria.WhatsApp)
        {
            // wa.me exige solo digitos (sin '+', espacios ni guiones).
            var digitos = new string(destino.Where(char.IsDigit).ToArray());
            if (digitos.Length == 0)
                throw new QrContenidoInvalidoException("El numero de WhatsApp no contiene digitos.");
            return new PayloadGenerator.WhatsAppMessage(digitos, Mensaje ?? string.Empty).ToString();
        }

        // Telegram: https://t.me/<usuario>  (se quita el '@' inicial si viene)
        var usuario = destino.TrimStart('@');
        var url = new StringBuilder("https://t.me/").Append(usuario);
        return url.ToString();
    }
}
