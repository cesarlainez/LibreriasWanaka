using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>
/// (11) Codigo de autenticacion en dos pasos (2FA / <c>otpauth://</c>). Es el QR que muestran
/// los servicios para vincular Google Authenticator, Microsoft Authenticator, Authy, etc.
/// </summary>
public sealed class ContenidoAutenticacion2Fa : ContenidoQR
{
    /// <summary>Secreto compartido, codificado en Base32 (lo entrega el servicio que activa el 2FA).</summary>
    public string Secreto { get; set; } = string.Empty;

    /// <summary>Emisor / nombre del servicio (p. ej. "ACSA"). Aparece en la app de autenticacion.</summary>
    public string Emisor { get; set; } = string.Empty;

    /// <summary>Cuenta / etiqueta (p. ej. el correo o usuario).</summary>
    public string Cuenta { get; set; } = string.Empty;

    /// <summary>Tipo de codigo. Por defecto TOTP (basado en tiempo).</summary>
    public Tipo2Fa Tipo2Fa { get; set; } = Tipo2Fa.Totp;

    /// <summary>Algoritmo hash. Por defecto SHA1 (el asumido por casi todas las apps).</summary>
    public Algoritmo2Fa Algoritmo { get; set; } = Algoritmo2Fa.Sha1;

    /// <summary>Numero de digitos del codigo. Por defecto 6.</summary>
    public int Digitos { get; set; } = 6;

    /// <summary>Periodo en segundos (solo TOTP). Por defecto 30.</summary>
    public int PeriodoSegundos { get; set; } = 30;

    /// <summary>Contador inicial (solo HOTP). Por defecto 0.</summary>
    public int Contador { get; set; }

    public override TipoQR Tipo => TipoQR.Autenticacion2Fa;

    public override string ConstruirPayload()
    {
        Exigir(Secreto, nameof(Secreto));
        if (string.IsNullOrWhiteSpace(Emisor) && string.IsNullOrWhiteSpace(Cuenta))
            throw new QrContenidoInvalidoException("Se requiere al menos Emisor o Cuenta.");

        var otp = new PayloadGenerator.OneTimePassword
        {
            Secret = Secreto.Trim(),
            Issuer = string.IsNullOrWhiteSpace(Emisor) ? null : Emisor.Trim(),
            Label = string.IsNullOrWhiteSpace(Cuenta) ? null : Cuenta.Trim(),
            Digits = Digitos,
            Type = Tipo2Fa == Tipo2Fa.Hotp
                ? PayloadGenerator.OneTimePassword.OneTimePasswordAuthType.HOTP
                : PayloadGenerator.OneTimePassword.OneTimePasswordAuthType.TOTP,
            AuthAlgorithm = Algoritmo switch
            {
                Algoritmo2Fa.Sha256 => PayloadGenerator.OneTimePassword.OneTimePasswordAuthAlgorithm.SHA256,
                Algoritmo2Fa.Sha512 => PayloadGenerator.OneTimePassword.OneTimePasswordAuthAlgorithm.SHA512,
                _ => PayloadGenerator.OneTimePassword.OneTimePasswordAuthAlgorithm.SHA1,
            },
        };

        if (Tipo2Fa == Tipo2Fa.Hotp)
            otp.Counter = Contador;
        else
            otp.Period = PeriodoSegundos;

        return otp.ToString();
    }
}
