using SkiaSharp;

namespace LibreriaOCR.Internal;

/// <summary>Un componente conexo (borrón de tinta) con su caja, cantidad de píxeles y cuántos son a color.</summary>
internal readonly record struct InkComponent(int MinX, int MinY, int MaxX, int MaxY, int Pixels, int ColorfulPixels)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterY => (MinY + MaxY) / 2.0;
}

/// <summary>Resultado del análisis geométrico de una página: regiones candidatas y el mapa de color / dimensiones.</summary>
internal readonly record struct GeometryResult(
    List<HandwritingRegion> Regions, float MaxScore, bool[] Colorful, int Width, int Height, double Scale);

/// <summary>
/// Detección heurística de escritura a mano (sin IA): binariza, extrae componentes conexos, los agrupa en
/// líneas y mide qué tan irregular es cada línea (altura, línea base y espaciado). La imprenta es muy regular;
/// la escritura a mano, no.
/// </summary>
internal static class HandwritingAnalyzer
{
    // --- Umbrales de las métricas (provisionales; calibrar con documentos reales) ---
    private const double HeightCvLow = 0.28, HeightCvHigh = 0.55;      // variabilidad de altura
    private const double BaselineLow = 0.06, BaselineHigh = 0.22;      // ondulación de la línea base
    private const double GapCvLow = 0.70, GapCvHigh = 1.50;            // irregularidad de espaciado
    private const double WeightHeight = 0.5, WeightBaseline = 0.4, WeightGap = 0.1;

    /// <summary>
    /// Análisis geométrico de la página: devuelve las regiones candidatas (trazos conectados + palabras
    /// irregulares) junto con el mapa de color y las dimensiones, para que el llamador corrobore con OCR.
    /// </summary>
    public static GeometryResult AnalyzeGeometry(SKBitmap source, int pageNumber, HandwritingOptions options)
    {
        var gray = ToGrayscale(source, options.MaxImageSide, out var colorful, out var w, out var h, out var scale);
        if (w < 10 || h < 10)
            return new GeometryResult(new List<HandwritingRegion>(), 0f, colorful, w, h, scale);

        var threshold = OtsuThreshold(gray);
        var ink = new bool[gray.Length];
        var inkCount = 0;
        for (var i = 0; i < gray.Length; i++)
            if (gray[i] < threshold) { ink[i] = true; inkCount++; }

        // Página casi vacía, o casi toda tinta (una foto): no es fiable analizar.
        var inkRatio = (double)inkCount / (w * h);
        if (inkRatio < 0.002 || inkRatio > 0.5)
            return new GeometryResult(new List<HandwritingRegion>(), 0f, colorful, w, h, scale);

        var components = FindComponents(ink, colorful, w, h);
        var glyphs = FilterGlyphs(components, w, h);
        var regions = new List<HandwritingRegion>();
        var maxScore = 0f;
        var inv = scale <= 0 ? 1.0 : 1.0 / scale;

        // Firmas y cursiva: se detectan como trazos conectados grandes (con filtro de color).
        if (options.DetectCursiveStrokes && glyphs.Count > 0)
        {
            var medianGlyphHeight = MedianHeight(glyphs);
            foreach (var stroke in FindConnectedStrokeRegions(components, w, h, pageNumber, inv, options, medianGlyphHeight))
            {
                if (regions.Any(existing => OverlapsSubstantially(existing, stroke))) continue;
                regions.Add(stroke);
                if (stroke.Score > maxScore) maxScore = stroke.Score;
            }
        }

        if (glyphs.Count >= options.MinComponentsPerLine)
        {
            // Se evalúa por "palabra" (trozos contiguos y homogéneos), no por línea completa.
            var lines = GroupIntoLines(glyphs);
            foreach (var line in lines)
            foreach (var word in SplitIntoWords(line))
            {
                if (word.Count < options.MinComponentsPerLine) continue;

                var (score, diag) = ScoreGroup(word, options.IncludeDiagnostics);
                if (score > maxScore) maxScore = score;
                if (score < options.ScoreThreshold) continue;

                int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
                foreach (var c in word)
                {
                    if (c.MinX < minX) minX = c.MinX;
                    if (c.MinY < minY) minY = c.MinY;
                    if (c.MaxX > maxX) maxX = c.MaxX;
                    if (c.MaxY > maxY) maxY = c.MaxY;
                }

                var region = new HandwritingRegion
                {
                    PageNumber = pageNumber,
                    X = (int)(minX * inv),
                    Y = (int)(minY * inv),
                    Width = (int)((maxX - minX + 1) * inv),
                    Height = (int)((maxY - minY + 1) * inv),
                    Score = score,
                    Diagnostics = diag
                };

                if (!regions.Any(existing => OverlapsSubstantially(existing, region)))
                    regions.Add(region);
            }
        }

        return new GeometryResult(regions, maxScore, colorful, w, h, scale);
    }

