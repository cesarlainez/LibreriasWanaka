using System;
using LibreriaQR.Contenidos;
using LibreriaQR.Internal;
using QRCoder;

namespace LibreriaQR;

/// <summary>
/// Servicio de alto nivel para generar codigos QR. Recibe un <see cref="ContenidoQR"/>
/// (URL, WiFi, vCard, etc.) y unas <see cref="OpcionesQR"/>, y devuelve un <see cref="ResultadoQR"/>
/// con el PNG en bytes, en Base64 y como data URI.
/// <para>
/// No guarda estado: una sola instancia es segura para usarse desde varios hilos
/// (por ejemplo, como singleton en una app web). Tambien hay atajos estaticos
/// (<see cref="Crear(ContenidoQR, OpcionesQR?)"/>).
/// </para>
/// </summary>
public sealed class GeneradorQR
{
    // ------------------------------------------------------------------ API principal

    /// <summary>Genera el QR de cualquier contenido.</summary>
    public ResultadoQR Generar(ContenidoQR contenido, OpcionesQR? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        opciones ??= new OpcionesQR();

        var payload = contenido.ConstruirPayload();
        var nivel = ResolverNivel(contenido, opciones);

        using var generador = new QRCodeGenerator();
        var datos = generador.CreateQrCode(payload, AEccLevel(nivel));

        var modulos = ExtraerModulos(datos, opciones.IncluirZonaQuieta);
        var (png, lado) = RenderizadorQR.Render(modulos, opciones);

        var base64 = Convert.ToBase64String(png);
        return new ResultadoQR
        {
            Png = png,
            Base64 = base64,
            DataUri = "data:image/png;base64," + base64,
            Tipo = contenido.Tipo,
            Payload = payload,
            AnchoPx = lado,
            AltoPx = lado,
            Correccion = nivel,
            TieneLogo = opciones.Logo is not null,
        };
    }

    /// <summary>Atajo estatico: crea un generador de un solo uso y genera el QR.</summary>
    public static ResultadoQR Crear(ContenidoQR contenido, OpcionesQR? opciones = null)
        => new GeneradorQR().Generar(contenido, opciones);

    // ------------------------------------------------------------------ Atajos por tipo

    /// <summary>(1) QR de enlace web.</summary>
    public ResultadoQR DesdeUrl(string url, OpcionesQR? opciones = null)
        => Generar(new ContenidoUrl(url), opciones);

    /// <summary>(2) QR de texto plano.</summary>
    public ResultadoQR DesdeTexto(string texto, OpcionesQR? opciones = null)
        => Generar(new ContenidoTexto(texto), opciones);

    /// <summary>(3) QR de llamada telefonica.</summary>
    public ResultadoQR DesdeTelefono(string numero, OpcionesQR? opciones = null)
        => Generar(new ContenidoTelefono(numero), opciones);

    /// <summary>(4) QR de mensaje SMS.</summary>
    public ResultadoQR DesdeSms(string numero, string mensaje = "", OpcionesQR? opciones = null)
        => Generar(new ContenidoSms(numero, mensaje), opciones);

    /// <summary>(5) QR de red Wi-Fi.</summary>
    public ResultadoQR DesdeWifi(string ssid, string password, TipoCifradoWifi cifrado = TipoCifradoWifi.WPA, bool oculta = false, OpcionesQR? opciones = null)
        => Generar(new ContenidoWifi(ssid, password, cifrado, oculta), opciones);

    /// <summary>(8) QR de correo electronico.</summary>
    public ResultadoQR DesdeCorreo(string destinatario, string asunto = "", string cuerpo = "", OpcionesQR? opciones = null)
        => Generar(new ContenidoCorreo(destinatario, asunto, cuerpo), opciones);

    /// <summary>(9) QR de ubicacion GPS.</summary>
    public ResultadoQR DesdeUbicacion(double latitud, double longitud, OpcionesQR? opciones = null)
        => Generar(new ContenidoUbicacion(latitud, longitud), opciones);

    /// <summary>(12) QR de app de mensajeria (WhatsApp / Telegram).</summary>
    public ResultadoQR DesdeMensajeria(AppMensajeria app, string destino, string mensaje = "", OpcionesQR? opciones = null)
        => Generar(new ContenidoMensajeria(app, destino, mensaje), opciones);

    // Los tipos con muchos campos (vCard, MeCard, evento, 2FA, cripto) se generan armando
    // su objeto de contenido y llamando a Generar(contenido, opciones).

    // ------------------------------------------------------------------ Interno

    private static NivelCorreccion ResolverNivel(ContenidoQR contenido, OpcionesQR opciones)
    {
        var nivel = opciones.Correccion
            ?? (opciones.Logo is not null ? NivelCorreccion.Maximo : contenido.CorreccionRecomendada);

        // Con logo, nunca por debajo de Alto: el logo tapa modulos y hay que poder recuperarlos.
        if (opciones.Logo is not null && nivel < NivelCorreccion.Alto)
            nivel = NivelCorreccion.Maximo;

        return nivel;
    }

    private static QRCodeGenerator.ECCLevel AEccLevel(NivelCorreccion nivel) => nivel switch
    {
        NivelCorreccion.Bajo => QRCodeGenerator.ECCLevel.L,
        NivelCorreccion.Alto => QRCodeGenerator.ECCLevel.Q,
        NivelCorreccion.Maximo => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.M,
    };

    private static bool[][] ExtraerModulos(QRCodeData datos, bool incluirZonaQuieta)
    {
        var matriz = datos.ModuleMatrix; // incluye una zona quieta de 4 modulos por lado
        var tamano = matriz.Count;

        // El estandar pide 4 modulos de margen; QRCoder ya los mete en ModuleMatrix.
        const int zona = 4;
        var recorte = incluirZonaQuieta ? 0 : zona;
        var n = tamano - recorte * 2;
        if (n <= 0)
            n = tamano; // salvaguarda por si algun payload minusculo no tuviese margen

        var modulos = new bool[n][];
        for (var y = 0; y < n; y++)
        {
            var filaOrigen = matriz[y + recorte];
            var fila = new bool[n];
            for (var x = 0; x < n; x++)
                fila[x] = filaOrigen[x + recorte];
            modulos[y] = fila;
        }

        return modulos;
    }
}
