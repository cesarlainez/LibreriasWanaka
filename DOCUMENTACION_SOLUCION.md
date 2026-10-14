# Documentación técnica de la solución LibreriasWanaka

Documento de referencia para mantener y ampliar el monorepo de librerías internas de ACSA.
Describe el estado comprobado de la solución al **20 de septiembre de 2026**, sus requisitos,
convenciones y la forma esperada de agregar nuevos proyectos.

> Este documento es la guía técnica de la solución. Cada librería debe conservar además su
> propio `README.md` con el API, ejemplos y limitaciones específicas. Si una librería tiene
> `AGENTS.md`, ese archivo contiene instrucciones adicionales y específicas para agentes IA.

## 1. Identidad y alcance

| Campo | Valor |
|---|---|
| Solución | `LibreriasWanaka.slnx` |
| Tipo de repositorio | Monorepo de librerías reutilizables |
| Organización | Una carpeta de primer nivel por librería |
| Lenguaje | C# |
| Plataforma principal | .NET 10 (`net10.0`) |
| Plataforma heredada | .NET Framework 4.8 (`net48`), únicamente para la variante `MarkItDown/framework48` |
| Estilo de proyectos | SDK-style (`Microsoft.NET.Sdk`) |
| Nullable | Habilitado (`Nullable=enable`) |
| Usings implícitos | Habilitados (`ImplicitUsings=enable`) |
| Versión de lenguaje | `latest` en los proyectos que tienen configuración compartida |
| Documentación XML | Obligatoria para las librerías públicas nuevas; actualmente habilitada en OCR, QR y Tokens |
| Autor/producto | ACSA / LibreriasWanaka |
| Licenciamiento | Uso interno de ACSA; las dependencias conservan sus licencias originales |

### 1.1 Estado del repositorio

La solución registrada contiene proyectos de `LibreriaOCR`, `MarkItDown`, `LibreriaTokens` y,
en el árbol de trabajo actual, entradas para `LibreriaQR`. La carpeta `LibreriaQR` continúa sin
seguimiento de Git y la modificación de `LibreriasWanaka.slnx` también está pendiente de
confirmación. Se documenta para que la discrepancia sea visible; no se modifican ni se agregan
archivos al control de versiones automáticamente.

No se encontró `global.json`, `Directory.Packages.props` ni `NuGet.config` en la raíz. Por lo
tanto, el SDK seleccionado por defecto es el instalado en la máquina y las versiones NuGet se
declaran en cada `.csproj`.

## 2. Requisitos de desarrollo

### 2.1 SDK y runtimes comprobados

La máquina usada para verificar la solución tiene:

- SDK activo: **.NET SDK 10.0.302**.
- SDKs instalados: 9.0.307, 10.0.301 y 10.0.302.
- Runtime principal para esta solución: `Microsoft.NETCore.App 10.0.x`.
- Para WinForms: `Microsoft.WindowsDesktop.App 10.0.x`.
- Para la variante heredada: paquete `Microsoft.NETFramework.ReferenceAssemblies 1.0.3`.

El requisito de compilación recomendado es instalar el SDK de .NET 10 más reciente disponible,
manteniendo el mismo major que los proyectos `net10.0`. Para proyectos `net48` se requiere un
entorno Windows compatible con .NET Framework 4.8 o el paquete de ensamblados de referencia
incluido por NuGet.

### 2.2 IDE y sistema operativo

- Visual Studio, VS Code o Rider con soporte para proyectos SDK-style y `.slnx`.
- Windows es necesario para las aplicaciones WinForms y es el entorno validado para los
  binarios nativos usados por `LibreriaOCR`.
- `LibreriaQR` y `LibreriaTokens` están diseñadas para evitar dependencias nativas en la
  biblioteca principal.

## 3. Estructura del repositorio

```text
Librerias/
├── LibreriasWanaka.slnx
├── DOCUMENTACION_SOLUCION.md       ← esta guía técnica
├── README.md                       ← introducción y guía rápida
├── LibreriaOCR/
│   ├── README.md
│   ├── src/LibreriaOCR/             ← biblioteca
│   └── samples/                     ← consola y WinForms
├── LibreriaQR/
│   ├── src/LibreriaQR/              ← biblioteca agregada en el árbol de trabajo
│   └── samples/LibreriaQR.Demo/     ← demo existente; pendiente de migrar a WinForms
├── LibreriaTokens/
│   ├── README.md
│   ├── AGENTS.md
│   ├── src/LibreriaTokens/
│   └── samples/LibreriaTokens.Demo/
└── MarkItDown/
    ├── README.md
    ├── Directory.Build.props        ← configuración de la variante .NET moderna
    ├── src/                         ← Core, Converters y fachada
    ├── app/                         ← aplicación WinForms
    ├── samples/                     ← consola
    ├── tests/                       ← xUnit
    └── framework48/                 ← copia aislada para .NET Framework 4.8
```

