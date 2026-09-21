using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(3) Llamada telefonica. Al escanear, prepara la marcacion del numero.</summary>
public sealed class ContenidoTelefono : ContenidoQR
{
    /// <summary>Numero a marcar. Se recomienda formato internacional, p. ej. <c>+50322500000</c>.</summary>
    public string Numero { get; set; } = string.Empty;

    public ContenidoTelefono() { }

    public ContenidoTelefono(string numero) => Numero = numero;

    public override TipoQR Tipo => TipoQR.LlamadaTelefonica;

    public override string ConstruirPayload()
        => new PayloadGenerator.PhoneNumber(Exigir(Numero, nameof(Numero))).ToString();
}