    /// <summary>
    /// Construye regiones de escritura a mano a partir de las palabras de OCR: toma las de baja confianza,
    /// de tamaño razonable y que no sean a color (logos), y las agrupa por línea.
    /// </summary>
    public static List<HandwritingRegion> BuildWordRegions(
        IReadOnlyList<OcrWord> words, bool[] colorful, int w, int h, double scale, int pageNumber, HandwritingOptions options)
    {
        var pageHeightOriginal = scale > 0 ? h / scale : h;
        var candidates = new List<OcrWord>();
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;
            if (word.Confidence < 0 || word.Confidence >= options.WordHandwritingConfidence) continue;
            if (word.Width < 8 || word.Height < 8) continue;                       // ruido
            if (word.Height > pageHeightOriginal * 0.12) continue;                 // demasiado grande
            if (ColorFractionInBox(colorful, w, h, scale, word) > 0.15) continue;  // logo a color
            candidates.Add(word);
        }
        if (candidates.Count == 0) return new List<HandwritingRegion>();

        var medianH = Median(candidates.Select(c => (double)c.Height).ToList());
        var rowTolerance = Math.Max(6.0, medianH * 0.7);
        var gapLimit = Math.Max(medianH * 3.0, 30);
        var sorted = candidates.OrderBy(c => c.Y + c.Height / 2.0).ThenBy(c => c.X).ToList();

        var regions = new List<HandwritingRegion>();
        var row = new List<OcrWord>();
        var rowY = double.NaN;

        void FlushRow()
        {
            if (row.Count == 0) return;
            row.Sort((a, b) => a.X.CompareTo(b.X));
            var run = new List<OcrWord>();
            foreach (var wd in row)
            {
                if (run.Count > 0 && wd.X - (run[^1].X + run[^1].Width) > gapLimit)
                {
                    regions.Add(RegionFromWords(run, pageNumber));
                    run = new List<OcrWord>();
                }
                run.Add(wd);
            }
            if (run.Count > 0) regions.Add(RegionFromWords(run, pageNumber));
            row = new List<OcrWord>();
        }

