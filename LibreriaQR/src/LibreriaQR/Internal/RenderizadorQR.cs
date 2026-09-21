using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LibreriaQR.Internal;

/// <summary>
/// Convierte la matriz de modulos del QR en un PNG con ImageSharp (100% gestionado, sin
/// System.Drawing). Aplica colores, escala con vecino mas cercano (bordes nitidos) y,
/// opcionalmente, compone el logo centrado sobre un recuadro de fondo redondeado.
/// </summary>
internal static class RenderizadorQR
{
    /// <param name="modulos">Matriz cuadrada; <c>true</c> = modulo oscuro.</param>
    /// <param name="opciones">Opciones de render.</param>
    /// <returns>PNG en bytes y sus dimensiones.</returns>
    public static (byte[] Png, int Lado) Render(bool[][] modulos, OpcionesQR opciones)
    {
        var n = modulos.Length;

        var fondo = opciones.FondoTransparente
            ? Color.Transparent
            : ParsearColor(opciones.ColorFondo, "ColorFondo");
        var primerPlano = ParsearColor(opciones.ColorPrimerPlano, "ColorPrimerPlano");
        var pxFondo = fondo.ToPixel<Rgba32>();
        var pxPrimerPlano = primerPlano.ToPixel<Rgba32>();

        // 1 pixel por modulo; luego se escala por vecino mas cercano.
        using var qr = new Image<Rgba32>(n, n, pxFondo);
        qr.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < n; y++)
            {
                var fila = acc.GetRowSpan(y);
                var filaMod = modulos[y];
                for (var x = 0; x < n; x++)
                    if (filaMod[x])
                        fila[x] = pxPrimerPlano;
            }
        });

        var ppm = opciones.TamanoPx is > 0
            ? Math.Max(1, opciones.TamanoPx.Value / n)
            : Math.Max(1, opciones.PixelesPorModulo);
        var lado = n * ppm;

        qr.Mutate(c => c.Resize(new ResizeOptions
        {
            Size = new Size(lado, lado),
            Sampler = KnownResamplers.NearestNeighbor,
            Mode = ResizeMode.Stretch,
        }));

        if (opciones.Logo is { } logoOpc)
            ComponerLogo(qr, logoOpc, lado);

        using var ms = new MemoryStream();
        qr.SaveAsPng(ms);
        return (ms.ToArray(), lado);
    }

    private static void ComponerLogo(Image<Rgba32> qr, OpcionesLogo opc, int lado)
    {
        var bytes = opc.Bytes;
        if (bytes is null || bytes.Length == 0)
        {
            if (string.IsNullOrWhiteSpace(opc.Ruta))
                throw new QrLogoInvalidoException("Se pidio logo pero no se aporto ni 'Bytes' ni 'Ruta'.");
            if (!File.Exists(opc.Ruta))
                throw new QrLogoInvalidoException($"No se encontro el archivo del logo: {opc.Ruta}");
            bytes = File.ReadAllBytes(opc.Ruta);
        }

        Image<Rgba32> logo;
        try
        {
            logo = Image.Load<Rgba32>(bytes);
        }
        catch (Exception ex)
        {
            throw new QrLogoInvalidoException("El logo no se pudo decodificar como imagen (PNG, JPG, BMP o GIF).", ex);
        }

        using (logo)
        {
            var proporcion = Math.Clamp(opc.Proporcion, 0.05, 0.35);
            var objetivo = Math.Max(1, (int)(lado * proporcion));
            logo.Mutate(c => c.Resize(new ResizeOptions
            {
                Size = new Size(objetivo, objetivo),
                Mode = ResizeMode.Max, // conserva proporcion, cabe dentro del cuadrado
            }));

            var lw = logo.Width;
            var lh = logo.Height;

            if (opc.ConFondo)
            {
                var margen = Math.Max(0, opc.MargenPx);
                var padW = lw + 2 * margen;
                var padH = lh + 2 * margen;
                var colorPad = ParsearColor(opc.ColorFondo, "Logo.ColorFondo");
                using var recuadro = CrearRecuadro(padW, padH, colorPad, opc.RadioEsquinas);
                qr.Mutate(c => c.DrawImage(recuadro, new Point((lado - padW) / 2, (lado - padH) / 2), 1f));
            }

            qr.Mutate(c => c.DrawImage(logo, new Point((lado - lw) / 2, (lado - lh) / 2), 1f));
        }
    }

    /// <summary>Crea un recuadro relleno con esquinas redondeadas (fuera del radio queda transparente).</summary>
    private static Image<Rgba32> CrearRecuadro(int w, int h, Color color, double radioFraccion)
    {
        var img = new Image<Rgba32>(w, h, color.ToPixel<Rgba32>());
        var r = (int)(Math.Min(w, h) * Math.Clamp(radioFraccion, 0, 0.5));
        if (r <= 0)
            return img;

        var transparente = new Rgba32(0, 0, 0, 0);
        img.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < h; y++)
            {
                var fila = acc.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                    if (FueraDeEsquina(x, y, w, h, r))
                        fila[x] = transparente;
            }
        });
        return img;
    }

    private static bool FueraDeEsquina(int x, int y, int w, int h, int r)
    {
        // Centros de los cuartos de circulo en cada esquina.
        var cx = x < r ? r : x > w - 1 - r ? w - 1 - r : x;
        var cy = y < r ? r : y > h - 1 - r ? h - 1 - r : y;
        // Solo importa cuando el punto esta en la region de una esquina.
        if (cx == x && cy == y)
            return false;
        var dx = x - cx;
        var dy = y - cy;
        return dx * dx + dy * dy > r * r;
    }

    private static Color ParsearColor(string hex, string nombreCampo)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new QrContenidoInvalidoException($"El color '{nombreCampo}' esta vacio.");
        var limpio = hex.Trim().TrimStart('#');
        if (!Color.TryParseHex(limpio, out var color))
            throw new QrContenidoInvalidoException($"El color '{nombreCampo}' no es un hex valido: '{hex}'.");
        return color;
    }
}
