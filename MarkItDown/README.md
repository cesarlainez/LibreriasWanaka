# MarkItDown.Net

> Parte de [**LibreriasWanaka**](../README.md).

Librería en C# (**.NET 10**) que convierte documentos **Word (.docx)** y **PDF** a **Markdown listo para IA**, al estilo de [`microsoft/markitdown`](https://github.com/microsoft/markitdown), pero 100 % nativa en .NET, **sin usar IA** y **sin conexión a internet**: le das el archivo y te devuelve el Markdown.

Existe una variante paralela compilada para **.NET Framework 4.8** en [`framework48/`](framework48/), pensada para integrarse en aplicaciones legadas (WinForms 4.8, Web Forms, servicios Windows antiguos). Ver más abajo la sección *Variante .NET Framework 4.8*.

## Instalar en tu proyecto

Referencia de proyecto (recomendado dentro del monorepo o si clonás `LibreriasWanaka` junto a tu solución):

```bash
dotnet add MiApi.csproj reference "../LibreriasWanaka/MarkItDown/src/MarkItDown/MarkItDown.csproj"
```

Como DLL compilada: `dotnet build -c Release` y copiar del `bin/Release/net10.0/` estos tres ensamblados a tu proyecto:

- `MarkItDown.dll` (fachada + DI)
- `MarkItDown.Converters.dll` (docx/pdf)
- `MarkItDown.Core.dll` (contratos)

Solo hace falta agregar la referencia a `MarkItDown.dll`; las otras dos DLL viajan con ella.

## Arquitectura en capas

```
┌─────────────────────────────────────────────────────┐
│  MarkItDown  (fachada / API pública)                │
│  MarkItDownConverter · AddMarkItDown() para DI      │
├─────────────────────────────────────────────────────┤
│  MarkItDown.Converters  (infraestructura)           │
│  DocxToMarkdownConverter  → DocumentFormat.OpenXml  │
│  PdfToMarkdownConverter   → PdfPig                  │
├─────────────────────────────────────────────────────┤
│  MarkItDown.Core  (dominio / abstracciones)         │
│  IDocumentConverter · ConversionOptions             │
│  ConversionResult · excepciones                     │
└─────────────────────────────────────────────────────┘
```

- **`MarkItDown.Core`** — contratos y modelos. No depende de nada externo.
- **`MarkItDown.Converters`** — implementaciones por formato. Depende solo de Core.
- **`MarkItDown`** — punto de entrada que enruta por extensión y registra la DI. Es la única referencia que necesita tu aplicación (las otras dos DLL llegan por transitividad).

Además hay dos aplicaciones de ejemplo que consumen la librería:

- **`app/MarkItDown.App`** — aplicación de escritorio (Windows Forms, `net10.0-windows`): formulario para elegir origen, destino `.md` y convertir con un botón.
- **`samples/MarkItDown.Sample`** — herramienta de línea de comandos.

## Uso básico

```csharp
using MarkItDown;

var converter = new MarkItDownConverter();

// Desde una ruta
var resultado = converter.Convert(@"C:\docs\informe.docx");
Console.WriteLine(resultado.Markdown);

// Directo a archivo
converter.ConvertToFile(@"C:\docs\poliza.pdf", @"C:\docs\poliza.md");

// Desde un stream (por ejemplo, un IFormFile en ASP.NET Core)
using var stream = archivo.OpenReadStream();
var resultado2 = converter.Convert(stream, archivo.FileName);
```

`MarkItDownConverter` es **thread-safe** (interno *copy-on-write*): podés registrarlo como singleton y reutilizarlo entre peticiones.

## API pública

