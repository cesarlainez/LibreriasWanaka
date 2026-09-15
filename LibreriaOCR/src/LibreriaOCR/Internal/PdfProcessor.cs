using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LibreriaOCR.Internal;

/// <summary>Utilidades para PDF: contar páginas, extraer texto digital y rasterizar a imagen.</summary>
internal static class PdfProcessor
{
    /// <summary>Una imagen incrustada en una página del PDF, con los bytes en un formato que Tesseract pueda cargar.</summary>
    internal readonly record struct EmbeddedImage(int PageNumber, int IndexEnPagina, byte[] Bytes, int Ancho, int Alto);

    /// <summary>
    /// Enumera las imágenes rasterizadas incrustadas en el PDF (por página) tratando de devolver
    /// PNG cuando es posible y, si no, los bytes crudos del <see cref="IPdfImage"/> tal como los
    /// guardó el PDF (típicamente JPEG). Ignora imágenes con menos píxeles que <paramref name="minPixeles"/>
    /// (íconos, viñetas y decoraciones donde el OCR solo produce ruido).
    /// </summary>
    public static IEnumerable<EmbeddedImage> ExtractEmbeddedImages(byte[] pdf, int minPixeles)
    {
        using var document = PdfDocument.Open(pdf);
        var numeroPagina = 0;
        foreach (var page in document.GetPages())
        {
            numeroPagina++;
            var indice = 0;
            foreach (var image in page.GetImages())
            {
                if (image is null) continue;

                var ancho = image.WidthInSamples;
                var alto = image.HeightInSamples;
                if ((long)ancho * alto < minPixeles) continue;

                if (!TryObtenerBytes(image, out var bytes)) continue;

                indice++;
                yield return new EmbeddedImage(numeroPagina, indice, bytes, ancho, alto);
            }
        }
    }

    private static bool TryObtenerBytes(IPdfImage image, out byte[] bytes)
    {
        // PdfPig ofrece una conversión a PNG que a veces no está disponible para el formato
        // interno de la imagen (por ejemplo JPX o CCITT); en ese caso caemos a los bytes crudos.
        if (image.TryGetPng(out var png) && png is { Length: > 0 })
        {
            bytes = png;
            return true;
        }

        var crudos = image.RawBytes;
        if (crudos.Length > 0)
        {
            bytes = crudos.ToArray();
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }

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
