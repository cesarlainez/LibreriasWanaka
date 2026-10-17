using System.Diagnostics;
using System.Text;
using LibreriaOCR;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("Uso: LibreriaOCR.Demo [--manuscrito] <archivo1> [archivo2] ...");
    Console.WriteLine("  (sin flag)      extrae el texto por OCR");
    Console.WriteLine("  --manuscrito    detecta si el documento tiene escritura a mano");
    Console.WriteLine("Formatos: PDF, PNG, JPG/JPEG, TIF/TIFF");
    return;
}

// Modo manuscrito: primer argumento --manuscrito / -m / --hw.
var modoManuscrito = args[0] is "--manuscrito" or "-m" or "--hw";
var archivos = modoManuscrito ? args[1..] : args;
if (archivos.Length == 0)
{
    Console.WriteLine("Falta indicar al menos un archivo.");
    return;
}

if (modoManuscrito)
{
    DetectarManuscrito(archivos);
    return;
}

// Una sola instancia reutilizable para todos los archivos (carga el idioma una vez).
using var ocr = new OcrService(new OcrOptions { Languages = "spa" });

var globalWatch = Stopwatch.StartNew();
var ok = 0;
var failed = 0;

foreach (var path in archivos)
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

// ------------------------------------------------------------------ manuscrito

static void DetectarManuscrito(string[] rutas)
{
    using var detector = new HandwritingDetector();
    foreach (var path in rutas)
    {
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"Archivo: {path}");
        try
        {
            var r = detector.Detect(path);
            Console.WriteLine(r.HasHandwriting
                ? $"SÍ tiene escritura a mano | Score máx: {r.MaxScore:0.#} | " +
                  $"Regiones: {r.Regions.Count} | Páginas: {r.Pages.Count} | Tiempo: {r.Duration.TotalSeconds:0.00}s"
                : $"No se detectó escritura a mano | Score máx: {r.MaxScore:0.#} | " +
                  $"Páginas: {r.Pages.Count} | Tiempo: {r.Duration.TotalSeconds:0.00}s");

            foreach (var region in r.Regions.OrderByDescending(x => x.Score))
                Console.WriteLine($"   - pág {region.PageNumber}  ·  score {region.Score:0.#}  ·  " +
                                  $"({region.X}, {region.Y}, {region.Width}×{region.Height})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
        }
        Console.WriteLine();
    }
}