| Método | Descripción |
|---|---|
| `Convert(string path, ConversionOptions?)` | Convierte un archivo a `ConversionResult`. |
| `Convert(Stream, string fileNameOrExtension, ConversionOptions?)` | Convierte desde un stream; usa la extensión para elegir el convertidor. |
| `ConvertToMarkdown(string path, ConversionOptions?)` | Atajo que devuelve solo el string Markdown. |
| `ConvertToFile(string input, string output, ConversionOptions?)` | Convierte y escribe UTF-8 en `output`. |
| `RegisterConverter(IDocumentConverter)` | Agrega un convertidor propio (tiene prioridad sobre los integrados). |
| `SupportedExtensions` | Colección con las extensiones que puede procesar (`.docx`, `.pdf`, …). |

`ConversionResult` incluye:

| Propiedad  | Descripción                                                       |
|------------|-------------------------------------------------------------------|
| `Markdown` | El contenido convertido (GitHub Flavored Markdown).               |
| `Title`    | Título del documento (metadatos o primer encabezado), si existe.  |
| `Warnings` | Advertencias no fatales (imágenes omitidas, páginas sin texto…). |

## Opciones

```csharp
using MarkItDown.Core;

var opciones = new ConversionOptions
{
    ExtractImages = true,                  // extrae las imágenes del .docx como archivos
    ImageOutputDirectory = @"C:\docs\img", // carpeta de destino de las imágenes
    ImageLinkPrefix = "img/",              // prefijo del enlace ![alt](img/imagen-001.png)
    IncludePageMarkers = true,             // PDF: agrega <!-- Página N -->
    DetectPdfHeadings = true,              // PDF: detecta títulos por tamaño de fuente
};

var resultado = converter.Convert(@"C:\docs\informe.pdf", opciones);
```

## Inyección de dependencias (ASP.NET Core / Worker)

```csharp
builder.Services.AddMarkItDown();

// y luego en cualquier servicio:
public class MiServicio(MarkItDownConverter converter)
{
    public string Convertir(string ruta) => converter.ConvertToMarkdown(ruta);
}
```

## Extensible: agregar tus propios formatos

Implementá `IDocumentConverter` (capa Core) y registralo:

```csharp
converter.RegisterConverter(new MiConvertidorHtml()); // tiene prioridad sobre los integrados

// o vía DI, antes de AddMarkItDown():
services.AddSingleton<IDocumentConverter, MiConvertidorHtml>();
services.AddMarkItDown();
```

## Qué convierte

| Elemento Word (.docx)        | Markdown                          |
|------------------------------|-----------------------------------|
| Encabezados (Título 1–6, nivel de esquema, estilos localizados) | `#` … `######` |
| Negrita / cursiva / tachado  | `**x**` / `*x*` / `~~x~~`         |
| Listas con viñetas y numeradas (anidadas, directas o por estilo) | `- x` / `1. x` |
| Tablas (celdas combinadas y filas/celdas en content controls) | tabla Markdown con `\|` |
| Hipervínculos                | `[texto](url)`                    |
| Imágenes                     | `![alt](ruta)` con `ExtractImages`|
| Campos `fldSimple` (fecha, referencias, mail merge) | su valor visible |
| Notas al pie y al final      | `[^F1]` / `[^E1]` estilo GFM      |
| Saltos de línea y de página  | salto suave (`  ` + nueva línea)  |
| Content controls (`sdt`) y `customXml` | su contenido            |

| Elemento PDF                 | Markdown                          |
|------------------------------|-----------------------------------|
| Títulos (por tamaño de fuente/negrita) | `#` … `######` (heurístico) |
| Párrafos (une líneas y palabras cortadas con guion) | texto corrido |
| Viñetas (•, ◦, ▪, -, …)      | `- x`                             |
| Listas numeradas             | `1. x`                            |
| Páginas                      | `<!-- Página N -->` (opcional)    |

## Excepciones

| Excepción | Causa |
|---|---|
| `UnsupportedFileFormatException` | La extensión no tiene convertidor registrado. |
| `MarkdownConversionException` | El archivo está dañado, protegido con contraseña o no se puede leer. |
| `FileNotFoundException`, `ArgumentException`, `ArgumentNullException` | Errores de entrada estándar. |

