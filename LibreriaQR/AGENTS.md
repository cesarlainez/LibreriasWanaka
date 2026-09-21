# LibreriaQR — Guía para agentes IA

> Documento pensado para que un asistente IA (Copilot, Cursor, Claude, GPT, etc.) use la librería sin leer el código fuente. Contiene el API completo, los payloads exactos que produce (verificados con un lector independiente), ejemplos ejecutables y las reglas de comportamiento.

---

## 1. Identidad de la librería

| Campo | Valor |
|---|---|
| Nombre del paquete / assembly | `LibreriaQR` |
| Namespaces | `LibreriaQR` (API) y `LibreriaQR.Contenidos` (clases de contenido) |
| Target framework | `net10.0` |
| Nullable | `enable` |
| Documentación XML | generada (visible en IntelliSense) |
| Dependencias NuGet | `QRCoder` 1.8.0 y `SixLabors.ImageSharp` 2.1.13 |
| Nativas / System.Drawing | **ninguna** (ImageSharp es 100 % gestionado; corre en Windows/Linux/macOS) |
| Distribución | `ProjectReference` dentro del monorepo `LibreriasWanaka`, o `LibreriaQR.dll` + sus NuGet |
| Thread-safety | `GeneradorQR` no tiene estado; una instancia es segura entre hilos (usable como singleton) |
| Excepciones | `QrContenidoInvalidoException` (datos inválidos), `QrLogoInvalidoException` (logo ilegible), ambas derivan de `QrException` |

## 2. Para qué sirve (en una frase)

**Generar códigos QR** de los 13 tipos de contenido nativos, con logo opcional, devolviendo la imagen como **PNG (`byte[]`), Base64 y data URI**.

## 3. Modelo mental

1. Elegís un **contenido** (`ContenidoQR`): una subclase por tipo, en `LibreriaQR.Contenidos`.
2. (Opcional) configurás el **render** con `OpcionesQR` (tamaño, colores, logo, corrección).
3. Llamás a `GeneradorQR.Generar(contenido, opciones)` → obtenés un `ResultadoQR`.

Para los 8 tipos simples hay **atajos** en `GeneradorQR` (`DesdeUrl`, `DesdeWifi`, …). Los 5 con muchos campos (vCard, MeCard, evento, 2FA, cripto) se arman por objeto y se pasan a `Generar`.

## 4. API pública completa

### 4.1 `GeneradorQR`

```csharp
public sealed class GeneradorQR
{
    public ResultadoQR Generar(ContenidoQR contenido, OpcionesQR? opciones = null);
    public static ResultadoQR Crear(ContenidoQR contenido, OpcionesQR? opciones = null);

    // Atajos (tipos simples):
    public ResultadoQR DesdeUrl(string url, OpcionesQR? opciones = null);
    public ResultadoQR DesdeTexto(string texto, OpcionesQR? opciones = null);
    public ResultadoQR DesdeTelefono(string numero, OpcionesQR? opciones = null);
    public ResultadoQR DesdeSms(string numero, string mensaje = "", OpcionesQR? opciones = null);
    public ResultadoQR DesdeWifi(string ssid, string password,
                                 TipoCifradoWifi cifrado = TipoCifradoWifi.WPA,
                                 bool oculta = false, OpcionesQR? opciones = null);
    public ResultadoQR DesdeCorreo(string destinatario, string asunto = "", string cuerpo = "", OpcionesQR? opciones = null);
    public ResultadoQR DesdeUbicacion(double latitud, double longitud, OpcionesQR? opciones = null);
    public ResultadoQR DesdeMensajeria(AppMensajeria app, string destino, string mensaje = "", OpcionesQR? opciones = null);
}
```

- `contenido = null` → `ArgumentNullException`.
- `opciones = null` → usa `new OpcionesQR()` (defaults).
- Datos inválidos del contenido → `QrContenidoInvalidoException`.

### 4.2 `OpcionesQR`