        foreach (var word in sorted)
        {
            var cy = word.Y + word.Height / 2.0;
            if (double.IsNaN(rowY) || Math.Abs(cy - rowY) <= rowTolerance)
            {
                row.Add(word);
                rowY = double.IsNaN(rowY) ? cy : (rowY + cy) / 2.0;
            }
            else
            {
                FlushRow();
                row.Add(word);
                rowY = cy;
            }
        }
        FlushRow();
        return regions;
    }

    private static HandwritingRegion RegionFromWords(List<OcrWord> run, int pageNumber)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
        double confSum = 0;
        foreach (var wd in run)
        {
            if (wd.X < minX) minX = wd.X;
            if (wd.Y < minY) minY = wd.Y;
            if (wd.X + wd.Width > maxX) maxX = wd.X + wd.Width;
            if (wd.Y + wd.Height > maxY) maxY = wd.Y + wd.Height;
            confSum += wd.Confidence;
        }
        var avgConf = confSum / run.Count;
        return new HandwritingRegion
        {
            PageNumber = pageNumber,
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            Score = (float)Math.Clamp(100 - avgConf, 0, 100),
            Diagnostics = $"words={run.Count} avgConf={avgConf:0.#}"
        };
    }

    private static double ColorFractionInBox(bool[] colorful, int w, int h, double scale, OcrWord word)
    {
        // La caja de la palabra está en coords originales; el mapa de color está en coords de análisis (×scale).
        var ax1 = Math.Clamp((int)Math.Floor(word.X * scale), 0, w - 1);
        var ay1 = Math.Clamp((int)Math.Floor(word.Y * scale), 0, h - 1);
        var ax2 = Math.Clamp((int)Math.Ceiling((word.X + word.Width) * scale), 0, w);
        var ay2 = Math.Clamp((int)Math.Ceiling((word.Y + word.Height) * scale), 0, h);
        if (ax2 <= ax1 || ay2 <= ay1) return 0;

        int total = 0, colored = 0;
        for (var y = ay1; y < ay2; y++)
        {
            var rowStart = y * w;
            for (var x = ax1; x < ax2; x++)
            {
                total++;
                if (colorful[rowStart + x]) colored++;
            }
        }
        return total > 0 ? (double)colored / total : 0;
    }

    /// <summary>
    /// Encuentra palabras cursivas y firmas que se convierten en un solo componente de tinta.
    /// Los caracteres de imprenta suelen ser componentes pequeños y compactos; los trazos a mano
    /// suelen ser horizontales, alargados y con poca ocupación de tinta dentro de su caja.
    /// </summary>
    private static IEnumerable<HandwritingRegion> FindConnectedStrokeRegions(
        List<InkComponent> components, int pageWidth, int pageHeight, int pageNumber, double inv,
        HandwritingOptions options, double medianGlyphHeight)
    {
        var minWidth = Math.Max(14, (int)(pageWidth * 0.018));
        var minHeight = Math.Max(5, (int)(pageHeight * 0.003));

        foreach (var c in components)
        {
            if (c.Width < minWidth || c.Height < minHeight ||
                c.Width > pageWidth * 0.85 || c.Height > pageHeight * 0.18)
                continue;

            // Debe ser claramente más alto que la letra impresa. Así se descarta el texto tipográfico
            // que en escaneos se conecta en un solo componente (tiene la misma altura que la imprenta).
            if (medianGlyphHeight > 0 && c.Height < medianGlyphHeight * 1.4)
                continue;

            var aspect = (double)c.Width / c.Height;
            var density = (double)c.Pixels / (c.Width * c.Height);
            // aspect > 8 = una línea o una fila de texto entera conectada, no una palabra/firma.
            if (aspect < 1.5 || aspect > 8.0 || density < 0.015 || density > 0.42)
                continue;

            // Trazo mayormente a color = logo/gráfico, no tinta de escritura (que es casi gris/negra).
            var colorFraction = c.Pixels > 0 ? (double)c.ColorfulPixels / c.Pixels : 0;
            if (colorFraction > 0.35)
                continue;

            // La densidad de una firma/cursiva normalmente queda entre 5% y 25%; una línea,
            // sello sólido o bloque tipográfico queda fuera de ese rango.
            // Una palabra cursiva corta suele tener proporción 3:1; no exigir una firma muy
            // larga (5.5:1) porque eso dejaba fuera justamente los campos manuscritos breves.
            var aspectScore = Normalize(aspect, 1.5, 4.0);
            var densityScore = density <= 0.12
                ? Normalize(density, 0.015, 0.06)
                : 1.0 - Normalize(density, 0.12, 0.42);
            var widthScore = Normalize((double)c.Width / pageWidth, 0.018, 0.16);
            var score = (float)(100 * Clamp01(0.50 * aspectScore + 0.30 * densityScore + 0.20 * widthScore));
            if (score < options.ScoreThreshold)
                continue;

            var diagnostics = options.IncludeDiagnostics
                ? $"connected width={c.Width} height={c.Height} aspect={aspect:0.00} density={density:0.000} color={colorFraction:0.00}"
                : null;

            yield return new HandwritingRegion
            {
                PageNumber = pageNumber,
                X = (int)(c.MinX * inv),
                Y = (int)(c.MinY * inv),
                Width = (int)(c.Width * inv),
                Height = (int)(c.Height * inv),
                Score = score,
                Diagnostics = diagnostics
            };
        }
    }

    private static bool OverlapsSubstantially(HandwritingRegion a, HandwritingRegion b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top) return false;

        var intersection = (right - left) * (bottom - top);
        var smaller = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return smaller > 0 && intersection >= smaller * 0.65;
    }

    private static HandwritingPage EmptyPage(int pageNumber) => new()
    {
        PageNumber = pageNumber,
        HasHandwriting = false,
        MaxScore = 0f,
        Regions = Array.Empty<HandwritingRegion>()
    };

    // ------------------------------------------------------------------ imagen -> gris

    private static byte[] ToGrayscale(SKBitmap source, int maxSide, out bool[] colorful, out int w, out int h, out double scale)
    {
        scale = Math.Min(1.0, (double)maxSide / Math.Max(source.Width, source.Height));
        w = Math.Max(1, (int)Math.Round(source.Width * scale));
        h = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var scaled = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(source, new SKRect(0, 0, w, h));
        }

        var span = scaled.GetPixelSpan();
        var rowBytes = scaled.RowBytes;
        var gray = new byte[w * h];
        colorful = new bool[w * h];
        for (var y = 0; y < h; y++)
        {
            var row = y * rowBytes;
            var outRow = y * w;
            for (var x = 0; x < w; x++)
            {
                var p = row + x * 4;               // BGRA
                int b = span[p], g = span[p + 1], r = span[p + 2];
                gray[outRow + x] = (byte)((77 * r + 150 * g + 29 * b) >> 8);

                // Saturación alta = píxel a color (logo/gráfico), no tinta de escritura (casi gris).
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                colorful[outRow + x] = (max - min) > 60;
            }
        }
        return gray;
    }

    private static int OtsuThreshold(byte[] gray)
    {
        Span<int> hist = stackalloc int[256];
        foreach (var v in gray) hist[v]++;

        var total = gray.Length;
        long sum = 0;
        for (var t = 0; t < 256; t++) sum += (long)t * hist[t];

        long sumB = 0;
        int wB = 0;
        double maxVar = -1;
        var threshold = 127;
        for (var t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            var wF = total - wB;
            if (wF == 0) break;

            sumB += (long)t * hist[t];
            var mB = (double)sumB / wB;
            var mF = (double)(sum - sumB) / wF;
            var between = (double)wB * wF * (mB - mF) * (mB - mF);
            if (between > maxVar) { maxVar = between; threshold = t; }
        }
        return threshold;
    }

    // ------------------------------------------------------------------ componentes conexos

    private static List<InkComponent> FindComponents(bool[] ink, bool[] colorful, int w, int h)
    {
        var comps = new List<InkComponent>();
        var visited = new bool[ink.Length];
        var stack = new Stack<int>();

        for (var start = 0; start < ink.Length; start++)
        {
            if (!ink[start] || visited[start]) continue;

            visited[start] = true;
            stack.Push(start);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0, count = 0, colorCount = 0;

            while (stack.Count > 0)
            {
                var idx = stack.Pop();
                var x = idx % w;
                var y = idx / w;
                count++;
                if (colorful[idx]) colorCount++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                // 8-conectividad
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        var nIdx = ny * w + nx;
                        if (ink[nIdx] && !visited[nIdx])
                        {
                            visited[nIdx] = true;
                            stack.Push(nIdx);
                        }
                    }
                }
            }

            comps.Add(new InkComponent(minX, minY, maxX, maxY, count, colorCount));
        }

        return comps;
    }

    private static List<InkComponent> FilterGlyphs(List<InkComponent> comps, int w, int h)
    {
        var heights = comps
            .Where(c => c.Height >= 4 && c.Height <= h / 6 && c.Width <= w / 2)
            .Select(c => c.Height)
            .OrderBy(x => x)
            .ToList();

        if (heights.Count == 0) return new List<InkComponent>();
        var median = heights[heights.Count / 2];
        double minH = Math.Max(4, median * 0.4), maxH = median * 3.0;

        var glyphs = new List<InkComponent>();
        foreach (var c in comps)
        {
            if (c.Height < minH || c.Height > maxH) continue;   // fuera de rango de letra
            if (c.Width > w * 0.5) continue;                    // líneas o bordes largos
            if (c.Pixels < 8) continue;                         // ruido
            glyphs.Add(c);
        }
        return glyphs;
    }

    private static List<List<InkComponent>> GroupIntoLines(List<InkComponent> glyphs)
    {
        var lines = new List<List<InkComponent>>();
        var sorted = glyphs.OrderBy(c => c.CenterY).ToList();

        var medianHeight = MedianHeight(sorted);
        var tolerance = Math.Max(4.0, medianHeight * 0.6);

        foreach (var c in sorted)
        {
            List<InkComponent>? best = null;
            var bestDist = double.MaxValue;
            foreach (var line in lines)
            {
                var lineY = LineCenterY(line);
                var d = Math.Abs(lineY - c.CenterY);
                if (d <= tolerance && d < bestDist) { bestDist = d; best = line; }
            }
            if (best is null) { best = new List<InkComponent>(); lines.Add(best); }
            best.Add(c);
        }

        foreach (var line in lines)
            line.Sort((a, b) => a.MinX.CompareTo(b.MinX));

        return lines;
    }

    private static double LineCenterY(List<InkComponent> line)
    {
        double sum = 0;
        foreach (var c in line) sum += c.CenterY;
        return sum / line.Count;
    }

    private static double MedianHeight(List<InkComponent> comps)
    {
        var hs = comps.Select(c => c.Height).OrderBy(x => x).ToList();
        return hs.Count == 0 ? 0 : hs[hs.Count / 2];
    }

    /// <summary>Parte una línea en "palabras" cortando donde hay huecos horizontales grandes (columnas o espacios).</summary>
    private static List<List<InkComponent>> SplitIntoWords(List<InkComponent> line)
    {
        var words = new List<List<InkComponent>>();
        if (line.Count == 0) return words;

        var medianHeight = MedianHeight(line);
        var gaps = new List<double>();
        for (var i = 1; i < line.Count; i++)
            gaps.Add(Math.Max(0, line[i].MinX - line[i - 1].MaxX));
        var medianGap = gaps.Count > 0 ? Median(gaps) : 0;

        var splitThreshold = Math.Max(medianHeight * 1.2, medianGap * 2.5 + 1);

        var current = new List<InkComponent> { line[0] };
        for (var i = 1; i < line.Count; i++)
        {
            var gap = line[i].MinX - line[i - 1].MaxX;
            if (gap > splitThreshold)
            {
                words.Add(current);
                current = new List<InkComponent>();
            }
            current.Add(line[i]);
        }
        words.Add(current);
        return words;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }

    // ------------------------------------------------------------------ score por línea

    private static (float score, string? diagnostics) ScoreGroup(List<InkComponent> group, bool includeDiagnostics)
    {
        var n = group.Count;

        // "Cuerpo": componentes de altura cercana a la mediana. Excluye tildes, puntos, comas y colas,
        // que ensucian la altura y la línea base también en texto impreso.
        var median = MedianHeight(group);
        var body = group.Where(c => c.Height >= 0.6 * median && c.Height <= 1.5 * median).ToList();
        if (body.Count < 3) body = group;
        var bn = body.Count;

        // Variabilidad de altura del cuerpo (coeficiente de variación)
        double meanH = 0;
        foreach (var c in body) meanH += c.Height;
        meanH /= bn;
        double varH = 0;
        foreach (var c in body) { var d = c.Height - meanH; varH += d * d; }
        varH /= bn;
        var heightCv = meanH > 0 ? Math.Sqrt(varH) / meanH : 0;

        // Ondulación de la línea base: ajuste por mínimos cuadrados de (centroX, base) sobre el cuerpo
        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        foreach (var c in body)
        {
            double x = c.CenterX, y = c.MaxY;
            sx += x; sy += y; sxx += x * x; sxy += x * y;
        }
        var denom = bn * sxx - sx * sx;
        var a = Math.Abs(denom) < 1e-6 ? 0 : (bn * sxy - sx * sy) / denom;
        var b = (sy - a * sx) / bn;
        double rss = 0;
        foreach (var c in body)
        {
            double x = c.CenterX, y = c.MaxY;
            var e = y - (a * x + b);
            rss += e * e;
        }
        var baselineNorm = meanH > 0 ? Math.Sqrt(rss / bn) / meanH : 0;

        // Irregularidad del espaciado entre componentes del grupo
        double gapCv = 0;
        if (n >= 3)
        {
            var gaps = new List<double>();
            for (var i = 1; i < n; i++)
            {
                var gap = group[i].MinX - group[i - 1].MaxX;
                if (gap > 0) gaps.Add(gap);
            }
            if (gaps.Count >= 2)
            {
                var meanG = gaps.Average();
                if (meanG > 0)
                {
                    double varG = 0;
                    foreach (var g in gaps) { var d = g - meanG; varG += d * d; }
                    varG /= gaps.Count;
                    gapCv = Math.Sqrt(varG) / meanG;
                }
            }
        }

        var sHeight = Normalize(heightCv, HeightCvLow, HeightCvHigh);
        var sBaseline = Normalize(baselineNorm, BaselineLow, BaselineHigh);
        var sGap = Normalize(gapCv, GapCvLow, GapCvHigh);
        var score01 = WeightHeight * sHeight + WeightBaseline * sBaseline + WeightGap * sGap;
        var score = (float)(100 * Clamp01(score01));

        string? diagnostics = includeDiagnostics
            ? $"n={n}/{bn} heightCV={heightCv:0.00} baseCV={baselineNorm:0.00} gapCV={gapCv:0.00} (sH={sHeight:0.00} sB={sBaseline:0.00} sG={sGap:0.00})"
            : null;

        return (score, diagnostics);
    }

    private static double Normalize(double value, double low, double high)
        => Clamp01((value - low) / (high - low));

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
