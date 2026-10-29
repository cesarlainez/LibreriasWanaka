# LibreriaOCR

> Parte de [**LibreriasWanaka**](../README.md).

Librería en **.NET 10** que recibe un documento (**PDF, PNG, JPG/JPEG, TIF/TIFF**) y devuelve el texto por **OCR** en español. 100 % gratuita, basada en **Tesseract**. Sin conexión a internet.

Sirve igual en **web (ASP.NET / Blazor)**, **escritorio (WinForms / WPF)** o un **servicio de Windows**: `OcrService` es *thread-safe* y mantiene un pool interno de motores.

## Requisitos

- .NET 10 SDK
- Windows x64/x86 (los binarios nativos de Tesseract, Leptonica, PDFium y Skia se copian solos al `bin`).

## Instalar en tu proyecto

Referencia de proyecto (recomendado dentro del monorepo o si clonás `LibreriasWanaka` junto a tu solución):

```bash
dotnet add MiApi.csproj reference "../LibreriasWanaka/LibreriaOCR/src/LibreriaOCR/LibreriaOCR.csproj"
```

Como DLL compilada: copiar `LibreriaOCR.dll` **junto con la carpeta `tessdata/`** al `bin` del ejecutable final (los `.traineddata` se buscan por defecto al lado del ensamblado).

## Estructura

| Proyecto | Qué es |
|---|---|
| `src/LibreriaOCR` | La librería (`LibreriaOCR.dll`) |
| `samples/LibreriaOCR.Demo` | Consola de prueba (`LibreriaOCR.Demo [--manuscrito] <archivo>…`) |
| `samples/LibreriaOCR.WinFormsTester` | App WinForms: botones para OCR y para detectar escritura a mano |

## Uso básico

```csharp
using LibreriaOCR;

using var ocr = new OcrService();                 // español ("spa") por defecto
OcrResult r = ocr.Recognize(@"C:\docs\factura.pdf");

Console.WriteLine(r.Text);                         // texto completo
Console.WriteLine($"{r.MeanConfidence:0.#}%  {r.PageCount} págs  {r.SourceKind}");

foreach (var p in r.Pages)                         // desglose por página
    Console.WriteLine($"Pág {p.PageNumber}: {p.Confidence:0.#}%");
```

Entradas admitidas: **ruta de archivo**, **`Stream`** o **`byte[]`**; en versión síncrona (`Recognize`) y asíncrona (`RecognizeAsync`).

```csharp
OcrResult r  = await ocr.RecognizeAsync(stream, "escaneo.tif");
OcrResult r2 = ocr.Recognize(bytes, "documento.png");
string texto = OcrService.ExtractText(@"C:\docs\carta.png").Text;   // atajo puntual
```

## Integración por escenario

**Web (ASP.NET Core / Blazor)** — registrar como *singleton* (thread-safe):

```csharp
builder.Services.AddSingleton(new OcrService(new OcrOptions { Languages = "spa" }));
// luego, en un controlador/servicio, inyectar OcrService y llamar RecognizeAsync(...)
```

**Escritorio / Servicio** — crear una instancia y reutilizarla (no crear una por documento):

```csharp
private static readonly OcrService Ocr = new(new OcrOptions { Languages = "spa" });
```

## OCR de imágenes incrustadas en un PDF

Complemento del texto digital: si un PDF ya trae texto pero además incrusta imágenes con texto dentro (logotipos, banners, capturas de pantalla), `RecognizeEmbeddedImages` corre OCR **solo** sobre esas imágenes, sin rasterizar la página entera.

```csharp
byte[] pdf = File.ReadAllBytes(@"C:\docs\poliza.pdf");
OcrResult imgs = await ocr.RecognizeEmbeddedImagesAsync(pdf);

foreach (var p in imgs.Pages)
    Console.WriteLine($"Pág {p.PageNumber} · imagen · conf {p.Confidence:0.#}% · {p.Text}");
```

Las imágenes por debajo de `OcrOptions.MinEmbeddedImagePixels` (por defecto 40 000 px = ≈200×200) se ignoran para filtrar íconos y viñetas donde Tesseract solo produce ruido. Si el PDF no tiene imágenes procesables, devuelve un resultado vacío (no lanza).

## Detección de escritura a mano (manuscrito)

`HandwritingDetector` responde dos preguntas sobre un documento (PDF o imagen): **(a)** ¿tiene escritura a mano? y **(b)** ¿en qué regiones? Es una **heurística sin IA**: analiza la geometría de los trazos (irregularidad de altura, línea base y espaciado; trazos conectados como firmas) y, para descartar logos y gráficos, **corrobora con OCR** (la escritura a mano produce texto de baja confianza; lo impreso, alta).

```csharp
using LibreriaOCR;

using var detector = new HandwritingDetector();
HandwritingResult r = detector.Detect(@"C:\docs\solicitud-firmada.pdf");

if (r.HasHandwriting)
    foreach (var reg in r.Regions)   // cada región: página + caja + score
        Console.WriteLine($"Pág {reg.PageNumber}: ({reg.X},{reg.Y},{reg.Width}×{reg.Height}) score {reg.Score:0.#}");
```

Admite **ruta**, **`byte[]`** y los mismos formatos que el OCR (PDF, PNG, JPG, TIFF). La corroboración con OCR usa `tessdata`; si no está disponible, cae a solo-geometría.

