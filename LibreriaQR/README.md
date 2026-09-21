# LibreriaQR

Generación de **códigos QR** para .NET 10 con los **13 tipos de contenido** que un lector interpreta de forma nativa (abrir una URL, marcar un teléfono, conectar a una WiFi, guardar un contacto, agendar un evento, vincular un 2FA, etc.), con **logo centrado** opcional y salida como **PNG / Base64 / data URI**.

- **Namespace / assembly:** `LibreriaQR`
- **Target:** `net10.0`
- **Dependencias NuGet:** `QRCoder` (algoritmo QR + payloads) y `SixLabors.ImageSharp` **2.1.x** (render y logo, 100 % gestionado, sin System.Drawing ni binarios nativos, licencia Apache-2.0).
- **Multiplataforma:** sí (Windows / Linux / macOS). No usa System.Drawing.
- **Thread-safe:** `GeneradorQR` no guarda estado; una instancia sirve para toda la app.

> ¿Sos un asistente IA? Usá [`AGENTS.md`](AGENTS.md): tiene el API completo y ejemplos copy-paste.

## Instalación / referencia

Dentro del monorepo `LibreriasWanaka` ya está en la solución. Desde otro proyecto:

```bash
dotnet add MiProyecto.csproj reference "..\LibreriasWanaka\LibreriaQR\src\LibreriaQR\LibreriaQR.csproj"
```

O como DLL compilada: `LibreriaQR.dll` + sus dependencias NuGet (`QRCoder`, `SixLabors.ImageSharp`) restauradas en el consumidor. Lo más limpio es el `ProjectReference`.

## Uso en 30 segundos

```csharp
using LibreriaQR;
using LibreriaQR.Contenidos;

var gen = new GeneradorQR();

// 1) Atajos para los tipos simples
ResultadoQR r = gen.DesdeUrl("https://acsa.com.sv");

// 2) Data URI listo para un <img> en HTML/Blazor
string src = r.DataUri;            // "data:image/png;base64,...."

// 3) O guardar el PNG a disco
r.GuardarPng(@"C:\temp\acsa.png");
```

Para los tipos con muchos campos, se arma el objeto de contenido y se llama a `Generar`:

```csharp
var vcard = new ContenidoVCard
{
    Nombre = "Cesar", Apellido = "Lainez",
    Empresa = "ACSA", Cargo = "TI",
    Movil = "+503 7000-0000", Correo = "soporte@acsa.com.sv",
};
ResultadoQR r = gen.Generar(vcard);
```

## Los 13 tipos

| # | Tipo | `TipoQR` | Clase de contenido | Atajo |
|---|---|---|---|---|
| 1 | Enlace web (URL) | `EnlaceWeb` | `ContenidoUrl` | `DesdeUrl` |
| 2 | Texto plano | `TextoPlano` | `ContenidoTexto` | `DesdeTexto` |
| 3 | Llamada telefónica | `LlamadaTelefonica` | `ContenidoTelefono` | `DesdeTelefono` |
| 4 | Mensaje SMS | `MensajeSms` | `ContenidoSms` | `DesdeSms` |
| 5 | Red Wi-Fi | `RedWifi` | `ContenidoWifi` | `DesdeWifi` |
| 6 | Tarjeta de contacto completa (vCard) | `TarjetaVCard` | `ContenidoVCard` | `Generar` |
| 7 | Tarjeta de contacto simple (MECARD) | `TarjetaMeCard` | `ContenidoMeCard` | `Generar` |
| 8 | Correo electrónico (mailto) | `CorreoElectronico` | `ContenidoCorreo` | `DesdeCorreo` |
| 9 | Ubicación GPS | `UbicacionGps` | `ContenidoUbicacion` | `DesdeUbicacion` |
| 10 | Evento de calendario (VEVENT) | `EventoCalendario` | `ContenidoEvento` | `Generar` |
| 11 | Autenticación 2FA (otpauth) | `Autenticacion2Fa` | `ContenidoAutenticacion2Fa` | `Generar` |
| 12 | Mensajería (WhatsApp / Telegram) | `MensajeriaApp` | `ContenidoMensajeria` | `DesdeMensajeria` |
| 13 | Dirección de criptomoneda | `Criptomoneda` | `ContenidoCripto` | `Generar` |

