# LibreriaTokens — Guía para agentes IA

> Documento pensado para que un asistente IA (Copilot, Cursor, Claude, GPT, etc.) pueda usar la librería sin leer el código fuente. Contiene el API completo, ejemplos ejecutables y las reglas de comportamiento verificadas por la prueba de mesa.

---

## 1. Identidad de la librería

| Campo | Valor |
|---|---|
| Nombre del paquete / assembly | `LibreriaTokens` |
| Namespace único | `LibreriaTokens` |
| Target framework | `net10.0` |
| Nullable | `enable` |
| Documentación XML | generada (visible en IntelliSense) |
| Dependencias NuGet | **ninguna** (solo `System.*`) |
| Distribución | `ProjectReference` dentro del monorepo `LibreriasWanaka`, o `LibreriaTokens.dll` copiada al `bin` del consumidor |
| Thread-safety | Todos los tipos públicos son thread-safe |
| Complejidad | `Normalizar` es **O(n)** en tiempo y memoria |
| Rango de excepciones | Ninguno de los métodos públicos lanza; entradas `null` / vacías → `string.Empty` o `0` |

## 2. Para qué sirve (en una frase)

**Reducir tokens de entrada al llamar LLMs** (a) limpiando whitespace redundante del prompt y (b) estimando cuántos tokens tendrá antes de enviarlo.

## 3. Cuándo usarla / cuándo NO usarla

Usala cuando:

- El texto viene de **OCR, MarkItDown, PDF, DOCX, correos** y trae ruido (líneas en blanco, tabs, padding de columnas, NBSP).
- Necesitás **loggear el tamaño** del prompt antes de mandarlo a OpenAI / Anthropic.
- Querés **avisar** al usuario "vas al 80 % del contexto" sin pagar la llamada real.

**No** la uses cuando:

- El texto es **código fuente** o Markdown con bloques de código: el whitespace ahí es semántico. Desactivá `ColapsarEspaciosInternos`, `ConvertirTabuladores` y `QuitarEspaciosAlFinalDeLinea` o **directamente no la uses** en esa parte del prompt.
- Necesitás el **conteo de tokens exacto para facturación**: usá el `usage` que devuelve el proveedor. `EstimadorTokens` tiene un margen de **±15 %**.

## 4. API pública completa

Namespace: `LibreriaTokens`. Todas las clases son `public sealed`; los estáticos son `public static`.

### 4.1 `NormalizadorTexto` (static)

```csharp
public static class NormalizadorTexto
{
    public static string Normalizar(
        string? texto,
        OpcionesNormalizacion? opciones = null);

    public static ResultadoNormalizacion NormalizarConMetricas(
        string? texto,
        OpcionesNormalizacion? opciones = null);
}
```

Contrato:

- `texto = null` → devuelve `""`.
- `texto = ""` → devuelve `""`.
- `texto` solo con whitespace → devuelve `""` (por `RecortarBloque = true`).
- `opciones = null` → usa `new OpcionesNormalizacion()` (defaults agresivos).
- **Nunca lanza excepciones.**

### 4.2 `OpcionesNormalizacion`

```csharp
public sealed class OpcionesNormalizacion
{
    public bool ColapsarSaltosDeLinea      { get; set; } = true;
    public int  MaximoSaltosConsecutivos   { get; set; } = 2;
    public bool ColapsarEspaciosInternos   { get; set; } = true;
    public bool ConvertirTabuladores       { get; set; } = true;
    public int  EspaciosPorTab             { get; set; } = 1;
    public bool QuitarEspaciosAlFinalDeLinea { get; set; } = true;
    public bool RecortarBloque             { get; set; } = true;
    public bool NormalizarWhitespaceUnicode{ get; set; } = true;
}
```

### 4.3 `ResultadoNormalizacion`

```csharp
public sealed class ResultadoNormalizacion
{
    public string Texto                 { get; init; }  // nunca null
    public int    CaracteresOriginales  { get; init; }
    public int    CaracteresResultantes { get; init; }
    public int    CaracteresAhorrados   { get; }        // Originales - Resultantes
    public double PorcentajeReduccion   { get; init; }  // 0..100
}
```

### 4.4 `EstimadorTokens` (static)

```csharp
public static class EstimadorTokens
{
    public static int Estimar(
        string? texto,
        FamiliaModelo modelo = FamiliaModelo.Generico);

    public static int EstimarCaracteres(
        int caracteres,
        FamiliaModelo modelo = FamiliaModelo.Generico);
}
```

Contrato:

- `texto = null` o `""` → devuelve `0`.
- `caracteres <= 0` → devuelve `0`.
- **Nunca lanza.** Precisión ±15 %.

Fórmula interna (por si un agente necesita reproducirla):

```
estimacion = max(caracteres / 4, palabras * 0.75)   ← palabras = grupos de \S+
tokens     = ceil(estimacion * factor(familia))
```

### 4.5 `FamiliaModelo` (enum)