La estructura habitual de una nueva librería es:

```text
LibreriaNueva/
├── README.md
├── src/LibreriaNueva/
├── samples/LibreriaNueva.WinFormsDemo/
└── tests/LibreriaNueva.Tests/       ← cuando la lógica lo justifique
```

El demo WinForms es obligatorio. Debe permitir probar visualmente todas las opciones públicas y
parametrizables de la librería, mostrar validaciones y errores relevantes, y presentar el
resultado de la operación. Una demo de consola puede agregarse como complemento, pero no
reemplaza al demo WinForms.

## 4. Inventario de proyectos

### 4.1 LibreriaOCR

| Proyecto | Target | Tipo | Responsabilidad |
|---|---|---|---|
| `LibreriaOCR/src/LibreriaOCR` | `net10.0` | Biblioteca | Fachada `OcrService`, opciones, resultados, detección de formatos, PDF y motor Tesseract |
| `LibreriaOCR/samples/LibreriaOCR.Demo` | `net10.0` | Consola | Ejecución de OCR desde línea de comandos |
| `LibreriaOCR/samples/LibreriaOCR.WinFormsTester` | `net10.0-windows` | WinForms | Prueba manual desde escritorio |

Dependencias directas de la biblioteca: `PdfPig 0.1.16`, `PDFtoImage 5.4.0` y `TesseractOCR
5.5.2`. La carpeta `src/LibreriaOCR/tessdata` contiene los modelos de idioma y se copia al
resultado de compilación como contenido. El consumidor debe conservar `tessdata` junto al
ejecutable final.

La API acepta ruta, `Stream` y `byte[]`, en forma síncrona y asíncrona. El servicio permite
configurar idiomas, ruta de datos, DPI, concurrencia y reglas de PDF mediante `OcrOptions`.

### 4.2 LibreriaQR

| Proyecto | Target | Tipo | Responsabilidad |
|---|---|---|---|
| `LibreriaQR/src/LibreriaQR` | `net10.0` | Biblioteca | Generación parametrizable de códigos QR, payloads tipados, logo y salidas PNG/base64/data-uri |
| `LibreriaQR/samples/LibreriaQR.Demo` | `net10.0` | Consola | Demo existente para pruebas manuales; debe complementarse o migrarse a WinForms para cumplir el estándar de la solución |

Dependencias directas: `QRCoder 1.8.0` y `SixLabors.ImageSharp 2.1.13`. La biblioteca está
organizada alrededor de `ContenidoQR`, tipos de contenido, `OpcionesQR`, `OpcionesLogo`, nivel
de corrección y `ResultadoQR`. La entrada de esta librería en el `.slnx` está pendiente de
confirmación junto con la carpeta que actualmente no tiene seguimiento de Git.

### 4.3 LibreriaTokens

| Proyecto | Target | Tipo | Responsabilidad |
|---|---|---|---|
| `LibreriaTokens/src/LibreriaTokens` | `net10.0` | Biblioteca | Normalización de texto y estimación aproximada de tokens |
| `LibreriaTokens/samples/LibreriaTokens.Demo` | `net10.0` | Consola | Pruebas de mesa y demostración |

No tiene dependencias NuGet directas. Toda la configuración funcional está expuesta en
`OpcionesNormalizacion`; `NormalizadorTexto` y `EstimadorTokens` son estáticos y thread-safe.
La estimación de tokens es aproximada, no debe utilizarse como medición de facturación.

### 4.4 MarkItDown para .NET 10

| Proyecto | Target | Tipo | Responsabilidad |
|---|---|---|---|
| `MarkItDown/src/MarkItDown.Core` | `net10.0` | Núcleo | Contratos, opciones, resultados, limpieza y excepciones |
| `MarkItDown/src/MarkItDown.Converters` | `net10.0` | Infraestructura | Convertidores DOCX y PDF a Markdown |
| `MarkItDown/src/MarkItDown` | `net10.0` | Fachada | API pública `MarkItDownConverter` y registro opcional en DI |
| `MarkItDown/app/MarkItDown.App` | `net10.0-windows7.0` | WinForms | Aplicación de escritorio |
| `MarkItDown/samples/MarkItDown.Sample` | `net10.0` | Consola | Ejemplo de uso |
| `MarkItDown/tests/MarkItDown.Tests` | `net10.0` | xUnit | Pruebas de conversión, fachada, limpieza y regresiones |

Dependencias de producción: `DocumentFormat.OpenXml 3.5.1`, `PdfPig 0.1.15` y
`Microsoft.Extensions.DependencyInjection.Abstractions 10.0.10`. La configuración común de
esta variante está en `MarkItDown/Directory.Build.props`.

