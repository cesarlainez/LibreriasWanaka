using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MarkItDown.Converters.Docx;
using MarkItDown.Core;

namespace MarkItDown.Tests;

public class DocxConverterTests
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

    private static string Convert(MemoryStream docx, ConversionOptions? options = null)
    {
        return new DocxToMarkdownConverter().Convert(docx, options).Markdown;
    }

    [Fact]
    public void Convierte_encabezado_por_nivel_de_esquema()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new ParagraphProperties(new OutlineLevel { Val = 0 }),
                new Run(new Text("Informe anual"))));
            body.Append(new Paragraph(new Run(new Text("Texto normal del documento."))));
        });

        var markdown = Convert(docx);

        Assert.Contains("# Informe anual", markdown);
        Assert.Contains("Texto normal del documento.", markdown);
    }

    [Fact]
    public void Convierte_encabezado_por_id_de_estilo()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Heading2" }),
                new Run(new Text("Resultados"))));
        });

        var markdown = Convert(docx);

        Assert.Contains("## Resultados", markdown);
    }

    [Fact]
    public void Convierte_negrita_cursiva_y_tachado()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(
                new Run(new Text("Texto ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new Bold()), new Text("importante")),
                new Run(new Text(" y ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new Italic()), new Text("resaltado")),
                new Run(new Text(" y ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new Strike()), new Text("eliminado"))));
        });

        var markdown = Convert(docx);

        Assert.Contains("**importante**", markdown);
        Assert.Contains("*resaltado*", markdown);
        Assert.Contains("~~eliminado~~", markdown);
    }

    [Fact]
    public void Convierte_hipervinculo_externo()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri("https://www.acsa.com.sv/"), true);
            body.Append(new Paragraph(
                new Hyperlink(new Run(new Text("sitio de ACSA"))) { Id = relationship.Id }));
        });

        var markdown = Convert(docx);

        Assert.Contains("[sitio de ACSA](https://www.acsa.com.sv/)", markdown);
    }

    [Fact]
    public void Convierte_listas_con_viñetas_y_numeradas()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var numberingPart = main.AddNewPart<NumberingDefinitionsPart>();
            numberingPart.Numbering = new Numbering(
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }) { LevelIndex = 0 })
                { AbstractNumberId = 1 },
                new AbstractNum(
                    new Level(new NumberingFormat { Val = NumberFormatValues.Decimal }) { LevelIndex = 0 })
                { AbstractNumberId = 2 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 },
                new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 });

            body.Append(ListItem("Manzana", numId: 1));
            body.Append(ListItem("Pera", numId: 1));
            body.Append(new Paragraph(new Run(new Text("Pasos:"))));
            body.Append(ListItem("Primero", numId: 2));
            body.Append(ListItem("Segundo", numId: 2));
        });

        var markdown = Convert(docx);

        Assert.Contains("- Manzana\n- Pera", markdown);
        Assert.Contains("1. Primero\n1. Segundo", markdown);
    }

    private static Paragraph ListItem(string text, int numId)
    {
        return new Paragraph(
            new ParagraphProperties(new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId })),
            new Run(new Text(text)));
    }

    [Fact]
    public void Convierte_tabla_con_encabezado()
    {
        using var docx = BuildDocx((_, body) =>
        {
            static TableCell Cell(string text) => new(new Paragraph(new Run(new Text(text))));

            body.Append(new Table(
                new TableRow(Cell("Producto"), Cell("Precio")),
                new TableRow(Cell("Seguro de hogar"), Cell("$25")),
                new TableRow(Cell("Seguro de viaje"), Cell("$10"))));
        });

        var markdown = Convert(docx);

        Assert.Contains("| Producto | Precio |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| Seguro de hogar | $25 |", markdown);
        Assert.Contains("| Seguro de viaje | $10 |", markdown);
    }

    [Fact]
    public void Escapa_caracteres_especiales_de_markdown()
    {
        using var docx = BuildDocx((_, body) =>
        {
            body.Append(new Paragraph(new Run(new Text("2 * 3 = 6 [ver nota]"))));
        });

        var markdown = Convert(docx);

        Assert.Contains(@"2 \* 3 = 6 \[ver nota\]", markdown);
    }

    [Fact]
    public void Documento_con_imagen_sin_extraccion_agrega_advertencia()
    {
        using var docx = BuildDocx((main, body) =>
        {
            var imagePart = main.AddImagePart(ImagePartType.Png);
            using (var pngStream = new MemoryStream(MinimalPng))
            {
                imagePart.FeedData(pngStream);
            }

            var relId = main.GetIdOfPart(imagePart);
            body.Append(new Paragraph(new Run(BuildDrawing(relId))));
            body.Append(new Paragraph(new Run(new Text("Texto tras la imagen."))));
        });

        var result = new DocxToMarkdownConverter().Convert(docx);

        Assert.Contains("Texto tras la imagen.", result.Markdown);
        Assert.Contains(result.Warnings, w => w.IndexOf("imágenes", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Documento_con_imagen_extrae_archivo_y_genera_enlace()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "markitdown-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var docx = BuildDocx((main, body) =>
            {
                var imagePart = main.AddImagePart(ImagePartType.Png);
                using (var pngStream = new MemoryStream(MinimalPng))
                {
                    imagePart.FeedData(pngStream);
                }

                var relId = main.GetIdOfPart(imagePart);
                body.Append(new Paragraph(new Run(BuildDrawing(relId))));
            });

            var options = new ConversionOptions
            {
                ExtractImages = true,
                ImageOutputDirectory = outputDir,
                ImageLinkPrefix = "imagenes/",
            };

            var markdown = Convert(docx, options);

            Assert.Contains("![", markdown);
            Assert.Contains("(imagenes/imagen-001.png)", markdown);
            Assert.True(File.Exists(Path.Combine(outputDir, "imagen-001.png")));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static Drawing BuildDrawing(string relationshipId)
    {
        // Estructura mínima de imagen inline (wp:inline > a:graphic > pic:pic > a:blip).
        return new Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = 990000L, Cy = 792000L },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties
                {
                    Id = 1U,
                    Name = "Logotipo",
                    Description = "Logotipo de la empresa",
                },
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties
                                {
                                    Id = 0U,
                                    Name = "imagen.png",
                                },
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                new DocumentFormat.OpenXml.Drawing.Blip { Embed = relationshipId },
                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = 990000L, Cy = 792000L }),
                                new DocumentFormat.OpenXml.Drawing.PresetGeometry(
                                    new DocumentFormat.OpenXml.Drawing.AdjustValueList())
                                { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));
    }

    // PNG válido de 1x1 píxel.
    private static readonly byte[] MinimalPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x73, 0x75, 0x01, 0x18, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82,
    };
}
