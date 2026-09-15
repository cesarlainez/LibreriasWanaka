namespace LibreriaOCR.Internal;

/// <summary>
/// Detecta el formato de un documento por sus bytes mágicos y, si no basta, por la extensión.
/// </summary>
internal static class FileTypeDetector
{
    public static DocumentFormat Detect(byte[] data, string? fileName)
    {
        if (data.Length >= 4)
        {
            // %PDF
            if (data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
                return DocumentFormat.Pdf;

            // PNG  (89 50 4E 47)
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return DocumentFormat.Png;

            // JPEG (FF D8 FF)
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return DocumentFormat.Jpeg;

            // TIFF (II*\0  o  MM\0*)
            if (IsTiff(data))
                return DocumentFormat.Tiff;
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => DocumentFormat.Pdf,
            ".png" => DocumentFormat.Png,
            ".jpg" or ".jpeg" or ".jpe" => DocumentFormat.Jpeg,
            ".tif" or ".tiff" => DocumentFormat.Tiff,
            _ => DocumentFormat.Unknown
        };
    }

    /// <summary>Indica si los bytes corresponden a un TIFF (little- o big-endian).</summary>
    public static bool IsTiff(byte[] data) =>
        data.Length >= 4 &&
        ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||   // II*\0
         (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A));    // MM\0*
}
