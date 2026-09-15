using System.Diagnostics;
using System.Text;
using LibreriaOCR;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("Uso: LibreriaOCR.Demo <archivo1> [archivo2] ...");
    Console.WriteLine("Formatos: PDF, PNG, JPG/JPEG, TIF/TIFF");
    return;
}

// Una sola instancia reutilizable para todos los archivos (carga el idioma una vez).
using var ocr = new OcrService(new OcrOptions { Languages = "spa" });

var globalWatch = Stopwatch.StartNew();
var ok = 0;
var failed = 0;

foreach (var path in args)
{
    Console.WriteLine(new string('=', 78));
    Console.WriteLine($"Archivo: {path}");
    try
    {
        var result = ocr.Recognize(path);
        var conf = result.MeanConfidence < 0 ? "n/a" : $"{result.MeanConfidence:0.#}%";
        Console.WriteLine($"Origen: {result.SourceKind} | Páginas: {result.PageCount} | " +
                          $"Confianza: {conf} | Tiempo: {result.Duration.TotalSeconds:0.00}s | " +
                          $"Caracteres: {result.Text.Length}");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine(result.Text.Trim());
        ok++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"   -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        failed++;
    }
    Console.WriteLine();
}

globalWatch.Stop();
Console.WriteLine(new string('=', 78));
Console.WriteLine($"Listos: {ok}  |  Con error: {failed}  |  Tiempo total: {globalWatch.Elapsed.TotalSeconds:0.00}s");