### 4.5 MarkItDown para .NET Framework 4.8

La carpeta `MarkItDown/framework48` mantiene los mismos niveles lógicos y una API funcionalmente
equivalente, pero con proyectos independientes:

```text
framework48/
├── src/MarkItDown.Core
├── src/MarkItDown.Converters
├── src/MarkItDown
├── app/MarkItDown.App
├── samples/MarkItDown.Sample
└── tests/MarkItDown.Tests
```

`framework48/Directory.Build.props` fija `TargetFramework=net48`, habilita el lenguaje moderno,
agrega `Microsoft.NETFramework.ReferenceAssemblies 1.0.3` y genera binding redirects. Sus
dependencias principales están fijadas a `DocumentFormat.OpenXml 3.5.1`, `PdfPig 0.1.15` y
`Microsoft.Extensions.DependencyInjection(.Abstractions) 8.0.x`; las pruebas usan xUnit 2.9.x.

La variante existe para aplicaciones legadas. No se debe mezclar una referencia `net48` con una
aplicación `net10.0`; si una funcionalidad debe existir en ambas, se implementa y verifica en las
dos ramas de la biblioteca.

## 5. Arquitectura y reglas de diseño

### 5.1 Capas

Los proyectos nuevos deben organizarse por responsabilidades, con dependencias apuntando hacia
el núcleo y no al contrario:

```text
Aplicación / Demo / API
          ↓
Fachada o servicios de aplicación
          ↓
Infraestructura / adaptadores externos
          ↓
Core: contratos, modelos, opciones y excepciones
```

Reglas prácticas:

- `Core` no conoce NuGet, UI, sistema de archivos ni proveedores concretos salvo que sea parte
  inseparable del dominio.
- La fachada expone una API pequeña y estable; el consumidor no necesita conocer las capas
  internas.
- Las integraciones externas se encapsulan detrás de interfaces cuando exista posibilidad de
  sustituir el proveedor.
- Las demos sirven para uso manual y ejemplos; no deben contener lógica de negocio que falte en
  la biblioteca.
- Toda librería nueva debe incluir un demo WinForms en `samples/`. El formulario debe cubrir
  todas las opciones configurables de la API, incluyendo valores predeterminados, casos límite,
  entrada inválida y visualización del resultado generado.
- La UI no debe alojar reglas de dominio ni acceder directamente a implementaciones internas.

### 5.2 Parametrización

La configuración que pueda variar por consumidor debe exponerse mediante una clase de opciones
tipada, con valores predeterminados seguros y documentación XML. Evitar constantes dispersas y
`if` específicos para un único cliente o caso de prueba.

Se recomienda:

- constructor o método de fábrica para inyectar dependencias;
- opciones inmutables cuando no se requiera modificación posterior;
- interfaces para motores, convertidores o proveedores reemplazables;
- validación temprana con mensajes claros en español;
- métodos síncronos y asíncronos solo cuando ambos aporten valor real;
- `CancellationToken` en operaciones I/O nuevas que puedan ser largas;
- no introducir configuración global mutable ni estado estático innecesario.

### 5.3 Código y API

- Lenguaje preferido: C#; no agregar otros lenguajes sin acuerdo explícito.
- Nombres de tipos y miembros públicos en PascalCase; variables y parámetros en camelCase.
- Namespace raíz igual al nombre de la biblioteca.
- Nullable habilitado y contratos de nulabilidad explícitos.
- Preferir tipos pequeños, `sealed` cuando no exista intención de herencia y records para datos
  inmutables.
- Evitar APIs públicas que expongan tipos internos o dependencias que el consumidor no pueda
  resolver.
- Las excepciones públicas deben ser específicas y documentar su causa.
- Toda API pública nueva debe tener `<summary>` y ejemplos en el README de la librería.
- Comentarios de implementación solo cuando expliquen una decisión no obvia; la documentación
  no debe reemplazar nombres claros.

## 6. Compilar, probar y ejecutar

Desde la raíz del repositorio:

```powershell
dotnet restore .\LibreriasWanaka.slnx
dotnet build .\LibreriasWanaka.slnx -c Release
dotnet test .\LibreriasWanaka.slnx -c Release
```

Para una ejecución enfocada:

```powershell
dotnet build .\LibreriasWanaka.slnx -c Debug
dotnet test .\MarkItDown\tests\MarkItDown.Tests\MarkItDown.Tests.csproj
dotnet run --project .\LibreriaOCR\samples\LibreriaOCR.Demo
dotnet run --project .\LibreriaTokens\samples\LibreriaTokens.Demo
dotnet run --project .\MarkItDown\samples\MarkItDown.Sample
```