## Personalización (`OpcionesQR`)

```csharp
var opciones = new OpcionesQR
{
    TamanoPx = 512,                 // tamaño objetivo (o usar PixelesPorModulo)
    ColorPrimerPlano = "#003A84",   // azul ACSA
    ColorFondo = "#FFFFFF",
    FondoTransparente = false,      // true = PNG con alfa
    Correccion = NivelCorreccion.Alto,
};

ResultadoQR r = gen.DesdeUrl("https://acsa.com.sv", opciones);
```

### Con logo

```csharp
var opciones = new OpcionesQR
{
    TamanoPx = 600,
    Logo = new OpcionesLogo
    {
        Ruta = @"C:\marca\logo-acsa.png",  // o Bytes = byte[]
        Proporcion = 0.24,                  // 24 % del ancho del QR
        ConFondo = true,                    // recuadro blanco detrás
        RadioEsquinas = 0.18,               // esquinas redondeadas
    },
};

ResultadoQR r = gen.DesdeUrl("https://acsa.com.sv", opciones);
```

> Con logo, la corrección de errores sube automáticamente a **Máximo (H, ~30 %)** para que el código siga leyéndose aunque el logo tape parte del centro. No pongás `Proporcion` por encima de `0.30`.

## Qué devuelve (`ResultadoQR`)

| Propiedad | Tipo | Descripción |
|---|---|---|
| `Png` | `byte[]` | Imagen PNG cruda. |
| `Base64` | `string` | PNG en Base64 (sin prefijo). |
| `DataUri` | `string` | `data:image/png;base64,...` para un `<img>`. |
| `Payload` | `string` | El texto que quedó dentro del QR. |
| `AnchoPx` / `AltoPx` | `int` | Dimensiones. |
| `Correccion` | `NivelCorreccion` | Nivel realmente aplicado. |
| `TieneLogo` | `bool` | Si lleva logo. |
| `GuardarPng(ruta)` | método | Escribe el PNG a disco. |

## Probarlo

**Consola (prueba de mesa):**

```bash
dotnet run --project LibreriaQR/samples/LibreriaQR.Demo
```

Corre una **prueba de mesa** de los 13 tipos (valida el payload y que el PNG no esté vacío) y deja los PNG de ejemplo en `bin/.../qr-salida/`. Todos los QR generados fueron verificados como legibles con un lector independiente (ZXing).

**WinForms (interactivo):**

```bash
dotnet run --project LibreriaQR/samples/LibreriaQR.WinFormsTester
```

Ventana completa para elegir cualquiera de los 13 tipos, rellenar sus campos, ajustar colores / tamaño / corrección, incrustar un logo y ver la vista previa en vivo. Botones para **guardar el PNG** y **copiar el Base64 / data URI** al portapapeles.

## Notas y límites

- El QR de **MECARD** se genera en el formato estándar de una sola línea (`MECARD:...;;`), no en el formato propio de QRCoder, para que los lectores lo reconozcan como "agregar contacto".
- **Telegram** genera `https://t.me/<usuario>` (no admite mensaje precargado); **WhatsApp** sí precarga el mensaje.
- **Ethereum** usa EIP-681 (`ethereum:<dirección>?value=<wei>`); Bitcoin/BCH/Litecoin usan BIP21 (`bitcoin:<dirección>?amount=...`).
- El render escala con vecino más cercano (bordes nítidos, sin difuminado). El tamaño final siempre es múltiplo entero de módulos.

## Licencia

Uso interno de ACSA. `QRCoder` (MIT) y `SixLabors.ImageSharp` 2.1.x (Apache-2.0) mantienen su licencia original.
