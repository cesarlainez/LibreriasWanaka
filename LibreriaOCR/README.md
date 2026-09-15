# LibreriaOCR

Librería en **.NET 10** que recibe un documento (**PDF, PNG, JPG/JPEG, TIF/TIFF**) y devuelve el texto por **OCR** en español. 100 % gratuita, basada en **Tesseract**.

Sirve igual en **web (ASP.NET/Blazor)**, **escritorio (WinForms/WPF)** o un **servicio de Windows**: `OcrService` es *thread-safe* (mantiene un pool interno de motores).

## Requisitos

- .NET 10 SDK
- Windows x64/x86 (los binarios nativos de Tesseract, Leptonica, PDFium y Skia se copian solos al `bin`).

## Estructura

| Proyecto | Qué es |
|---|---|
| `src/LibreriaOCR` | La librería (`LibreriaOCR.dll`) |
| `samples/LibreriaOCR.Demo` | Consola de prueba (`LibreriaOCR.Demo <archivo>…`) |
| `samples/LibreriaOCR.WinFormsTester` | App WinForms: botón para escoger documento y ver el OCR |

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
OcrResult r = await ocr.RecognizeAsync(stream, "escaneo.tif");
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

## `OcrResult`

| Miembro | Descripción |
|---|---|
| `Text` | Texto completo (todas las páginas) |
| `Pages` | Lista de `OcrPage` (texto y confianza por página) |
| `MeanConfidence` | Confianza media 0–100 (‑1 si no hubo OCR, p. ej. PDF digital) |
| `SourceKind` | `Image`, `PdfText`, `PdfOcr` o `Mixed` |
| `PageCount`, `Duration`, `FileName` | Metadatos |

## `OcrOptions`

| Opción | Por defecto | Para qué |
|---|---|---|
| `Languages` | `"spa"` | Idiomas Tesseract; combinar con `+` (ej. `"spa+eng"`). Requiere `<código>.traineddata` en `tessdata`. |
| `TessDataPath` | `null` → `./tessdata` | Carpeta de los `.traineddata` |
| `PdfDpi` | `300` | Resolución al rasterizar PDF escaneado |
| `PreferEmbeddedPdfText` | `true` | En PDF, usa el texto digital si existe y solo hace OCR de páginas escaneadas |
| `MinEmbeddedCharsPerPage` | `16` | Umbral para aceptar texto digital de una página sin OCR |
| `MaxConcurrency` | nº de núcleos | Máximo de motores OCR simultáneos |

## Idioma / precisión

- Incluido: **español** (`tessdata/spa.traineddata`, versión *fast*).
- ¿Más precisión? Reemplaza ese archivo por la versión **best**:
  <https://github.com/tesseract-ocr/tessdata_best/raw/main/spa.traineddata> (más lento pero más exacto).
- ¿Otro idioma? Descarga su `.traineddata` en `tessdata` y ponlo en `Languages`.

## Notas

- **TIFF multipágina**: soportado (una `OcrPage` por página).
- **PDF híbrido**: si la página ya trae texto digital se extrae directo (exacto, sin OCR); si es escaneo, se rasteriza y se hace OCR. Un PDF mixto devuelve `SourceKind = Mixed`.
- **Cambiar de motor**: `OcrService` usa la interfaz `IOcrEngine`; se puede enchufar otro motor (p. ej. PaddleOCR) con el constructor `new OcrService(() => new MiMotor(), options)`.

## Probar

```bash
# Consola
dotnet run --project samples/LibreriaOCR.Demo -- "C:\ruta\documento.tif"

# WinForms (botón para escoger documento)
dotnet run --project samples/LibreriaOCR.WinFormsTester
```

## Resultados de prueba (6 TIF escaneados de pólizas)

| Documento | Confianza | Tiempo |
|---|---|---|
| Póliza asistencia | 95 % | 1.4 s |
| Anexo de exclusión | 91 % | 1.3 s |
| Aviso de emisión | 88 % | 1.2 s |
| Póliza diamante | 85 % | 1.1 s |
| (otros dos) | 94–95 % | 1.5–2.1 s |

Promedio ~**91 %** de confianza, ~1.5 s por página.
