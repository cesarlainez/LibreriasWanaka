using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MarkItDown.Converters.Docx;
using MarkItDown.Converters.Pdf;
using MarkItDown.Core;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace MarkItDown.Tests;

/// <summary>Regresiones de los defectos encontrados en la revisión adversarial.</summary>
public class ReviewRegressionTests
{
    private static MemoryStream BuildDocx(Action<MainDocumentPart, Body> configure)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);
            configure(main, body);
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static string ConvertDocx(MemoryStream docx)
    {
        return new DocxToMarkdownConverter().Convert(docx).Markdown;
    }

    private static TableCell Cell(string text) => new(new Paragraph(new Run(new Text(text))));

    // ------------------------- DOCX -------------------------

    [Fact]
    public void Campo_fldSimple_conserva_su_texto()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new Run(new Text("Emitido el ") { Space = SpaceProcessingModeValues.Preserve }),
                new SimpleField(new Run(new Text("20/07/2026"))) { Instruction = " DATE " }));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains("Emitido el 20/07/2026", markdown);
    }

    [Fact]
    public void Fila_de_tabla_dentro_de_content_control_no_se_pierde()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Table(
                new TableRow(Cell("Encabezado")),
                new SdtRow(new SdtContentRow(new TableRow(Cell("Fila en control"))))));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains("| Fila en control |", markdown);
    }

    [Fact]
    public void Lista_definida_por_estilo_genera_viñetas()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }) { LevelIndex = 0 })
                { AbstractNumberId = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });

            var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new Style(
                    new StyleParagraphProperties(new NumberingProperties(new NumberingId { Val = 1 })))
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "ListBullet",
                });

            body.Append(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "ListBullet" }),
                new Run(new Text("Elemento por estilo"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains("- Elemento por estilo", markdown);
    }

    [Fact]
    public void Salto_de_pagina_no_pega_los_textos()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new Run(new Text("fin de sección")),
                new Run(new Break { Type = BreakValues.Page }),
                new Run(new Text("Nueva sección"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.DoesNotContain("secciónNueva", markdown);
        Assert.Contains("fin de sección", markdown);
        Assert.Contains("Nueva sección", markdown);
    }

    [Fact]
    public void Nota_al_pie_se_conserva_como_footnote()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var footnotesPart = main.AddNewPart<FootnotesPart>();
            footnotesPart.Footnotes = new Footnotes(
                new Footnote(new Paragraph(new Run(new Text("Aplica solo a pólizas vigentes")))) { Id = 2 });

            body.Append(new Paragraph(
                new Run(new Text("Ver condiciones generales")),
                new Run(new FootnoteReference { Id = 2 })));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains("Ver condiciones generales[^F2]", markdown);
        Assert.Contains("[^F2]: Aplica solo a pólizas vigentes", markdown);
    }

    [Fact]
    public void Listas_numeradas_distintas_no_se_fusionan()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Decimal }) { LevelIndex = 0 })
                { AbstractNumberId = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 2 });

            body.Append(Item("Primero", 1));
            body.Append(Item("Segundo", 1));
            body.Append(new Paragraph());
            body.Append(Item("Uno", 2));
            body.Append(Item("Dos", 2));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains("1. Segundo\n\n<!-- -->\n\n1. Uno", markdown);
    }

    private static Paragraph Item(string text, int numId)
    {
        return new Paragraph(
            new ParagraphProperties(new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId })),
            new Run(new Text(text)));
    }

    [Fact]
    public void Elemento_anidado_sin_padre_no_se_vuelve_bloque_de_codigo()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }) { LevelIndex = 1 })
                { AbstractNumberId = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });

            body.Append(new Paragraph(new Run(new Text("Intro"))));
            body.Append(new Paragraph(
                new ParagraphProperties(new NumberingProperties(
                    new NumberingLevelReference { Val = 1 },
                    new NumberingId { Val = 1 })),
                new Run(new Text("Elemento huérfano"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.DoesNotContain("    - ", markdown);
        Assert.Contains(markdown.Split('\n'), line => line == "- Elemento huérfano");
    }

    [Fact]
    public void Tildes_literales_no_se_vuelven_tachado()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(new Run(new Text("antes ~~100~~ ahora 80"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains(@"\~\~100\~\~", markdown);
    }

    [Fact]
    public void Parrafo_de_guiones_no_se_vuelve_regla_horizontal()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(new Run(new Text("---"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains(@"\---", markdown);
    }

    [Fact]
    public void Linea_interna_tras_salto_suave_tambien_se_escapa()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new Run(new Text("Requisitos:")),
                new Run(new Break()),
                new Run(new Text("- uno"))));
        });

        var markdown = ConvertDocx(docx);

        Assert.Contains(@"\- uno", markdown);
    }

    // ------------------------- PDF -------------------------

    [Fact]
    public void Pdf_escapa_caracteres_de_markdown_en_el_texto()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("Tarifa: 5*3 y 2*4 unidades por cada plan contratado.", 11, new PdfPoint(40, 700), regular);
        page.AddText("El resto del cuerpo del documento sigue en tamano normal.", 11, new PdfPoint(40, 685), regular);

        var result = Convert(builder);

        Assert.Contains(@"5\*3 y 2\*4", result.Markdown);
    }

    [Fact]
    public void Encabezado_numerado_conserva_su_nivel()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("2. Metodologia", 16, new PdfPoint(40, 760), bold);
        page.AddText("La muestra incluyo trescientas polizas de la cartera de hogar.", 11, new PdfPoint(40, 720), regular);
        page.AddText("El analisis considero los siniestros de los ultimos tres anos.", 11, new PdfPoint(40, 705), regular);

        var result = Convert(builder);

        Assert.Contains("# 2. Metodologia", result.Markdown);
        Assert.DoesNotContain("\n2. La muestra", result.Markdown);
    }

    [Fact]
    public void Guion_de_continuacion_no_se_vuelve_viñeta()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("El rango de distancias observado va de 10", 11, new PdfPoint(40, 700), regular);
        page.AddText("- 20 km en promedio segun el estudio realizado.", 11, new PdfPoint(40, 687), regular);

        var result = Convert(builder);

        Assert.DoesNotContain("\n- 20 km", result.Markdown);
        Assert.Contains("de 10 - 20 km", result.Markdown);
    }

    private static ConversionResult Convert(PdfDocumentBuilder builder)
    {
        using var stream = new MemoryStream(builder.Build());
        return new PdfToMarkdownConverter().Convert(stream);
    }

    // ------------------------- Fachada / DI -------------------------

    [Fact]
    public void Convertidor_propio_registrado_despues_de_AddMarkItDown_tiene_prioridad()
    {
        var provider = new ServiceCollection()
            .AddMarkItDown()
            .AddSingleton<IDocumentConverter, FakePdfConverter>()
            .BuildServiceProvider();

        var converter = provider.GetRequiredService<MarkItDownConverter>();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = converter.Convert(stream, "documento.pdf");

        Assert.Equal("PDF-FAKE", result.Markdown);
    }

    private sealed class FakePdfConverter : IDocumentConverter
    {
        public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".pdf" };

        public bool CanConvert(string extension) =>
            extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        public ConversionResult Convert(Stream input, ConversionOptions? options = null)
        {
            return new ConversionResult("PDF-FAKE");
        }
    }
}