## Limitaciones conocidas

- **PDF no guarda estructura semántica**: la detección de encabezados y listas es heurística (igual que en markitdown, que extrae texto plano). Documentos con maquetación compleja (varias columnas, tablas dibujadas con líneas) pueden salir imperfectos.
- **PDF escaneados** (solo imagen) no tienen texto extraíble; el resultado es vacío con una advertencia. **Esta librería no hace OCR** — para eso está [LibreriaOCR](../LibreriaOCR/README.md) en el mismo monorepo.
- **Palabras cortadas con guion** al final de línea en PDF: el guion se elimina solo si la palabra fusionada aparece en otra parte del documento; si un compuesto ("socio-económico") se corta justo en su guion y no se repite, el resultado conserva el guion (caso recuperable).
- **`.doc` antiguo** (Word 97–2003, binario) no está soportado: guardalo como `.docx`.
- Documentos protegidos con contraseña lanzan `MarkdownConversionException`.
- La API es sincrónica (OpenXml y PdfPig lo son); para no bloquear un hilo de UI envolvela en `Task.Run`.

## Combinar con LibreriaOCR (PDF escaneado → Markdown)

Si el PDF es puramente escaneado, primero pasalo por OCR y luego convertí el texto:

```csharp
using LibreriaOCR;
using MarkItDown;

using var ocr = new OcrService();
var texto = ocr.Recognize(@"C:\docs\escaneo.pdf").Text;
File.WriteAllText(@"C:\docs\escaneo.md", "# Escaneo\n\n" + texto);
```

## Variante .NET Framework 4.8

`framework48/` contiene la misma solución (Core + Converters + Fachada + App WinForms + Sample + Tests) pero compilada para `net48`. Se aísla del `Directory.Build.props` de la raíz mediante uno propio (`framework48/Directory.Build.props`), que fija `TargetFramework=net48`, agrega `Microsoft.NETFramework.ReferenceAssemblies` para compilar sin depender del targeting pack instalado y activa `AutoGenerateBindingRedirects`.

Usala cuando tu consumidor sea:

- Una app **WinForms / WPF sobre .NET Framework 4.8**.
- Un **Web Forms** o **WCF** legado.
- Un **servicio Windows** que aún no migró a .NET moderno.

El API pública (`MarkItDownConverter`, `ConversionOptions`, `ConversionResult`) es idéntica a la de `net10`; solo cambian las dependencias transitivas de DI (`Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2`).

## Compilar y probar

```bash
# Solución completa (desde la raíz del monorepo)
dotnet build LibreriasWanaka.slnx -c Release

# Solo tests de MarkItDown
dotnet test MarkItDown/tests/MarkItDown.Tests/MarkItDown.Tests.csproj

# App de escritorio (formulario Windows Forms):
dotnet run --project MarkItDown/app/MarkItDown.App

# Herramienta de línea de comandos:
dotnet run --project MarkItDown/samples/MarkItDown.Sample -- "C:\docs\informe.docx"
```

### App de escritorio

`MarkItDown.App` abre una ventana donde el usuario:

1. Pulsa **Examinar…** y elige el `.docx` o `.pdf` a convertir.
2. El destino `.md` se propone automáticamente (misma carpeta y nombre); puede cambiarlo con el segundo **Examinar…**.
3. Opcionalmente marca *Extraer imágenes* (a una subcarpeta `imagenes`) y *Abrir al terminar*.
4. Pulsa **Convertir a Markdown**. La conversión corre en segundo plano (no congela la ventana) y muestra el resultado, el título detectado y las advertencias.

## Dependencias (open source, sin IA)

- [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml) (MIT) — lectura de .docx.
- [PdfPig](https://www.nuget.org/packages/PdfPig) (Apache-2.0) — extracción de texto y layout de PDF.
- [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions) (MIT) — solo para `AddMarkItDown()`.