| Opción (`HandwritingOptions`) | Por defecto | Para qué |
|---|---|---|
| `ScoreThreshold` | `55` | Umbral (0–100) para marcar una región como manuscrita. **Es el parámetro a calibrar.** |
| `CorroborateWithOcr` | `true` | Confirma con OCR para descartar logos/gráficos |
| `MaxOcrConfidenceForHandwriting` | `78` | Si la confianza OCR de la página supera esto, se asume impreso |
| `PdfDpi` / `MaxImageSide` | `150` / `1600` | Resolución de análisis (menor = más rápido) |
| `Languages` | `"spa"` | Idioma del OCR de corroboración |
| `IncludeDiagnostics` | `false` | Añade las métricas a `HandwritingRegion.Diagnostics` para calibrar |

> Es una heurística: conviene **calibrar `ScoreThreshold` con documentos reales**. Funciona mejor con **firmas y campos llenados a mano** que con texto corrido manuscrito.

## `OcrResult`

| Miembro | Descripción |
|---|---|
| `Text` | Texto completo (todas las páginas) |
| `Pages` | Lista de `OcrPage` (texto y confianza por página) |
| `MeanConfidence` | Confianza media 0–100 (−1 si no hubo OCR, p. ej. PDF digital) |
| `SourceKind` | `Image`, `PdfText`, `PdfOcr` o `Mixed` |
| `PageCount`, `Duration`, `FileName` | Metadatos |

`OcrPage.FromEmbeddedText = true` indica que esa página se resolvió sin OCR (texto digital del PDF); en ese caso `Confidence = −1`.

## `OcrOptions`

| Opción | Por defecto | Para qué |
|---|---|---|
| `Languages` | `"spa"` | Idiomas Tesseract; combinar con `+` (ej. `"spa+eng"`). Requiere `<código>.traineddata` en `tessdata`. |
| `TessDataPath` | `null` → `./tessdata` | Carpeta de los `.traineddata` |
| `PdfDpi` | `300` | Resolución al rasterizar PDF escaneado |
| `PreferEmbeddedPdfText` | `true` | En PDF, usa el texto digital si existe y solo hace OCR de páginas escaneadas |
| `MinEmbeddedCharsPerPage` | `16` | Umbral para aceptar texto digital de una página sin OCR |
| `MaxConcurrency` | nº de núcleos | Máximo de motores OCR simultáneos |
| `MinEmbeddedImagePixels` | `40 000` (≈200×200) | Umbral (ancho × alto) por debajo del cual las imágenes incrustadas se descartan en `RecognizeEmbeddedImages` |

## Idioma / precisión

- Incluido: **español** (`tessdata/spa.traineddata`, versión *fast*).
- ¿Más precisión? Reemplazá ese archivo por la versión **best**:
  <https://github.com/tesseract-ocr/tessdata_best/raw/main/spa.traineddata> (más lento pero más exacto).
- ¿Otro idioma? Descargá su `.traineddata` en `tessdata` y ponelo en `Languages`.

## Notas

- **TIFF multipágina**: soportado (una `OcrPage` por página).
- **PDF híbrido**: si la página trae texto digital se extrae directo (exacto, sin OCR); si es escaneo, se rasteriza y se hace OCR. Un PDF mixto devuelve `SourceKind = Mixed`.
- **Cambiar de motor**: `OcrService` usa la interfaz `IOcrEngine`; se puede enchufar otro motor (p. ej. PaddleOCR) con el constructor `new OcrService(() => new MiMotor(), options)`.
- **Excepciones**: `FormatoNoSoportadoException` (extensión no admitida), `OcrException` (PDF dañado o rasterizado fallido), más las estándar (`ArgumentException`, `FileNotFoundException`, `ObjectDisposedException`).

## Probar

```bash
# Consola — OCR
dotnet run --project samples/LibreriaOCR.Demo -- "C:\ruta\documento.tif"

# Consola — detección de manuscrito
dotnet run --project samples/LibreriaOCR.Demo -- --manuscrito "C:\ruta\solicitud.pdf"

# WinForms (botones de OCR y de manuscrito)
dotnet run --project samples/LibreriaOCR.WinFormsTester
```

## Resultados de prueba (6 TIF escaneados de pólizas)

| Documento | Confianza | Tiempo |
|---|---|---|
| Póliza asistencia | 95 % | 1.4 s |
| Anexo de exclusión | 91 % | 1.3 s |
| Aviso de emisión | 88 % | 1.2 s |
| Póliza (otro producto) | 85 % | 1.1 s |
| (otros dos) | 94–95 % | 1.5–2.1 s |

Promedio ~**91 %** de confianza, ~1.5 s por página.

## Dependencias

- [Tesseract](https://github.com/tesseract-ocr/tesseract) vía [TesseractOCR](https://www.nuget.org/packages/TesseractOCR) (Apache-2.0)
- [PdfPig](https://www.nuget.org/packages/PdfPig) (Apache-2.0) — texto digital de PDF
- [PDFtoImage](https://www.nuget.org/packages/PDFtoImage) (MIT) — rasterizado de PDF a imagen
- [SkiaSharp](https://www.nuget.org/packages/SkiaSharp) (MIT) — análisis de imagen para la detección de manuscrito (llega transitiva vía PDFtoImage)
