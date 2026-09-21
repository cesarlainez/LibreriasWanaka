using System.Globalization;
using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(9) Ubicacion GPS. Al escanear, abre las coordenadas en la app de mapas.</summary>
public sealed class ContenidoUbicacion : ContenidoQR
{
    /// <summary>Latitud en grados decimales (-90 a 90).</summary>
    public double Latitud { get; set; }

    /// <summary>Longitud en grados decimales (-180 a 180).</summary>
    public double Longitud { get; set; }

    public ContenidoUbicacion() { }

    public ContenidoUbicacion(double latitud, double longitud)
    {
        Latitud = latitud;
        Longitud = longitud;
    }

    public override TipoQR Tipo => TipoQR.UbicacionGps;

    public override string ConstruirPayload()
    {
        if (Latitud is < -90 or > 90)
            throw new QrContenidoInvalidoException("La latitud debe estar entre -90 y 90.");
        if (Longitud is < -180 or > 180)
            throw new QrContenidoInvalidoException("La longitud debe estar entre -180 y 180.");

        // Formato invariante para que el punto decimal no dependa de la cultura del servidor.
        var lat = Latitud.ToString("0.######", CultureInfo.InvariantCulture);
        var lng = Longitud.ToString("0.######", CultureInfo.InvariantCulture);
        return new PayloadGenerator.Geolocation(lat, lng).ToString();
    }
}
