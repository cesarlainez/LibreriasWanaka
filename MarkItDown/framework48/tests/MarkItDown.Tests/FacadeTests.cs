using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MarkItDown.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MarkItDown.Tests;

public class FacadeTests
{
    private static byte[] BuildSimpleDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    [Fact]
    public void Extension_no_soportada_lanza_excepcion_con_extension()
    {
        var converter = new MarkItDownConverter();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var exception = Assert.Throws<UnsupportedFileFormatException>(
            () => converter.Convert(stream, "archivo.xyz"));

        Assert.Equal(".xyz", exception.Extension);
        Assert.Contains(".docx", exception.Message);
        Assert.Contains(".pdf", exception.Message);
    }

    [Fact]
    public void Doc_antiguo_sugiere_guardar_como_docx()
    {
        var converter = new MarkItDownConverter();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var exception = Assert.Throws<UnsupportedFileFormatException>(
            () => converter.Convert(stream, "informe.doc"));

        Assert.Contains(".docx", exception.Message);
    }

    [Fact]
    public void Enruta_por_extension_sin_distinguir_mayusculas()
    {
        var converter = new MarkItDownConverter();
        using var stream = new MemoryStream(BuildSimpleDocx("Hola mundo"));

        var result = converter.Convert(stream, "REPORTE.DOCX");

        Assert.Contains("Hola mundo", result.Markdown);
    }

    [Fact]
    public void Acepta_extension_sin_punto()
    {
        var converter = new MarkItDownConverter();
        using var stream = new MemoryStream(BuildSimpleDocx("Contenido"));

        var result = converter.Convert(stream, "docx");

        Assert.Contains("Contenido", result.Markdown);
    }

    [Fact]
    public void Convierte_archivo_desde_ruta_y_escribe_salida()
    {
        var directory = Path.Combine(Path.GetTempPath(), "markitdown-facade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var inputPath = Path.Combine(directory, "documento.docx");
            var outputPath = Path.Combine(directory, "salida", "documento.md");
            File.WriteAllBytes(inputPath, BuildSimpleDocx("Contenido del documento"));

            var converter = new MarkItDownConverter();
            var result = converter.ConvertToFile(inputPath, outputPath);

            Assert.Contains("Contenido del documento", result.Markdown);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("Contenido del documento", File.ReadAllText(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Archivo_inexistente_lanza_FileNotFound()
    {
        var converter = new MarkItDownConverter();

        Assert.Throws<FileNotFoundException>(() => converter.Convert(@"C:\no\existe\x.docx"));
    }

    [Fact]
    public void Se_puede_registrar_un_convertidor_personalizado()
    {
        var converter = new MarkItDownConverter();
        converter.RegisterConverter(new FakeTxtConverter());
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("hola"));

        var result = converter.Convert(stream, "notas.txt");

        Assert.Equal("hola", result.Markdown);
        Assert.Contains(".txt", converter.SupportedExtensions);
    }

    [Fact]
    public void Se_resuelve_desde_inyeccion_de_dependencias()
    {
        var provider = new ServiceCollection()
            .AddMarkItDown()
            .BuildServiceProvider();

        var converter = provider.GetRequiredService<MarkItDownConverter>();

        Assert.Contains(".docx", converter.SupportedExtensions);
        Assert.Contains(".pdf", converter.SupportedExtensions);
    }

    private sealed class FakeTxtConverter : IDocumentConverter
    {
        public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".txt" };

        public bool CanConvert(string extension) =>
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

        public ConversionResult Convert(Stream input, ConversionOptions? options = null)
        {
            using var reader = new StreamReader(input);
            return new ConversionResult(reader.ReadToEnd());
        }
    }
}