Para proyectos WinForms, ejecutar desde Windows:

```powershell
dotnet run --project .\LibreriaOCR\samples\LibreriaOCR.WinFormsTester
dotnet run --project .\MarkItDown\app\MarkItDown.App
```

Antes de entregar cambios se debe verificar, como mínimo, que compilan los proyectos afectados,
que las pruebas existentes pasan y que el README de la librería refleja los cambios de API o
requisitos. También se debe abrir el demo WinForms y comprobar que cada opción pública relevante
puede configurarse desde la interfaz y produce el resultado esperado.

## 7. Cómo agregar una nueva librería

1. Crear una carpeta de primer nivel con nombre `LibreriaNombre`.
2. Crear la biblioteca en `src/LibreriaNombre/LibreriaNombre.csproj` con `TargetFramework=net10.0`.
3. Agregar `README.md`, documentación XML y opciones parametrizables.
4. Crear obligatoriamente `samples/LibreriaNombre.WinFormsDemo` con target
   `net10.0-windows`. El formulario debe exponer todas las opciones públicas de la librería,
   permitir ejecutar los escenarios principales y mostrar resultados, advertencias y errores.
5. Crear `tests/LibreriaNombre.Tests` cuando haya lógica no trivial, formatos, reglas de negocio
   o regresiones que deban protegerse.
6. Registrar todos los proyectos en `LibreriasWanaka.slnx`, agrupándolos por `src`, `samples`,
   `tests` y `app`.
7. Agregar la librería a la tabla de contenido de `README.md` y actualizar este documento con
   target, capas, dependencias, plataformas y restricciones.
8. Ejecutar restore, build y tests; registrar cualquier limitación o dependencia nativa.

Ejemplo de bloque para el `.slnx`:

```xml
<Folder Name="/LibreriaNombre/src/">
  <Project Path="LibreriaNombre/src/LibreriaNombre/LibreriaNombre.csproj" />
</Folder>
<Folder Name="/LibreriaNombre/samples/">
  <Project Path="LibreriaNombre/samples/LibreriaNombre.WinFormsDemo/LibreriaNombre.WinFormsDemo.csproj" />
</Folder>
<Folder Name="/LibreriaNombre/tests/">
  <Project Path="LibreriaNombre/tests/LibreriaNombre.Tests/LibreriaNombre.Tests.csproj" />
</Folder>
```

## 8. Consumo desde otra solución

Dentro del mismo repositorio, preferir `ProjectReference` a la DLL compilada:

```powershell
dotnet add .\MiProyecto.csproj reference `
  ..\Librerias\LibreriaTokens\src\LibreriaTokens\LibreriaTokens.csproj
```

Para distribución externa, compilar en `Release` y copiar la salida completa de la biblioteca,
incluyendo dependencias transitivas y contenido adicional. En particular:

- `LibreriaOCR`: conservar `tessdata` y los binarios nativos publicados por NuGet.
- `MarkItDown`: conservar `MarkItDown.dll`, `MarkItDown.Core.dll`,
  `MarkItDown.Converters.dll` y todas las dependencias de la carpeta de salida.
- `LibreriaQR` y `LibreriaTokens`: comprobar el modo de publicación y las dependencias NuGet
  declaradas antes de copiar DLL manualmente.

Para consumidores nuevos, es preferible crear paquetes NuGet internos cuando el versionado y la
distribución de DLL sueltas empiecen a ser difíciles de controlar.

## 9. Dependencias y seguridad de mantenimiento

- Revisar licencias y compatibilidad antes de actualizar paquetes.
- No subir secretos, archivos de entrada de clientes, binarios generados ni carpetas `bin/`,
  `obj/` o `.vs/`.
- Mantener las versiones explícitas y actualizar una familia de dependencias relacionada de forma
  coordinada.
- Si una dependencia es nativa, documentar RID, arquitectura soportada, archivos que deben
  copiarse y comportamiento esperado cuando falten.
- No cambiar target framework, proveedor principal, contrato público o esquema de carpetas sin
  actualizar esta guía y el README afectado.

## 10. Registro de cambios de la solución

Este apartado debe actualizarse junto con cambios estructurales, de herramientas o de requisitos.
No es necesario registrar aquí cada corrección interna de una librería; esos cambios pertenecen
al README o historial de la librería correspondiente.

| Fecha | Cambio |
|---|---|
| 2026-09-20 | Creación de esta guía técnica. Inventario de OCR, QR, Tokens, MarkItDown net10 y MarkItDown net48; documentados requisitos, capas y reglas de ampliación. |
| 2026-09-20 | Se establece como obligatorio un demo WinForms por librería, con cobertura de todas sus opciones públicas y parametrizables. |
