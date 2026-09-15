using System.Text;
using MarkItDown;
using MarkItDown.Core;

namespace MarkItDown.Sample;

internal static class Program
{
    // .NET Framework 4.8: se usa Main clásico en lugar de instrucciones de nivel superior.
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            Console.WriteLine("Uso: MarkItDown.Sample <archivo.docx|archivo.pdf> [salida.md]");
            Console.WriteLine("Convierte un documento Word o PDF a Markdown (sin IA, 100% local).");
            return 1;
        }

        var inputPath = args[0];
        var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".md");
        var imagesDirectory = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".", "imagenes");

        var converter = new MarkItDownConverter();

        try
        {
            var result = converter.ConvertToFile(inputPath, outputPath, new ConversionOptions
            {
                ExtractImages = true,
                ImageOutputDirectory = imagesDirectory,
                ImageLinkPrefix = "imagenes/",
            });

            Console.WriteLine($"✔ Convertido: {Path.GetFullPath(outputPath)}");
            if (result.Title is not null)
            {
                Console.WriteLine($"  Título: {result.Title}");
            }

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  Advertencia: {warning}");
            }

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"✖ {ex.Message}");
            return 2;
        }
        catch (MarkdownConversionException ex)
        {
            Console.Error.WriteLine($"✖ {ex.Message}");
            return 3;
        }
    }
}
