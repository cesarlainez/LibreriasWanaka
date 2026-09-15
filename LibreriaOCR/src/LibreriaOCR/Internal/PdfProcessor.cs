using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LibreriaOCR.Internal;

/// <summary>Utilidades para PDF: contar páginas, extraer texto digital y rasterizar a imagen.</summary>
internal static class PdfProcessor
{
    /// <summary>Número de páginas del PDF.</summary>
    public static int GetPageCount(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.NumberOfPages;
    }

    /// <summary>
    /// Extrae el texto digital de cada página. El índice del arreglo equivale a (número de página - 1).
    /// Devuelve cadena vacía en las páginas que no tienen texto (escaneadas).
    /// </summary>
    public static string[] ExtractEmbeddedText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        var result = new string[document.NumberOfPages];
        var i = 0;
        foreach (var page in document.GetPages())
        {
            result[i++] = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
        }
        return result;
    }

    /// <summary>Rasteriza una página (índice 0-based) a PNG en memoria a la resolución indicada.</summary>
    public static byte[] RenderPageToPng(byte[] pdf, int pageIndexZeroBased, int dpi)
    {
        using var bitmap = Conversion.ToImage(pdf, page: pageIndexZeroBased, options: new RenderOptions(Dpi: dpi));
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
