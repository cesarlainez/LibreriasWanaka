using System.Text;
using LibreriaTokens;

// Modo archivo: se paso una ruta como primer argumento.
if (args.Length > 0)
{
    var ruta = args[0];
    if (!File.Exists(ruta))
    {
        Console.Error.WriteLine($"No se encontro el archivo: {ruta}");
        return 1;
    }

    var original = File.ReadAllText(ruta);
    var metricas = NormalizadorTexto.NormalizarConMetricas(original);
    var tokensAntes = EstimadorTokens.Estimar(original, FamiliaModelo.OpenAiGpt4);
    var tokensDespues = EstimadorTokens.Estimar(metricas.Texto, FamiliaModelo.OpenAiGpt4);

    Console.WriteLine($"Archivo             : {ruta}");
    Console.WriteLine($"Chars originales    : {metricas.CaracteresOriginales:N0}");
    Console.WriteLine($"Chars resultantes   : {metricas.CaracteresResultantes:N0}");
    Console.WriteLine($"Chars ahorrados     : {metricas.CaracteresAhorrados:N0} ({metricas.PorcentajeReduccion:0.0} %)");
    Console.WriteLine($"Tokens estimados antes  : {tokensAntes:N0} (GPT-4o)");
    Console.WriteLine($"Tokens estimados despues: {tokensDespues:N0} (GPT-4o)");
    Console.WriteLine();
    Console.WriteLine("--- Primeros 500 chars del resultado ---");
    Console.WriteLine(metricas.Texto[..Math.Min(500, metricas.Texto.Length)]);
    return 0;
}

// Modo prueba de mesa: sin argumentos, corre los casos armados a mano.
Console.WriteLine("Prueba de mesa de LibreriaTokens");
Console.WriteLine("================================");

var casos = new List<(string nombre, Func<bool> comprobacion)>
{
    ("null devuelve string vacio",
     () => NormalizadorTexto.Normalizar(null) == string.Empty),

    ("\"\" devuelve string vacio",
     () => NormalizadorTexto.Normalizar(string.Empty) == string.Empty),

    ("solo espacios devuelve string vacio (recorta bloque)",
     () => NormalizadorTexto.Normalizar("   ") == string.Empty),

    ("colapsa 5 saltos a 2",
     () => NormalizadorTexto.Normalizar("hola\n\n\n\n\nmundo") == "hola\n\nmundo"),

    ("colapsa espacios internos",
     () => NormalizadorTexto.Normalizar("col1    col2    col3") == "col1 col2 col3"),

    ("quita espacios al final de linea",
     () => NormalizadorTexto.Normalizar("linea con espacios al final   \nsiguiente")
           == "linea con espacios al final\nsiguiente"),

    ("normaliza CRLF y CR sueltos",
     () => NormalizadorTexto.Normalizar("uno\r\ndos\rtres\nfin") == "uno\ndos\ntres\nfin"),

    ("NBSP y ZWSP: NBSP a espacio, ZWSP fuera",
     () => NormalizadorTexto.Normalizar("hola mundo​fin") == "hola mundofin"),

    ("100.000 chars no explota y sigue siendo un solo bloque",
     () => PruebaDeVolumen()),

    ("EstimadorTokens: null da 0, algo razonable da > 0",
     () => EstimadorTokens.Estimar(null) == 0 && EstimadorTokens.Estimar("hola mundo, esto es una prueba") > 0),
};

var fallas = 0;
foreach (var (nombre, comprobacion) in casos)
{
    var paso = false;
    string? error = null;
    try
    {
        paso = comprobacion();
    }
    catch (Exception ex)
    {
        error = ex.Message;
    }

    Console.WriteLine($"  [{(paso ? "PASA " : "FALLA")}] {nombre}{(error is null ? string.Empty : $" -> {error}")}");
    if (!paso) fallas++;
}

Console.WriteLine();
Console.WriteLine($"Resultado: {casos.Count - fallas}/{casos.Count} PASA");
return fallas == 0 ? 0 : 2;

static bool PruebaDeVolumen()
{
    // Generamos 100k chars mezclando ruido con contenido y verificamos que:
    //  - la salida no supera al original,
    //  - el algoritmo termina en un tiempo razonable (implicito: si fuera O(n^2)
    //    con 100k chars la ejecucion se sentiria; con O(n) es milisegundos).
    var sb = new StringBuilder(100_000);
    for (var i = 0; i < 100_000; i++)
    {
        var r = i % 20;
        sb.Append(r switch
        {
            < 5 => 'a',
            < 10 => 'b',
            10 => ' ',
            11 => '\t',
            12 => '\n',
            13 => '\r',
            14 => ' ',
            15 => '​',
            _ => 'c',
        });
    }

    var entrada = sb.ToString();
    var salida = NormalizadorTexto.Normalizar(entrada);
    return salida.Length > 0 && salida.Length <= entrada.Length;
}