```csharp
public sealed class OpcionesQR
{
    public int  PixelesPorModulo   { get; set; } = 20;    // ignorado si TamanoPx tiene valor
    public int? TamanoPx           { get; set; }          // tamaño objetivo (lado); el real es múltiplo de módulos
    public NivelCorreccion? Correccion { get; set; }      // null = recomendado por el contenido / Máximo si hay logo
    public string ColorPrimerPlano { get; set; } = "#000000";  // hex #RRGGBB o #RRGGBBAA
    public string ColorFondo       { get; set; } = "#FFFFFF";
    public bool   FondoTransparente{ get; set; } = false;
    public bool   IncluirZonaQuieta{ get; set; } = true;  // margen blanco de 4 módulos (déjalo en true)
    public OpcionesLogo? Logo      { get; set; }          // null = sin logo
}
```

### 4.3 `OpcionesLogo`

```csharp
public sealed class OpcionesLogo
{
    public byte[]? Bytes       { get; set; }              // prioridad sobre Ruta
    public string? Ruta        { get; set; }              // archivo de imagen (PNG/JPG/BMP/GIF)
    public double  Proporcion  { get; set; } = 0.22;      // 0.05–0.35 del ancho del QR
    public bool    ConFondo    { get; set; } = true;      // recuadro detrás del logo
    public string  ColorFondo  { get; set; } = "#FFFFFF";
    public int     MargenPx    { get; set; } = 8;
    public double  RadioEsquinas { get; set; } = 0.18;    // 0 = recto, 0.5 = círculo
}
```

### 4.4 `ResultadoQR`

```csharp
public sealed class ResultadoQR
{
    public byte[] Png            { get; init; }   // nunca null
    public string Base64         { get; init; }   // sin prefijo "data:"
    public string DataUri        { get; init; }   // "data:image/png;base64,...."
    public TipoQR Tipo           { get; init; }
    public string Payload        { get; init; }   // el texto codificado
    public int    AnchoPx        { get; init; }
    public int    AltoPx         { get; init; }
    public NivelCorreccion Correccion { get; init; }
    public bool   TieneLogo      { get; init; }
    public void   GuardarPng(string ruta);
    public override string ToString();            // devuelve DataUri
}
```

### 4.5 Enums

```csharp
public enum TipoQR { EnlaceWeb=1, TextoPlano=2, LlamadaTelefonica=3, MensajeSms=4, RedWifi=5,
    TarjetaVCard=6, TarjetaMeCard=7, CorreoElectronico=8, UbicacionGps=9, EventoCalendario=10,
    Autenticacion2Fa=11, MensajeriaApp=12, Criptomoneda=13 }

public enum NivelCorreccion { Bajo=0, Medio=1, Alto=2, Maximo=3 }   // L, M, Q, H
public enum TipoCifradoWifi { WPA=0, WEP=1, SinCifrado=2 }
public enum VersionVCard    { V21=0, V3=1, V4=2 }
public enum AppMensajeria   { WhatsApp=0, Telegram=1 }
public enum Criptomoneda    { Bitcoin=0, BitcoinCash=1, Litecoin=2, Ethereum=3 }
public enum Tipo2Fa         { Totp=0, Hotp=1 }
public enum Algoritmo2Fa    { Sha1=0, Sha256=1, Sha512=2 }
```

### 4.6 Clases de contenido (`LibreriaQR.Contenidos`)

Todas derivan de `ContenidoQR` y exponen `ConstruirPayload()`. Propiedades por tipo:

```csharp
ContenidoUrl      { string Url }
ContenidoTexto    { string Texto }
ContenidoTelefono { string Numero }
ContenidoSms      { string Numero; string Mensaje }
ContenidoWifi     { string Ssid; string Password; TipoCifradoWifi Cifrado; bool Oculta }
ContenidoVCard    { string Nombre; string Apellido; string? Empresa; string? Cargo;
                    string? Telefono; string? Movil; string? TelefonoTrabajo; string? Correo;
                    string? SitioWeb; string? Direccion; string? Ciudad; string? Region;
                    string? CodigoPostal; string? Pais; string? Nota; VersionVCard Version }
ContenidoMeCard   { string Nombre; string Apellido; string? Telefono; string? Correo;
                    string? Direccion; string? SitioWeb; string? Nota }
ContenidoCorreo   { string Destinatario; string Asunto; string Cuerpo }
ContenidoUbicacion{ double Latitud; double Longitud }
ContenidoEvento   { string Titulo; string Descripcion; string Ubicacion;
                    DateTime Inicio; DateTime Fin; bool TodoElDia }
ContenidoAutenticacion2Fa { string Secreto; string Emisor; string Cuenta; Tipo2Fa Tipo2Fa;
                    Algoritmo2Fa Algoritmo; int Digitos; int PeriodoSegundos; int Contador }
ContenidoMensajeria { AppMensajeria App; string Destino; string Mensaje }
ContenidoCripto   { Criptomoneda Moneda; string Direccion; decimal? Monto; string? Etiqueta; string? Mensaje }
```

