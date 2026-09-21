using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(4) Mensaje SMS. Al escanear, abre el SMS con el numero y el texto ya rellenos.</summary>
public sealed class ContenidoSms : ContenidoQR
{
    /// <summary>Numero destinatario.</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Texto del mensaje (opcional).</summary>
    public string Mensaje { get; set; } = string.Empty;

    public ContenidoSms() { }

    public ContenidoSms(string numero, string mensaje = "")
    {
        Numero = numero;
        Mensaje = mensaje;
    }

    public override TipoQR Tipo => TipoQR.MensajeSms;

    public override string ConstruirPayload()
        => new PayloadGenerator.SMS(Exigir(Numero, nameof(Numero)), Mensaje ?? string.Empty).ToString();
}
