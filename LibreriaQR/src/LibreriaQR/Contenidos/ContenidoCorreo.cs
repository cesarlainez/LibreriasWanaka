using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(8) Correo electronico (mailto). Al escanear, abre un correo nuevo ya rellenado.</summary>
public sealed class ContenidoCorreo : ContenidoQR
{
    /// <summary>Direccion destinataria.</summary>
    public string Destinatario { get; set; } = string.Empty;

    /// <summary>Asunto (opcional).</summary>
    public string Asunto { get; set; } = string.Empty;

    /// <summary>Cuerpo del mensaje (opcional).</summary>
    public string Cuerpo { get; set; } = string.Empty;

    public ContenidoCorreo() { }

    public ContenidoCorreo(string destinatario, string asunto = "", string cuerpo = "")
    {
        Destinatario = destinatario;
        Asunto = asunto;
        Cuerpo = cuerpo;
    }

    public override TipoQR Tipo => TipoQR.CorreoElectronico;

    public override string ConstruirPayload()
        => new PayloadGenerator.Mail(
            Exigir(Destinatario, nameof(Destinatario)),
            Asunto ?? string.Empty,
            Cuerpo ?? string.Empty).ToString();
}