## 5. Payload exacto por tipo (verificado con lector ZXing)

| Tipo | Ejemplo de entrada | Payload generado |
|---|---|---|
| URL | `"acsa.com.sv"` | `http://acsa.com.sv` (antepone esquema si falta) |
| Texto | `"Hola ACSA"` | `Hola ACSA` |
| Teléfono | `"+50322500000"` | `tel:+50322500000` |
| SMS | num + `"Consulta"` | `sms:+50322500000?body=Consulta` |
| WiFi | ssid/pass WPA | `WIFI:T:WPA;S:ACSA-Invitados;P:Clave1234;;` |
| vCard | V3 | `BEGIN:VCARD` … `VERSION:3.0` … `END:VCARD` |
| MeCard | apellido/nombre | `MECARD:N:Lainez,Cesar;TEL:...;EMAIL:...;;` |
| Correo | dest+asunto+cuerpo | `mailto:x@y?subject=...&body=...` |
| Ubicación | 13.6989, -89.1914 | `geo:13.6989,-89.1914` |
| Evento | título+fechas | `BEGIN:VEVENT` … `END:VEVENT` |
| 2FA TOTP | secreto/emisor/cuenta | `otpauth://totp/ACSA:cuenta?secret=...&issuer=ACSA&digits=6&period=30&algorithm=SHA1` |
| WhatsApp | `"+503 2250-0000"`+msg | `https://wa.me/50322500000?text=...` (solo dígitos) |
| Telegram | `"@acsa_sv"` | `https://t.me/acsa_sv` |
| Bitcoin | dir + monto | `bitcoin:<dir>?amount=...` (BIP21) |
| Ethereum | dir + 1 ETH | `ethereum:<dir>?value=1000000000000000000` (EIP-681, wei) |

## 6. Escenarios copy-paste

### 6.1 URL para un `<img>` en Blazor/HTML

```csharp
using LibreriaQR;
var r = new GeneradorQR().DesdeUrl("https://acsa.com.sv");
// <img src="@r.DataUri" />
string html = $"<img src=\"{r.DataUri}\" alt=\"QR\" />";
```

### 6.2 WiFi de invitados

```csharp
var r = new GeneradorQR().DesdeWifi("ACSA-Invitados", "Clave1234", TipoCifradoWifi.WPA);
r.GuardarPng(@"C:\temp\wifi.png");
```

### 6.3 vCard con color corporativo y tamaño fijo

```csharp
using LibreriaQR;
using LibreriaQR.Contenidos;

var vcard = new ContenidoVCard
{
    Nombre = "Cesar", Apellido = "Lainez", Empresa = "ACSA", Cargo = "TI",
    Movil = "+503 7000-0000", Correo = "soporte@acsa.com.sv", SitioWeb = "https://acsa.com.sv",
};
var opciones = new OpcionesQR { TamanoPx = 600, ColorPrimerPlano = "#003A84" };
var r = GeneradorQR.Crear(vcard, opciones);
```

### 6.4 QR con logo (fuerza corrección Máxima)

```csharp
var opciones = new OpcionesQR
{
    TamanoPx = 600,
    Logo = new OpcionesLogo { Ruta = @"C:\marca\logo.png", Proporcion = 0.24 },
};
var r = new GeneradorQR().DesdeUrl("https://acsa.com.sv", opciones);
```

### 6.5 2FA (TOTP) para vincular Google Authenticator

```csharp
var otp = new ContenidoAutenticacion2Fa
{
    Secreto = "JBSWY3DPEHPK3PXP",   // Base32
    Emisor  = "ACSA",
    Cuenta  = "soporte@acsa.com.sv",
};
var r = new GeneradorQR().Generar(otp);
```

### 6.6 Criptomoneda