```csharp
public enum FamiliaModelo
{
    Generico        = 0,   // factor 1.00
    OpenAiGpt4      = 1,   // factor 1.00 (gpt-4o, gpt-4.1, gpt-5, o200k_base)
    OpenAiGpt35     = 2,   // factor 1.10 (cl100k_base, penaliza espanol)
    AnthropicClaude = 3,   // factor 1.00
}
```

## 5. Reglas del normalizador (con entrada → salida)

Todas verificadas por la prueba de mesa del proyecto `LibreriaTokens.Demo`. `⏎` = `\n`.

| Regla | Opción que la controla | Entrada | Salida |
|---|---|---|---|
| Colapsa 3+ saltos a 2 | `ColapsarSaltosDeLinea`, `MaximoSaltosConsecutivos` | `hola⏎⏎⏎⏎⏎mundo` | `hola⏎⏎mundo` |
| Colapsa espacios internos | `ColapsarEspaciosInternos` | `col1    col2` | `col1 col2` |
| Quita trailing whitespace por línea | `QuitarEspaciosAlFinalDeLinea` | `linea   ⏎sig` | `linea⏎sig` |
| Normaliza CRLF y CR sueltos | (siempre activo) | `uno\r\ndos\rtres⏎fin` | `uno⏎dos⏎tres⏎fin` |
| NBSP (`\u00A0`) → espacio | `NormalizarWhitespaceUnicode` | `hola\u00A0mundo` | `hola mundo` |
| ZWSP (`\u200B`), BOM, ZWJ → eliminado | `NormalizarWhitespaceUnicode` | `hola\u200Bmundo` | `holamundo` |
| Tab → N espacios | `ConvertirTabuladores`, `EspaciosPorTab` | `col1\tcol2` (default `EspaciosPorTab = 1`, combinado con colapso interno) | `col1 col2` |
| Recorta bloque completo | `RecortarBloque` | `⏎⏎hola⏎⏎` | `hola` |
| Solo whitespace → vacío | `RecortarBloque` | `   ` | `` (vacío) |
| `null` → vacío | contrato duro | `null` | `` (vacío) |

## 6. Escenarios copy-paste

### 6.1 Normalizar y mandar a un LLM

```csharp
using LibreriaTokens;

string prompt = File.ReadAllText("prompt-crudo.txt");
string limpio = NormalizadorTexto.Normalizar(prompt);

// mandalo al LLM en vez del original
await client.Chat.CompleteAsync(new { model = "gpt-4o", input = limpio });
```

### 6.2 Loggear el ahorro

```csharp
var r = NormalizadorTexto.NormalizarConMetricas(prompt);
_logger.LogInformation(
    "Prompt {orig} -> {res} chars ({pct:0.0}% menos, ~{tokens} tokens)",
    r.CaracteresOriginales,
    r.CaracteresResultantes,
    r.PorcentajeReduccion,
    EstimadorTokens.Estimar(r.Texto, FamiliaModelo.OpenAiGpt4));
```

### 6.3 Cortar antes de mandar si el prompt es enorme

```csharp
const int LimiteTokens = 120_000;

string limpio = NormalizadorTexto.Normalizar(prompt);
int estimados = EstimadorTokens.Estimar(limpio, FamiliaModelo.AnthropicClaude);

if (estimados > LimiteTokens)
{
    _logger.LogWarning("Prompt estimado en {t} tokens; se rechaza sin llamar al modelo.", estimados);
    return TooLarge();
}
```

### 6.4 Preservar el whitespace de bloques de código

Si tu prompt mezcla prosa con snippets de código, **normalizá solo la prosa**:

```csharp
string prosa   = obtenerParteDeProsa();   // texto explicativo
string codigo  = obtenerCodigo();          // no tocar

string ensamblado =
    NormalizadorTexto.Normalizar(prosa) +
    "\n\n```csharp\n" + codigo + "\n```\n";
```

O usá opciones conservadoras que no alteren el layout:

```csharp
var opciones = new OpcionesNormalizacion
{
    ColapsarEspaciosInternos = false,
    ConvertirTabuladores     = false,
    QuitarEspaciosAlFinalDeLinea = false,
};
```

### 6.5 Integración con MarkItDown y LibreriaOCR (mismo monorepo)

```csharp
using MarkItDown;
using LibreriaOCR;
using LibreriaTokens;

string ExtraerYNormalizar(string ruta) => Path.GetExtension(ruta).ToLowerInvariant() switch
{
    ".pdf" or ".docx" => NormalizadorTexto.Normalizar(
                            new MarkItDownConverter().Convert(ruta).Markdown),
    ".png" or ".jpg" or ".tif" or ".tiff" =>
                         NormalizadorTexto.Normalizar(
                            OcrService.ExtractText(ruta).Text),
    _ => throw new NotSupportedException(ruta),
};
```

## 7. Registro en DI (opcional)

`LibreriaTokens` no expone extensiones para `IServiceCollection` porque **no hace falta**: los métodos son estáticos. Si tu equipo prefiere inyectarlos, envolvelos en una interfaz local:

```csharp
public interface INormalizadorPrompt
{
    string Normalizar(string texto);
    int    Estimar(string texto, FamiliaModelo modelo);
}

public sealed class NormalizadorPrompt : INormalizadorPrompt
{
    public string Normalizar(string texto) => NormalizadorTexto.Normalizar(texto);
    public int    Estimar(string texto, FamiliaModelo modelo) => EstimadorTokens.Estimar(texto, modelo);
}

services.AddSingleton<INormalizadorPrompt, NormalizadorPrompt>();
```

## 8. Cómo referenciar la librería

### 8.1 Desde el mismo monorepo `LibreriasWanaka`

Ya está registrada en `LibreriasWanaka.slnx`; en otro proyecto de la misma solución basta con:

```bash
dotnet add MiProyecto.csproj reference "..\..\LibreriaTokens\src\LibreriaTokens\LibreriaTokens.csproj"
```

### 8.2 Desde un repo hermano bajo `source\repos\`

```bash
dotnet add MiApi.csproj reference "..\LibreriasWanaka\LibreriaTokens\src\LibreriaTokens\LibreriaTokens.csproj"
```

### 8.3 Como DLL compilada

```bash
dotnet build LibreriasWanaka.slnx -c Release
# copiar del bin:
#   LibreriasWanaka\LibreriaTokens\src\LibreriaTokens\bin\Release\net10.0\LibreriaTokens.dll
```

Añadila como *Reference* en el proyecto consumidor. **No arrastra ninguna transitiva de NuGet.**

## 9. Errores y comportamientos que un agente NO debe suponer

Un agente que vaya a generar código con esta librería debe evitar estos errores comunes:

- ❌ Suponer que `Normalizar` lanza `ArgumentNullException` con `null`. **No lanza; devuelve `""`.**
- ❌ Suponer que existe una versión asíncrona (`NormalizarAsync`). **No existe.** El método es puramente CPU-bound y O(n). Si lo llamás desde un hilo de UI y el texto es enorme (>1 MB), envolvelo en `Task.Run`.
- ❌ Suponer que `EstimadorTokens.Estimar` devuelve el número exacto de tokens del proveedor. **Es ±15 %.**
- ❌ Suponer que `OpcionesNormalizacion` es un `record` inmutable. **Es una clase mutable con setters públicos** (mismo estilo que `OcrOptions` en LibreriaOCR).
- ❌ Instanciar `NormalizadorTexto` o `EstimadorTokens` con `new`. **Son static classes**; los métodos se llaman por tipo.
- ❌ Configurar `MaximoSaltosConsecutivos = 0` esperando eliminar todos los saltos. La implementación lo eleva a mínimo `1` (poner `0` no colapsa a un solo bloque).
- ❌ Depender del orden en que se aplican las reglas. La normalización es una sola pasada; las reglas están diseñadas para conmutar correctamente. Cambiar el orden en tu propio código pos-normalización puede dar resultados diferentes.

## 10. Trazabilidad del comportamiento

Cada regla de la sección 5 tiene un caso PASA/FALLA en `LibreriaTokens/samples/LibreriaTokens.Demo/Program.cs`. Un agente que dude sobre el comportamiento puede correr:

```bash
dotnet run --project LibreriaTokens/samples/LibreriaTokens.Demo
```

Salida esperada:

```
Prueba de mesa de LibreriaTokens
================================
  [PASA ] null devuelve string vacio
  [PASA ] "" devuelve string vacio
  [PASA ] solo espacios devuelve string vacio (recorta bloque)
  [PASA ] colapsa 5 saltos a 2
  [PASA ] colapsa espacios internos
  [PASA ] quita espacios al final de linea
  [PASA ] normaliza CRLF y CR sueltos
  [PASA ] NBSP y ZWSP: NBSP a espacio, ZWSP fuera
  [PASA ] 100.000 chars no explota y sigue siendo un solo bloque
  [PASA ] EstimadorTokens: null da 0, algo razonable da > 0

Resultado: 10/10 PASA
```

Con un archivo real:

```bash
dotnet run --project LibreriaTokens/samples/LibreriaTokens.Demo -- "C:\ruta\prompt.txt"
```

Imprime chars antes/después, tokens antes/después (GPT-4o) y los primeros 500 chars del resultado.

## 11. Referencia rápida (memoriza esto)

```csharp
// 1. Normalizar y mandar al LLM
var limpio = LibreriaTokens.NormalizadorTexto.Normalizar(prompt);

// 2. Estimar antes de mandar
int t = LibreriaTokens.EstimadorTokens.Estimar(limpio, LibreriaTokens.FamiliaModelo.OpenAiGpt4);

// 3. Con métricas para loggear
var r = LibreriaTokens.NormalizadorTexto.NormalizarConMetricas(prompt);
//     r.Texto, r.CaracteresOriginales, r.CaracteresResultantes,
//     r.CaracteresAhorrados, r.PorcentajeReduccion
```

Eso cubre el 95 % de los casos. Todo lo demás son variaciones de `OpcionesNormalizacion`.
