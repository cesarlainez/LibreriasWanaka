# LibreriasWanaka

> La guía técnica y de mantenimiento de toda la solución está en [`DOCUMENTACION_SOLUCION.md`](DOCUMENTACION_SOLUCION.md).

Monorepo de librerías internas de **ACSA** para .NET. Cada carpeta de nivel superior es una librería independiente, con sus propios `src/`, `samples/` y (según el caso) `tests/` y `app/`. Todas conviven bajo una única solución `LibreriasWanaka.slnx` en la raíz para que un solo Visual Studio (o `dotnet build`) las compile juntas.

> Publicado en: https://github.com/cesarlainez/LibreriasWanaka

## Contenido

| Librería | Qué hace | Target | Doc para IA | Estado |
|---|---|---|---|---|
| [**LibreriaOCR**](LibreriaOCR/README.md) | OCR de PDF / PNG / JPG / TIFF en español. Basada en Tesseract. Extrae texto de imágenes escaneadas y de PDFs (digitales, escaneados o mixtos). | `net10.0` | — | Estable |
| [**MarkItDown**](MarkItDown/README.md) | Conversión de **.docx** y **.pdf** a Markdown listo para IA, sin conexión a internet y sin usar IA. Puerto .NET nativo de [`microsoft/markitdown`](https://github.com/microsoft/markitdown). | `net10.0` | — | Estable |
| **MarkItDown (Framework 4.8)** | Misma librería que la anterior, pero portada a `.NET Framework 4.8` para aplicaciones legadas. Solución paralela dentro de [`MarkItDown/framework48/`](MarkItDown/framework48). | `net48` | — | Estable |
| [**LibreriaTokens**](LibreriaTokens/README.md) | Normaliza whitespace redundante y estima tokens antes de mandar prompts a LLMs (OpenAI/Anthropic). Cero dependencias NuGet. | `net10.0` | [`AGENTS.md`](LibreriaTokens/AGENTS.md) | Estable |
| [**LibreriaQR**](LibreriaQR/README.md) | Genera códigos QR de los 13 tipos nativos (URL, texto, teléfono, SMS, WiFi, vCard, MECARD, correo, GPS, evento, 2FA, WhatsApp/Telegram, cripto), con logo centrado y salida PNG/Base64/data URI. Sin System.Drawing ni binarios nativos. | `net10.0` | [`AGENTS.md`](LibreriaQR/AGENTS.md) | Estable |

> **Nota para agentes IA (Copilot, Cursor, Claude, GPT…):** cuando una librería tenga un `AGENTS.md` en su carpeta, usalo como fuente autoritativa. Está escrito para vos: contiene el API completo, ejemplos copy-paste y las reglas verificadas por la prueba de mesa. `README.md` es para humanos; `AGENTS.md` es para vos.

## Requisitos

- **.NET 10 SDK** (para todo el monorepo).
- **Visual Studio 2026 / VS Code / Rider** con soporte de `.slnx`.
- **Windows x64 o x86** para ejecutar los binarios nativos de Tesseract, Leptonica, PDFium y Skia que usa `LibreriaOCR` (se copian solos al `bin`).

## Cómo clonar y compilar todo

```bash
git clone https://github.com/cesarlainez/LibreriasWanaka.git
cd LibreriasWanaka
dotnet restore LibreriasWanaka.slnx
dotnet build   LibreriasWanaka.slnx -c Release
```

Para correr los tests de MarkItDown:

```bash
dotnet test MarkItDown/tests/MarkItDown.Tests/MarkItDown.Tests.csproj
```

## Cómo consumir una librería desde otro proyecto

Hay dos maneras: como **referencia de proyecto** (recomendado cuando tu solución vive junto a `LibreriasWanaka`) o copiando la **DLL compilada** (para soluciones separadas o Blazor/WinForms cerradas).

### Referencia de proyecto (mismo solución/workspace)

```bash
dotnet add MiApi.csproj reference "../LibreriasWanaka/LibreriaOCR/src/LibreriaOCR/LibreriaOCR.csproj"
dotnet add MiApi.csproj reference "../LibreriasWanaka/MarkItDown/src/MarkItDown/MarkItDown.csproj"
```

Solo hace falta referenciar el proyecto de **fachada** (`LibreriaOCR` y `MarkItDown`); las capas internas (`MarkItDown.Core`, `MarkItDown.Converters`, `Engines`) llegan por transitividad.

### DLL compilada (integrar en una solución externa)

1. `dotnet build LibreriasWanaka.slnx -c Release`.
2. Copiá los archivos de `bin/Release/net10.0/` (o `net48/` según variante) de la librería que necesites:

   - **LibreriaOCR**: `LibreriaOCR.dll` + la carpeta **`tessdata/`** completa (los `.traineddata` deben quedar junto al `.dll` del ejecutable final).
   - **MarkItDown**: `MarkItDown.dll` + `MarkItDown.Core.dll` + `MarkItDown.Converters.dll`.

3. En tu proyecto: `Referencias > Agregar > Examinar…` y apuntar a esas DLL. Las dependencias NuGet (Tesseract, PdfPig, DocumentFormat.OpenXml, PDFtoImage) hay que restaurarlas en el consumidor —lo más limpio sigue siendo el `ProjectReference`.

## Estructura del repositorio

```
LibreriasWanaka/
├── LibreriasWanaka.slnx          ← solución única de VS
├── .gitignore
│
├── LibreriaOCR/                  ← librería 1
│   ├── README.md                 ← doc detallada de LibreriaOCR
│   ├── src/LibreriaOCR/          → LibreriaOCR.dll
│   └── samples/
│       ├── LibreriaOCR.Demo/           (consola)
│       └── LibreriaOCR.WinFormsTester/ (WinForms)
│
└── MarkItDown/                   ← librería 2
    ├── README.md                 ← doc detallada de MarkItDown
    ├── Directory.Build.props     ← comparte props para net10
    ├── src/
    │   ├── MarkItDown.Core/            (dominio)
    │   ├── MarkItDown.Converters/      (docx/pdf → Markdown)
    │   └── MarkItDown/                 (fachada + DI)
    ├── app/MarkItDown.App/       (WinForms de escritorio)
    ├── samples/MarkItDown.Sample (consola)
    ├── tests/MarkItDown.Tests    (xUnit)
    └── framework48/              ← variante para .NET Framework 4.8
        ├── Directory.Build.props (fija net48 y aísla del padre)
        ├── src/ · app/ · samples/ · tests/
        └── Leame.txt
```

## Añadir una nueva librería al monorepo

1. Crear la carpeta en la raíz, por ejemplo `LibreriaFacturas/`, con la estructura habitual:
   ```
   LibreriaFacturas/
   ├── README.md
   ├── src/LibreriaFacturas/LibreriaFacturas.csproj
   └── samples/LibreriaFacturas.Demo/LibreriaFacturas.Demo.csproj
   ```
2. Registrarla en [`LibreriasWanaka.slnx`](LibreriasWanaka.slnx) agregando un bloque:
   ```xml
   <Folder Name="/LibreriaFacturas/">
     <Folder Name="/LibreriaFacturas/src/">
       <Project Path="LibreriaFacturas/src/LibreriaFacturas/LibreriaFacturas.csproj" />
     </Folder>
     <Folder Name="/LibreriaFacturas/samples/">
       <Project Path="LibreriaFacturas/samples/LibreriaFacturas.Demo/LibreriaFacturas.Demo.csproj" />
     </Folder>
   </Folder>
   ```
3. Añadir la fila correspondiente a la tabla de arriba (**Contenido**) y un `README.md` en la carpeta con la misma estructura que los existentes: qué hace, requisitos, uso básico, opciones y limitaciones.

## Convenciones compartidas

- **Namespace raíz** = nombre del proyecto (`LibreriaOCR`, `MarkItDown`).
- **Target por defecto**: `net10.0`. Solo `MarkItDown/framework48/` compila para `net48`.
- **`Nullable`** habilitado y `LangVersion=latest` en todos los proyectos.
- **Documentación XML** (`GenerateDocumentationFile`) en las librerías públicas — el consumidor ve los `<summary>` en IntelliSense.
- **`README.md` en la raíz de cada librería** describiendo API pública, opciones y ejemplos de uso.
- **Tests** en `tests/*.Tests/` con xUnit (donde aplique).

## Licencia y autoría

Uso interno de ACSA. Autor: **Cesar Lainez** (soporte@acsa.com.sv). Las dependencias de terceros mantienen su licencia original (MIT, Apache-2.0). Revisá el `README.md` de cada librería para el detalle.