```csharp
var btc = new ContenidoCripto { Moneda = Criptomoneda.Bitcoin,
    Direccion = "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2", Monto = 0.005m };
var eth = new ContenidoCripto { Moneda = Criptomoneda.Ethereum,
    Direccion = "0x71C7656EC7ab88b098defB751B7401B5f6d8976F", Monto = 1m };
var g = new GeneradorQR();
var rBtc = g.Generar(btc);
var rEth = g.Generar(eth);
```

## 7. Errores que un agente NO debe suponer

- ❌ Que `GeneradorQR` requiera `Dispose`. **No es `IDisposable`.** El `QRCodeGenerator` interno se libera solo por llamada.
- ❌ Que exista un atajo `DesdeVCard`, `DesdeEvento`, `Desde2Fa` o `DesdeCripto`. **No existen**; esos tipos se generan con `Generar(contenido, opciones)` sobre su objeto de contenido.
- ❌ Que `DesdeUrl("")` devuelva algo. **Lanza `QrContenidoInvalidoException`** (URL vacía).
- ❌ Que el MeCard salga en el formato multilinea de QRCoder. **Sale como MECARD estándar** de una línea (`MECARD:...;;`).
- ❌ Que Telegram acepte mensaje precargado. **No**: genera solo `https://t.me/<usuario>` (el parámetro `Mensaje` se ignora para Telegram; sí aplica a WhatsApp).
- ❌ Que el monto de Ethereum vaya en ETH. **Va en wei** (1 ETH = 10¹⁸ wei); la librería hace la conversión desde `decimal`.
- ❌ Que el color acepte nombres (`"red"`). **Solo hex** (`#RRGGBB` o `#RRGGBBAA`); un valor inválido lanza `QrContenidoInvalidoException`.
- ❌ Poner `Logo.Proporcion` > 0.35. Se recorta a 0.35; por encima de ~0.30 el QR puede no escanearse aun con corrección Máxima.
- ❌ Que `TamanoPx` dé exactamente ese tamaño. El lado final es **múltiplo entero de módulos** (puede quedar algo por debajo del pedido).
- ❌ Que `ImageSharp` 3.x sirva igual. Se ancla en **2.1.x** a propósito (licencia Apache-2.0; la 3.x es de pago para empresas grandes).

## 8. Trazabilidad

Todos los tipos tienen un caso PASA/FALLA en `LibreriaQR/samples/LibreriaQR.Demo/Program.cs`, que además guarda los PNG en `bin/.../qr-salida/`:

```bash
dotnet run --project LibreriaQR/samples/LibreriaQR.Demo
```

Salida esperada: `Resultado: 18/18 PASA`. Cada PNG fue verificado como legible con un decodificador independiente (ZXing).

## 9. Referencia rápida (memoriza esto)

```csharp
using LibreriaQR;
using LibreriaQR.Contenidos;

var gen = new GeneradorQR();

// Simples (atajos):
gen.DesdeUrl("https://acsa.com.sv");
gen.DesdeTexto("Hola");
gen.DesdeTelefono("+50322500000");
gen.DesdeSms("+50322500000", "hola");
gen.DesdeWifi("SSID", "clave", TipoCifradoWifi.WPA);
gen.DesdeCorreo("x@acsa.com.sv", "asunto", "cuerpo");
gen.DesdeUbicacion(13.6989, -89.1914);
gen.DesdeMensajeria(AppMensajeria.WhatsApp, "+50322500000", "hola");

// Con muchos campos (objeto + Generar):
gen.Generar(new ContenidoVCard { Nombre = "C", Apellido = "L" });
gen.Generar(new ContenidoMeCard { Nombre = "C", Apellido = "L" });
gen.Generar(new ContenidoEvento { Titulo = "R", Inicio = DateTime.Now, Fin = DateTime.Now.AddHours(1) });
gen.Generar(new ContenidoAutenticacion2Fa { Secreto = "JBSWY3DPEHPK3PXP", Emisor = "ACSA", Cuenta = "x" });
gen.Generar(new ContenidoCripto { Moneda = Criptomoneda.Bitcoin, Direccion = "1Bv..." });

// Personalización:
var o = new OpcionesQR { TamanoPx = 512, ColorPrimerPlano = "#003A84",
    Logo = new OpcionesLogo { Ruta = @"C:\logo.png", Proporcion = 0.24 } };
var r = gen.DesdeUrl("https://acsa.com.sv", o);
// r.Png (byte[]), r.Base64, r.DataUri, r.GuardarPng("out.png")
```
