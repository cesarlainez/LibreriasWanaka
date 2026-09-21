using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(5) Red Wi-Fi. Al escanear, ofrece conectarse a la red sin teclear la clave.</summary>
public sealed class ContenidoWifi : ContenidoQR
{
    /// <summary>Nombre de la red (SSID).</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>Contrasena. Obligatoria salvo que <see cref="Cifrado"/> sea <see cref="TipoCifradoWifi.SinCifrado"/>.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Tipo de cifrado de la red. Por defecto WPA/WPA2.</summary>
    public TipoCifradoWifi Cifrado { get; set; } = TipoCifradoWifi.WPA;

    /// <summary>Marca la red como oculta (SSID no difundido). Por defecto false.</summary>
    public bool Oculta { get; set; }

    public ContenidoWifi() { }

    public ContenidoWifi(string ssid, string password, TipoCifradoWifi cifrado = TipoCifradoWifi.WPA, bool oculta = false)
    {
        Ssid = ssid;
        Password = password;
        Cifrado = cifrado;
        Oculta = oculta;
    }

    public override TipoQR Tipo => TipoQR.RedWifi;

    public override string ConstruirPayload()
    {
        var ssid = Exigir(Ssid, nameof(Ssid));

        var auth = Cifrado switch
        {
            TipoCifradoWifi.WEP => PayloadGenerator.WiFi.Authentication.WEP,
            TipoCifradoWifi.SinCifrado => PayloadGenerator.WiFi.Authentication.nopass,
            _ => PayloadGenerator.WiFi.Authentication.WPA,
        };

        if (Cifrado != TipoCifradoWifi.SinCifrado && string.IsNullOrEmpty(Password))
            throw new QrContenidoInvalidoException("La red tiene cifrado pero no se indico contrasena.");

        return new PayloadGenerator.WiFi(ssid, Password ?? string.Empty, auth, Oculta).ToString();
    }
}
