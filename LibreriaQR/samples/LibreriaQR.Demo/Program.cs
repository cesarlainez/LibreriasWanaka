using LibreriaQR;
using LibreriaQR.Contenidos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Carpeta de salida: primer argumento, o ./qr-salida junto al ejecutable.
var salida = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "qr-salida");
Directory.CreateDirectory(salida);

var gen = new GeneradorQR();

Console.WriteLine("Prueba de mesa de LibreriaQR");
Console.WriteLine("============================");

// Cada caso: nombre, contenido, validador del payload, opciones y nombre de archivo PNG.
var casos = new List<(string nombre, ContenidoQR contenido, Func<string, bool> valida, OpcionesQR? opciones, string archivo)>
{
    ("01 URL",
     new ContenidoUrl("https://acsa.com.sv"),
     p => p.Contains("acsa.com.sv") && p.StartsWith("http"),
     null, "01-url.png"),

    ("02 Texto plano",
     new ContenidoTexto("Hola ACSA"),
     p => p == "Hola ACSA",
     null, "02-texto.png"),

    ("03 Telefono",
     new ContenidoTelefono("+50322500000"),
     p => p == "tel:+50322500000",
     null, "03-telefono.png"),

    ("04 SMS",
     new ContenidoSms("+50322500000", "Consulta de poliza"),
     p => p.Contains("50322500000") && p.Contains("Consulta", StringComparison.OrdinalIgnoreCase),
     null, "04-sms.png"),

    ("05 WiFi",
     new ContenidoWifi("ACSA-Invitados", "Clave1234", TipoCifradoWifi.WPA),
     p => p.StartsWith("WIFI:") && p.Contains("ACSA-Invitados"),
     null, "05-wifi.png"),

    ("06 vCard",
     new ContenidoVCard { Nombre = "Cesar", Apellido = "Lainez", Empresa = "ACSA", Cargo = "TI", Correo = "soporte@acsa.com.sv", Movil = "+50370000000" },
     p => p.Contains("BEGIN:VCARD") && p.Contains("VERSION:3.0") && p.Contains("Lainez"),
     null, "06-vcard.png"),

    ("07 MeCard",
     new ContenidoMeCard { Nombre = "Cesar", Apellido = "Lainez", Telefono = "+50322500000", Correo = "soporte@acsa.com.sv" },
     p => p.StartsWith("MECARD:") && p.EndsWith(";;") && p.Contains("N:Lainez,Cesar"),
     null, "07-mecard.png"),

    ("08 Correo",
     new ContenidoCorreo("soporte@acsa.com.sv", "Soporte", "Necesito ayuda"),
     p => p.StartsWith("mailto:") && p.Contains("soporte@acsa.com.sv"),
     null, "08-correo.png"),

    ("09 Ubicacion GPS",
     new ContenidoUbicacion(13.6989, -89.1914),
     p => p.StartsWith("geo:") && p.Contains("13.6989") && p.Contains("-89.1914"),
     null, "09-ubicacion.png"),

    ("10 Evento",
     new ContenidoEvento { Titulo = "Reunion TI", Ubicacion = "ACSA", Inicio = new DateTime(2026, 10, 1, 9, 0, 0), Fin = new DateTime(2026, 10, 1, 10, 0, 0) },
     p => p.Contains("BEGIN:VEVENT") && p.Contains("Reunion TI"),
     null, "10-evento.png"),

    ("11 2FA (TOTP)",
     new ContenidoAutenticacion2Fa { Secreto = "JBSWY3DPEHPK3PXP", Emisor = "ACSA", Cuenta = "soporte@acsa.com.sv" },
     p => p.StartsWith("otpauth://totp/") && p.Contains("ACSA"),
     null, "11-2fa.png"),

    ("12 WhatsApp",
     new ContenidoMensajeria(AppMensajeria.WhatsApp, "+503 2250-0000", "Hola"),
     p => p.Contains("wa.me/50322500000"),
     null, "12-whatsapp.png"),

    ("12b Telegram",
     new ContenidoMensajeria(AppMensajeria.Telegram, "@acsa_sv"),
     p => p == "https://t.me/acsa_sv",
     null, "12b-telegram.png"),

    ("13 Bitcoin",
     new ContenidoCripto { Moneda = Criptomoneda.Bitcoin, Direccion = "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2", Monto = 0.005m },
     p => p.StartsWith("bitcoin:") && p.Contains("1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2"),
     null, "13-bitcoin.png"),

    ("13b Ethereum",
     new ContenidoCripto { Moneda = Criptomoneda.Ethereum, Direccion = "0x71C7656EC7ab88b098defB751B7401B5f6d8976F", Monto = 1m },
     p => p == "ethereum:0x71C7656EC7ab88b098defB751B7401B5f6d8976F?value=1000000000000000000",
     null, "13b-ethereum.png"),

    // Opciones: color e-e-e (azul ACSA) + tamano objetivo.
    ("Extra color+tamano",
     new ContenidoUrl("https://acsa.com.sv"),
     p => p.Contains("acsa.com.sv"),
     new OpcionesQR { ColorPrimerPlano = "#003A84", TamanoPx = 512 },
     "extra-color.png"),

    // Opciones: logo centrado (fuerza correccion maxima).
    ("Extra logo",
     new ContenidoUrl("https://acsa.com.sv"),
     p => p.Contains("acsa.com.sv"),
     new OpcionesQR { Logo = new OpcionesLogo { Bytes = CrearLogoDePrueba(), Proporcion = 0.24 } },
     "extra-logo.png"),
};

var fallas = 0;
foreach (var (nombre, contenido, valida, opciones, archivo) in casos)
{
    var paso = false;
    string? detalle = null;
    try
    {
        var r = gen.Generar(contenido, opciones);
        var okPayload = valida(r.Payload);
        var okPng = r.Png.Length > 0 && r.DataUri.StartsWith("data:image/png;base64,");
        paso = okPayload && okPng;

        var ruta = Path.Combine(salida, archivo);
        r.GuardarPng(ruta);

        detalle = $"{r.AnchoPx}px, ECC {r.Correccion}, {r.Png.Length:N0} bytes" +
                  (okPayload ? "" : $"  <-- payload inesperado: {Recortar(r.Payload)}");
    }
    catch (Exception ex)
    {
        detalle = ex.Message;
    }

    Console.WriteLine($"  [{(paso ? "PASA " : "FALLA")}] {nombre,-22} {detalle}");
    if (!paso) fallas++;
}

// Caso negativo: contenido invalido debe lanzar.
var lanzo = false;
try { gen.DesdeUrl(""); }
catch (QrContenidoInvalidoException) { lanzo = true; }
Console.WriteLine($"  [{(lanzo ? "PASA " : "FALLA")}] {"URL vacia lanza",-22}");
if (!lanzo) fallas++;

Console.WriteLine();
Console.WriteLine($"Resultado: {casos.Count + 1 - fallas}/{casos.Count + 1} PASA");
Console.WriteLine($"PNG generados en: {salida}");
return fallas == 0 ? 0 : 2;

// ------------------------------------------------------------------ helpers

static byte[] CrearLogoDePrueba()
{
    // Logo de prueba: cuadrado azul ACSA. Sustituyalo por su logotipo real.
    using var img = new Image<Rgba32>(160, 160, Color.ParseHex("003A84").ToPixel<Rgba32>());
    using var ms = new MemoryStream();
    img.SaveAsPng(ms);
    return ms.ToArray();
}

static string Recortar(string s) => s.Length <= 60 ? s : s[..60] + "...";
