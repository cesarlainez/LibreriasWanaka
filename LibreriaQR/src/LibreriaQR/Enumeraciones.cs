namespace LibreriaQR;

/// <summary>Tipo de cifrado de una red Wi-Fi.</summary>
public enum TipoCifradoWifi
{
    /// <summary>WPA / WPA2 / WPA3 (lo normal hoy).</summary>
    WPA = 0,

    /// <summary>WEP (obsoleto, redes muy antiguas).</summary>
    WEP = 1,

    /// <summary>Red abierta, sin contrasena.</summary>
    SinCifrado = 2,
}

/// <summary>Version del formato de tarjeta de contacto vCard.</summary>
public enum VersionVCard
{
    /// <summary>vCard 2.1 (maxima compatibilidad con lectores viejos).</summary>
    V21 = 0,

    /// <summary>vCard 3.0 (por defecto; la mas soportada).</summary>
    V3 = 1,

    /// <summary>vCard 4.0 (la mas moderna).</summary>
    V4 = 2,
}

/// <summary>App de mensajeria a la que abre el QR.</summary>
public enum AppMensajeria
{
    /// <summary>WhatsApp (<c>https://wa.me/&lt;numero&gt;</c>).</summary>
    WhatsApp = 0,

    /// <summary>Telegram (<c>https://t.me/&lt;usuario&gt;</c>).</summary>
    Telegram = 1,
}

/// <summary>Criptomoneda de la direccion de pago.</summary>
public enum Criptomoneda
{
    /// <summary>Bitcoin (<c>bitcoin:</c>).</summary>
    Bitcoin = 0,

    /// <summary>Bitcoin Cash (<c>bitcoincash:</c>).</summary>
    BitcoinCash = 1,

    /// <summary>Litecoin (<c>litecoin:</c>).</summary>
    Litecoin = 2,

    /// <summary>Ethereum (<c>ethereum:</c>, EIP-681).</summary>
    Ethereum = 3,
}

/// <summary>Tipo de codigo de un solo uso (2FA).</summary>
public enum Tipo2Fa
{
    /// <summary>Basado en tiempo (TOTP). Es el usado por Google Authenticator, Authy, etc.</summary>
    Totp = 0,

    /// <summary>Basado en contador (HOTP).</summary>
    Hotp = 1,
}

/// <summary>Algoritmo hash del codigo 2FA.</summary>
public enum Algoritmo2Fa
{
    /// <summary>SHA1 (por defecto; el que asumen casi todas las apps de autenticacion).</summary>
    Sha1 = 0,

    /// <summary>SHA256.</summary>
    Sha256 = 1,

    /// <summary>SHA512.</summary>
    Sha512 = 2,
}
