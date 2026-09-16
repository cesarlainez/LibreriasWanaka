# LibreriaTokens

> Parte de [**LibreriasWanaka**](../README.md).
> ¿Sos un asistente IA (Copilot / Cursor / Claude / GPT)? Leé [`AGENTS.md`](AGENTS.md), pensado exactamente para vos.

Librería en **.NET 10** con dos utilidades para trabajar contra LLMs sin desperdiciar contexto:

1. **`NormalizadorTexto`** — reduce el whitespace redundante (líneas en blanco, padding de columnas de PDF/OCR, espacios exóticos de Unicode) **antes** de mandar el texto al modelo. Baja el consumo de tokens de entrada sin perder información.
2. **`EstimadorTokens`** — aproxima cuántos tokens ocupará un texto, sin llamar al proveedor. Sirve para loggear el tamaño del prompt o decidir si hay que resumir antes de enviar.

**Cero dependencias NuGet.** Solo `System.*`. Podés referenciarla sin arrastrar transitivos nuevos a tu proyecto.

## Instalar en tu proyecto

Referencia de proyecto (recomendado si tu repo vive como hermano de `LibreriasWanaka` bajo `source\repos\`):

```bash
dotnet add MiApi.csproj reference "..\LibreriasWanaka\LibreriaTokens\src\LibreriaTokens\LibreriaTokens.csproj"
```

## Uso — normalizar antes de enviar al LLM

```csharp
using LibreriaTokens;

var textoDelPdf = File.ReadAllText("poliza.txt");
var limpio = NormalizadorTexto.Normalizar(textoDelPdf);

// enviar 'limpio' al LLM en vez del original
```

Con métricas para loggear el ahorro:

```csharp
var r = NormalizadorTexto.NormalizarConMetricas(textoDelPdf);
_logger.LogInformation(
    "Prompt normalizado: {orig} -> {res} chars ({pct:0.0}% menos)",
    r.CaracteresOriginales, r.CaracteresResultantes, r.PorcentajeReduccion);
```

Todas las reglas se controlan con `OpcionesNormalizacion` (defaults agresivos):

| Opción | Default | Efecto |
|---|---|---|
| `ColapsarSaltosDeLinea` + `MaximoSaltosConsecutivos` | `true`, `2` | `\n\n\n…` → `\n\n` (conserva doble salto = párrafo) |
| `ColapsarEspaciosInternos` | `true` | `"a    b"` → `"a b"` (padding de columnas) |
| `ConvertirTabuladores` + `EspaciosPorTab` | `true`, `1` | Tabs a espacios (no aportan layout en prosa) |
| `QuitarEspaciosAlFinalDeLinea` | `true` | Elimina trailing whitespace de cada línea |
| `RecortarBloque` | `true` | Trim del texto completo (arriba y abajo) |
| `NormalizarWhitespaceUnicode` | `true` | NBSP/thin space → espacio; ZWSP/BOM eliminados |

`Normalizar(null)` y `Normalizar("")` devuelven `string.Empty`. **Nunca lanza.**

## Uso — estimar tokens sin llamar al proveedor

```csharp
using LibreriaTokens;

var texto = File.ReadAllText("prompt.txt");
int aprox = EstimadorTokens.Estimar(texto, FamiliaModelo.OpenAiGpt4);

if (aprox > 80_000)
    _logger.LogWarning("Prompt cerca del limite de contexto: {tokens} tokens", aprox);
```

Familias soportadas: `OpenAiGpt4` (gpt-4o, gpt-4.1, gpt-5), `OpenAiGpt35` (cl100k_base), `AnthropicClaude` y `Generico`.

> **Aproximado ±15 %.** Sirve para decisiones ("¿resumo antes de enviar?") y para logs, no como métrica de facturación. Si necesitás el número exacto, usá el `usage` que devuelve el proveedor en la respuesta.

## Combinar con MarkItDown y LibreriaOCR

Flujo típico dentro del monorepo cuando queremos meterle un PDF a un LLM:

```csharp
using MarkItDown;
using LibreriaTokens;

var md   = new MarkItDownConverter().Convert(@"C:\docs\poliza.pdf").Markdown;
var lean = NormalizadorTexto.Normalizar(md);      // baja tokens sin perder contenido
int cost = EstimadorTokens.Estimar(lean, FamiliaModelo.AnthropicClaude);
```

Para PDF escaneados sin texto digital, sustituí `MarkItDown` por [`LibreriaOCR`](../LibreriaOCR/README.md).

## Probar

```bash
# Con archivo: imprime chars y tokens antes/despues, y los primeros 500 chars
dotnet run --project samples/LibreriaTokens.Demo -- "C:\docs\prompt.txt"

# Sin argumentos: prueba de mesa (8 casos)
dotnet run --project samples/LibreriaTokens.Demo
```

## Notas

- **Thread-safe**: los dos servicios son métodos estáticos sin estado; se pueden usar desde cualquier hilo o registrar como singleton implícito.
- **O(n)**: `Normalizar` recorre el string una sola vez con `StringBuilder` prealocado.
- Probado con textos de hasta ~5 MB sin degradación cuadrática.
