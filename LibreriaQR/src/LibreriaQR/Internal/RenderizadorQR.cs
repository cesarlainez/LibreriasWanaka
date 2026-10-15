using System.Globalization;
using SkiaSharp;

namespace LibreriaQR.Internal;

/// <summary>
/// Convierte la matriz de modulos del QR en un PNG con SkiaSharp (sin System.Drawing). Aplica
/// colores, dibuja cada modulo como un bloque de pixeles exactos (bordes nitidos, equivalente a
/// escalar por vecino mas cercano) y, opcionalmente, compone el logo centrado sobre un recuadro
/// de fondo redondeado.
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
            ? SKColors.Transparent
            : ParsearColor(opciones.ColorFondo, "ColorFondo");
        var primerPlano = ParsearColor(opciones.ColorPrimerPlano, "ColorPrimerPlano");

        var ppm = opciones.TamanoPx is > 0
            ? Math.Max(1, opciones.TamanoPx.Value / n)
            : Math.Max(1, opciones.PixelesPorModulo);
        var lado = n * ppm;

        using var qr = new SKBitmap(new SKImageInfo(lado, lado, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(qr))
        {
            canvas.Clear(fondo);

            // BlendMode.Src: el modulo reemplaza al fondo (no se mezcla), igual con colores semitransparentes.
            using var pincel = new SKPaint { Color = primerPlano, IsAntialias = false, BlendMode = SKBlendMode.Src };
            for (var y = 0; y < n; y++)
            {
                var filaMod = modulos[y];
                for (var x = 0; x < n; x++)
                    if (filaMod[x])
                        canvas.DrawRect(SKRect.Create(x * ppm, y * ppm, ppm, ppm), pincel);
            }

            if (opciones.Logo is { } logoOpc)
                ComponerLogo(canvas, logoOpc, lado);
        }

        using var datos = qr.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("No se pudo codificar el QR como PNG.");
        return (datos.ToArray(), lado);
    }

    private static void ComponerLogo(SKCanvas canvas, OpcionesLogo opc, int lado)
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

        SKBitmap? original;
        try
        {
            original = SKBitmap.Decode(bytes);
        }
        catch (Exception ex)
        {
            throw new QrLogoInvalidoException("El logo no se pudo decodificar como imagen (PNG, JPG, BMP o GIF).", ex);
        }
        if (original is null)
            throw new QrLogoInvalidoException("El logo no se pudo decodificar como imagen (PNG, JPG, BMP o GIF).");

        using (original)
        {
            // Conserva la proporcion y cabe dentro del cuadrado objetivo (agranda o reduce).
            var proporcion = Math.Clamp(opc.Proporcion, 0.05, 0.35);
            var objetivo = Math.Max(1, (int)(lado * proporcion));
            var escala = Math.Min((double)objetivo / original.Width, (double)objetivo / original.Height);
            var lw = Math.Max(1, (int)Math.Round(original.Width * escala));
            var lh = Math.Max(1, (int)Math.Round(original.Height * escala));

            using var logo = Redimensionar(original, lw, lh);

            if (opc.ConFondo)
            {
                var margen = Math.Max(0, opc.MargenPx);
                var padW = lw + 2 * margen;
                var padH = lh + 2 * margen;
                var colorPad = ParsearColor(opc.ColorFondo, "Logo.ColorFondo");
                using var recuadro = CrearRecuadro(padW, padH, colorPad, opc.RadioEsquinas);
                canvas.DrawBitmap(recuadro, (lado - padW) / 2, (lado - padH) / 2, SKSamplingOptions.Default);
            }

            canvas.DrawBitmap(logo, (lado - lw) / 2, (lado - lh) / 2, SKSamplingOptions.Default);
        }
    }

    /// <summary>
    /// Redimensiona el logo. Al reducir promedia por area (cada pixel destino es la media ponderada
    /// de los pixeles origen que cubre), sin aliasing aunque la reduccion sea grande; al ampliar usa
    /// cubico Catmull-Rom (el bicubico que usaba ImageSharp). Trabaja en alfa premultiplicado.
    /// </summary>
    private static SKBitmap Redimensionar(SKBitmap origen, int w, int h)
    {
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        if (w >= origen.Width || h >= origen.Height)
            return origen.Resize(info, new SKSamplingOptions(SKCubicResampler.CatmullRom))
                ?? throw new QrLogoInvalidoException("El logo no se pudo redimensionar.");

        using var src = new SKBitmap(new SKImageInfo(origen.Width, origen.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (!origen.ScalePixels(src, SKSamplingOptions.Default))
            throw new QrLogoInvalidoException("El logo no se pudo convertir a RGBA.");

        var sw = src.Width;
        var sh = src.Height;
        var pix = src.GetPixelSpan();
        var px = Contribuciones(sw, w);
        var py = Contribuciones(sh, h);

        // Pasada horizontal: sh filas x w columnas x 4 canales.
        var tmp = new float[sh * w * 4];
        for (var y = 0; y < sh; y++)
        {
            var fila = y * sw * 4;
            for (var x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                foreach (var (i, peso) in px[x])
                {
                    var o = fila + i * 4;
                    r += pix[o] * peso; g += pix[o + 1] * peso; b += pix[o + 2] * peso; a += pix[o + 3] * peso;
                }
                var t = (y * w + x) * 4;
                tmp[t] = r; tmp[t + 1] = g; tmp[t + 2] = b; tmp[t + 3] = a;
            }
        }

        // Pasada vertical.
        var destino = new SKBitmap(info);
        var dst = destino.GetPixelSpan();
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                foreach (var (i, peso) in py[y])
                {
                    var t = (i * w + x) * 4;
                    r += tmp[t] * peso; g += tmp[t + 1] * peso; b += tmp[t + 2] * peso; a += tmp[t + 3] * peso;
                }
                var o = (y * w + x) * 4;
                var alfa = Byte(a);
                dst[o] = Math.Min(Byte(r), alfa); dst[o + 1] = Math.Min(Byte(g), alfa); dst[o + 2] = Math.Min(Byte(b), alfa); dst[o + 3] = alfa;
            }
        return destino;

        static byte Byte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
    }

    /// <summary>Para cada pixel destino, los pixeles origen que cubre y la fraccion de cada uno (suman 1).</summary>
    private static (int Indice, float Peso)[][] Contribuciones(int origen, int destino)
    {
        var escala = (double)origen / destino;
        var resultado = new (int, float)[destino][];
        for (var d = 0; d < destino; d++)
        {
            var ini = d * escala;
            var fin = ini + escala;
            var lista = new List<(int, float)>();
            for (var s = (int)ini; s < Math.Min(origen, (int)Math.Ceiling(fin)); s++)
            {
                var cubierto = Math.Min(fin, s + 1) - Math.Max(ini, s);
                if (cubierto > 0)
                    lista.Add((s, (float)(cubierto / escala)));
            }
            resultado[d] = lista.ToArray();
        }
        return resultado;
    }

    /// <summary>Crea un recuadro relleno con esquinas redondeadas (fuera del radio queda transparente).</summary>
    private static SKBitmap CrearRecuadro(int w, int h, SKColor color, double radioFraccion)
    {
        var img = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        img.Erase(color);
        var r = (int)(Math.Min(w, h) * Math.Clamp(radioFraccion, 0, 0.5));
        if (r <= 0)
            return img;

        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (FueraDeEsquina(x, y, w, h, r))
                    img.SetPixel(x, y, SKColors.Transparent);
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

    /// <summary>
    /// Interpreta un color hex CSS: <c>RGB</c>, <c>RGBA</c>, <c>RRGGBB</c> o <c>RRGGBBAA</c>, con o sin
    /// <c>#</c>. El alfa va al final (a diferencia de <c>SKColor.TryParse</c>, que lo espera al inicio).
    /// </summary>
    private static SKColor ParsearColor(string hex, string nombreCampo)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new QrContenidoInvalidoException($"El color '{nombreCampo}' esta vacio.");
        var limpio = hex.Trim().TrimStart('#');
        if (limpio.Length is 3 or 4)
            limpio = string.Concat(limpio.Select(c => new string(c, 2)));
        if (limpio.Length == 6)
            limpio += "FF";
        if (limpio.Length != 8
            || !uint.TryParse(limpio, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgba))
            throw new QrContenidoInvalidoException($"El color '{nombreCampo}' no es un hex valido: '{hex}'.");
        return new SKColor((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
    }
}
