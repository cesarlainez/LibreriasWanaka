using MarkItDown.Converters.Pdf;
using MarkItDown.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace MarkItDown.Tests;

public class PdfConverterTests
{
    private static ConversionResult Convert(byte[] pdfBytes, ConversionOptions? options = null)
    {
        using var stream = new MemoryStream(pdfBytes);
        return new PdfToMarkdownConverter().Convert(stream, options);
    }

    [Fact]
    public void Detecta_titulo_por_tamano_de_fuente()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("Informe de resultados", 22, new PdfPoint(40, 780), bold);
        page.AddText("El primer trimestre cerro con resultados positivos para la compania.", 11, new PdfPoint(40, 740), regular);
        page.AddText("La cartera de asegurados crecio un dos por ciento en el periodo.", 11, new PdfPoint(40, 725), regular);
        page.AddText("Los gastos operativos se mantuvieron estables durante el trimestre.", 11, new PdfPoint(40, 710), regular);

        var result = Convert(builder.Build());

        Assert.Contains("# Informe de resultados", result.Markdown);
        Assert.Contains("El primer trimestre cerro con resultados positivos", result.Markdown);
    }

    [Fact]
    public void Sin_deteccion_de_encabezados_todo_es_parrafo()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("Informe de resultados", 22, new PdfPoint(40, 780), bold);
        page.AddText("Texto del cuerpo del documento en tamano normal.", 11, new PdfPoint(40, 740), regular);

        var result = Convert(builder.Build(), new ConversionOptions { DetectPdfHeadings = false });

        Assert.DoesNotContain("# ", result.Markdown);
        Assert.Contains("Informe de resultados", result.Markdown);
    }

    [Fact]
    public void Convierte_lineas_con_guion_en_lista()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText("Los productos disponibles se listan a continuacion en detalle.", 11, new PdfPoint(40, 700), regular);
        page.AddText("- Seguro de hogar para viviendas urbanas", 11, new PdfPoint(40, 660), regular);
        page.AddText("- Seguro de viaje con cobertura internacional", 11, new PdfPoint(40, 645), regular);

        var result = Convert(builder.Build());

        Assert.Contains("- Seguro de hogar para viviendas urbanas", result.Markdown);
        Assert.Contains("- Seguro de viaje con cobertura internacional", result.Markdown);
    }

    [Fact]
    public void Marcadores_de_pagina_opcionales()
    {
        var builder = new PdfDocumentBuilder();
        var regular = builder.AddStandard14Font(Standard14Font.Helvetica);

        var page1 = builder.AddPage(PageSize.A4);
        page1.AddText("Contenido de la primera pagina del documento.", 11, new PdfPoint(40, 700), regular);
        var page2 = builder.AddPage(PageSize.A4);
        page2.AddText("Contenido de la segunda pagina del documento.", 11, new PdfPoint(40, 700), regular);

        var result = Convert(builder.Build(), new ConversionOptions { IncludePageMarkers = true });

        Assert.Contains("<!-- Página 1 -->", result.Markdown);
        Assert.Contains("<!-- Página 2 -->", result.Markdown);
    }

    [Fact]
    public void Pdf_sin_texto_devuelve_advertencia()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);

        var result = Convert(builder.Build());

        Assert.Equal(string.Empty, result.Markdown);
        Assert.Contains(result.Warnings, w => w.Contains("OCR"));
    }

    [Fact]
    public void Stream_no_pdf_lanza_excepcion_de_conversion()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        Assert.Throws<MarkdownConversionException>(() => Convert(bytes));
    }
}
