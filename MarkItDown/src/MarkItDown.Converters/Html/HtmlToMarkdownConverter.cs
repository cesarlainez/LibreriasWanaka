using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MarkItDown.Converters.Common;
using MarkItDown.Core;

namespace MarkItDown.Converters.Html;

/// <summary>
/// Convierte HTML (.html, .htm) a Markdown listo para IA, con foco en correos electrónicos:
/// omite lo que el lector no ve (scripts, estilos, elementos ocultos con <c>display:none</c> y el
/// relleno invisible de los "preheader" con caracteres de ancho cero), aplana las tablas de
/// maquetación (las que los correos usan para diseñar) y solo emite como tabla Markdown las tablas
/// de datos. Soporta encabezados, párrafos, saltos, negrita, cursiva, enlaces, imágenes (texto
/// alternativo), listas anidadas, citas, bloques de código y reglas horizontales.
/// </summary>
public sealed class HtmlToMarkdownConverter : IDocumentConverter
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "head", "noscript", "template", "svg", "iframe", "object", "embed", "canvas", "button", "input", "select", "textarea"
    };

    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "header", "footer", "main", "aside", "nav", "center", "address", "figure", "figcaption",
        "table", "tbody", "thead", "tfoot", "tr", "td", "th", "caption", "dl", "dt", "dd", "form", "fieldset", "body", "html",
        "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "pre", "blockquote", "hr"
    };

    private const string BlockSelector = "p, div, table, h1, h2, h3, h4, h5, h6, ul, ol, blockquote, pre, hr";

    // Caracteres que los correos usan como relleno invisible del "preheader" (o que no aportan al texto).
    private static readonly char[] Invisible = { '͏', '­', '​', '‌', '‍', '⁠', '﻿', '᠎' };

    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".html", ".htm" };

    public bool CanConvert(string extension)
    {
        var normalized = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (!normalized.StartsWith('.'))
        {
            normalized = "." + normalized;
        }

        return normalized is ".html" or ".htm";
    }

    public ConversionResult Convert(Stream input, ConversionOptions? options = null)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        try
        {
            // El parser detecta la codificación (BOM o <meta charset>) como un navegador.
            var document = new HtmlParser().ParseDocument(input);
            return Render(document, options ?? new ConversionOptions());
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new MarkdownConversionException("No se pudo leer el HTML.", ex);
        }
    }

    /// <summary>Convierte un texto HTML (p. ej. el cuerpo de un correo) a Markdown.</summary>
    /// <param name="html">HTML completo o un fragmento.</param>
    /// <param name="options">Opciones (opcional).</param>
    public ConversionResult ConvertHtml(string? html, ConversionOptions? options = null)
    {
        var document = new HtmlParser().ParseDocument(html ?? string.Empty);
        return Render(document, options ?? new ConversionOptions());
    }

    private static ConversionResult Render(IHtmlDocument document, ConversionOptions options)
    {
        var context = new RenderContext(options);
        var root = (INode?)document.Body ?? document.DocumentElement;
        if (root is not null)
        {
            RenderBlock(root, context, 0);
        }

        context.Flush();
        var title = MarkdownText.CollapseWhitespace(document.Title ?? string.Empty);
        var markdown = CompactBlankLines(context.Builder.Build());
        return new ConversionResult(markdown, title, context.Warnings);
    }

    /// <summary>Recorre un nodo en contexto de bloque: el texto suelto se acumula en el párrafo en curso.</summary>
    private static void RenderBlock(INode node, RenderContext context, int listDepth)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case IText text:
                    context.Inline.Append(NormalizeText(text.Data));
                    break;
                case IElement element when IsHidden(element):
                    break;
                case IElement element:
                    RenderElement(element, context, listDepth);
                    break;
            }
        }
    }

    private static void RenderElement(IElement element, RenderContext context, int listDepth)
    {
        var name = element.LocalName.ToLowerInvariant();
        switch (name)
        {
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
            {
                context.Flush();
                var text = MarkdownText.CollapseWhitespace(RenderInline(element, context));
                if (text.Length > 0)
                {
                    context.Builder.AddBlock(new string('#', name[1] - '0') + " " + text);
                }

                return;
            }

            case "br":
                context.Inline.Append('\n');
                return;

            case "hr":
                context.Flush();
                context.Builder.AddBlock("---");
                return;

            case "ul" or "ol":
                context.Flush();
                RenderList(element, context, listDepth, ordered: name == "ol");
                return;

            case "pre":
            {
                context.Flush();
                var code = element.TextContent.TrimEnd();
                if (code.Length > 0)
                {
                    context.Builder.AddBlock("```\n" + code + "\n```");
                }

                return;
            }

            case "blockquote":
            {
                context.Flush();
                var inner = new RenderContext(context.Options);
                RenderBlock(element, inner, 0);
                inner.Flush();
                var quoted = inner.Builder.Build();
                if (quoted.Length > 0)
                {
                    context.Builder.AddBlock(string.Join("\n", quoted.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l)));
                }

                return;
            }

            case "table" when IsDataTable(element):
                context.Flush();
                RenderDataTable(element, context);
                return;
        }

        // Un elemento en línea (típicamente <a> en correos) que envuelve bloques se trata como bloque:
        // si no, títulos y párrafos de adentro quedarían pegados en una sola línea.
        if (BlockElements.Contains(name) || element.QuerySelector(BlockSelector) is not null)
        {
            // Tablas de maquetación, celdas, divs y párrafos: cada uno es un bloque aparte.
            context.Flush();
            RenderBlock(element, context, listDepth);
            context.Flush();
            return;
        }

        context.Inline.Append(RenderInlineElement(element, context));
    }

    private static void RenderList(IElement list, RenderContext context, int depth, bool ordered, string? key = null)
    {
        // Las sublistas comparten la clave de su lista madre: son la misma lista para Markdown.
        key ??= "list-" + context.NextListId();
        var number = 1;
        foreach (var item in list.Children.Where(c => c.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase) && !IsHidden(c)))
        {
            // El texto del elemento sin sus listas anidadas; las anidadas van después, más indentadas.
            var text = new StringBuilder();
            foreach (var child in item.ChildNodes)
            {
                if (child is IElement nested && (nested.LocalName is "ul" or "ol"))
                {
                    continue;
                }

                text.Append(child switch
                {
                    IText t => NormalizeText(t.Data),
                    IElement e when !IsHidden(e) => RenderInlineElement(e, context),
                    _ => string.Empty
                });
            }

            var marker = ordered ? $"{number++}." : "-";
            var line = MarkdownText.CollapseWhitespace(text.ToString());
            context.Builder.AddListItem(new string(' ', depth * 2) + marker + " " + line, key);

            foreach (var nested in item.Children.Where(c => c.LocalName is "ul" or "ol"))
            {
                RenderList(nested, context, depth + 1, nested.LocalName == "ol", key);
            }
        }
    }

    private static string RenderInline(INode node, RenderContext context)
    {
        var sb = new StringBuilder();
        foreach (var child in node.ChildNodes)
        {
            sb.Append(child switch
            {
                IText t => NormalizeText(t.Data),
                IElement e when !IsHidden(e) => RenderInlineElement(e, context),
                _ => string.Empty
            });
        }

        return sb.ToString();
    }

    private static string RenderInlineElement(IElement element, RenderContext context)
    {
        var name = element.LocalName.ToLowerInvariant();
        if (Ignored.Contains(name))
        {
            return string.Empty;
        }

        switch (name)
        {
            case "br":
                return "\n";
            case "img":
            {
                var alt = MarkdownText.CollapseWhitespace(element.GetAttribute("alt") ?? string.Empty);
                return alt.Length > 0 ? alt : string.Empty;
            }
        }

        var inner = RenderInline(element, context);
        var trimmed = inner.Trim();
        if (trimmed.Length == 0)
        {
            return inner.Length > 0 ? " " : string.Empty;
        }

        switch (name)
        {
            case "strong" or "b":
                return Wrap(inner, "**");
            case "em" or "i":
                return Wrap(inner, "*");
            case "s" or "strike" or "del":
                return Wrap(inner, "~~");
            case "code":
                return "`" + trimmed + "`";
            case "a":
            {
                var href = element.GetAttribute("href")?.Trim();
                if (!context.Options.IncludeLinkUrls || string.IsNullOrEmpty(href) ||
                    !(href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                      href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)))
                {
                    return inner;
                }

                var text = MarkdownText.CollapseWhitespace(trimmed);
                return text == href ? $"<{href}>" : $"[{text}]({href.Replace(" ", "%20").Replace(")", "%29")})";
            }

            default:
                return inner;
        }
    }

    /// <summary>Envuelve en marcas de énfasis respetando los espacios de los bordes (<c> **x** </c>, no <c>** x **</c>).</summary>
    private static string Wrap(string inner, string mark)
    {
        var leading = inner.Length - inner.TrimStart().Length;
        var trailing = inner.Length - inner.TrimEnd().Length;
        var core = MarkdownText.CollapseWhitespace(inner);
        return (leading > 0 ? " " : string.Empty) + mark + core + mark + (trailing > 0 ? " " : string.Empty);
    }

    /// <summary>
    /// Una tabla es de DATOS si no es de presentación, no tiene tablas anidadas ni bloques dentro de
    /// sus celdas y tiene al menos 2 filas y 2 columnas. El resto es maquetación (típico en correos).
    /// </summary>
    private static bool IsDataTable(IElement table)
    {
        if (string.Equals(table.GetAttribute("role"), "presentation", StringComparison.OrdinalIgnoreCase) ||
            table.QuerySelector("table") is not null)
        {
            return false;
        }

        var rows = Rows(table).Where(r => !IsHidden(r)).ToList();
        if (rows.Count < 2)
        {
            return false;
        }

        var columns = rows.Max(r => r.Children.Count(IsCell));
        if (columns < 2)
        {
            return false;
        }

        return rows.SelectMany(r => r.Children.Where(IsCell))
            .All(cell => cell.QuerySelector("div, p, h1, h2, h3, h4, h5, h6, ul, ol, blockquote, pre") is null);
    }

    private static void RenderDataTable(IElement table, RenderContext context)
    {
        var rows = Rows(table)
            .Where(r => !IsHidden(r))
            .Select(r => r.Children.Where(c => IsCell(c) && !IsHidden(c))
                .Select(c => MarkdownText.ForTableCell(MarkdownText.CollapseWhitespace(RenderInline(c, context))))
                .ToList())
            .Where(r => r.Any(c => c.Length > 0))
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var columns = rows.Max(r => r.Count);
        var sb = new StringBuilder();
        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Concat(Enumerable.Repeat(string.Empty, columns - rows[i].Count));
            sb.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
            if (i == 0)
            {
                sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", columns))).Append('\n');
            }
        }

        context.Builder.AddBlock(sb.ToString());
    }

    private static IEnumerable<IElement> Rows(IElement table) =>
        table.Children.SelectMany(c => c.LocalName is "thead" or "tbody" or "tfoot" ? c.Children.AsEnumerable() : new[] { c })
            .Where(c => c.LocalName.Equals("tr", StringComparison.OrdinalIgnoreCase));

    private static bool IsCell(IElement element) => element.LocalName is "td" or "th";

    /// <summary>Lo que un lector de correo no muestra: atributo hidden, display:none, visibility:hidden, mso-hide:all.</summary>
    private static bool IsHidden(IElement element)
    {
        if (Ignored.Contains(element.LocalName) || element.HasAttribute("hidden") ||
            string.Equals(element.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase) && element.TextContent.Trim().Length == 0)
        {
            return true;
        }

        var style = element.GetAttribute("style");
        if (string.IsNullOrEmpty(style))
        {
            return false;
        }

        // Además de display:none, los correos esconden copias de texto con opacidad 0, alto máximo 0 o
        // letra de 0–1 px (para lectores de pantalla o para el texto previo de la bandeja).
        var compact = style.Replace(" ", string.Empty).ToLowerInvariant();
        return compact.Contains("display:none") || compact.Contains("visibility:hidden") || compact.Contains("mso-hide:all") ||
               HasDeclaration(compact, "opacity:0") || HasDeclaration(compact, "max-height:0") ||
               HasDeclaration(compact, "font-size:0") || HasDeclaration(compact, "font-size:1px");
    }

    /// <summary>¿El estilo tiene exactamente esa declaración? (<c>opacity:0</c> sí, <c>opacity:0.5</c> no).</summary>
    private static bool HasDeclaration(string compactStyle, string declaration)
    {
        foreach (var part in compactStyle.Split(';'))
        {
            var value = part.Replace("!important", string.Empty);
            if (value == declaration || value == declaration + "px" || value == declaration + "%")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Quita caracteres invisibles, convierte espacios duros y colapsa los espacios como lo haría un navegador.</summary>
    private static string NormalizeText(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            if (Array.IndexOf(Invisible, ch) >= 0)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == ' ')
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            sb.Append(ch);
            lastWasSpace = false;
        }

        return sb.ToString();
    }

    private static string CompactBlankLines(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd());
        var sb = new StringBuilder();
        var blank = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (++blank > 1)
                {
                    continue;
                }
            }
            else
            {
                blank = 0;
            }

            sb.Append(line).Append('\n');
        }

        return sb.ToString().Trim('\n');
    }

    /// <summary>Estado de una conversión: bloques terminados, párrafo en curso y advertencias.</summary>
    private sealed class RenderContext(ConversionOptions options)
    {
        private int _listId;

        public ConversionOptions Options { get; } = options;

        public MarkdownDocumentBuilder Builder { get; } = new();

        public StringBuilder Inline { get; } = new();

        public List<string> Warnings { get; } = new();

        public int NextListId() => ++_listId;

        /// <summary>Cierra el párrafo en curso: líneas sin espacios sobrantes, sin líneas vacías repetidas.</summary>
        public void Flush()
        {
            if (Inline.Length == 0)
            {
                return;
            }

            var lines = Inline.ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
            var paragraph = string.Join("\n", lines.Select(MarkdownText.EscapeLineStart));
            Inline.Clear();
            Builder.AddBlock(paragraph);
        }
    }
}
