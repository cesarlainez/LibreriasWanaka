using System.Text;
using System.Text.RegularExpressions;

namespace MarkItDown.Converters.Common;

/// <summary>Utilidades de escape de texto para generar Markdown válido.</summary>
internal static class MarkdownText
{
    private static readonly Regex OrderedListLikeStart = new(@"^(\s*)(\d{1,9})([.)])(\s|$)", RegexOptions.Compiled);

    /// <summary>
    /// Escapa los caracteres que Markdown interpretaría como formato dentro de una línea
    /// (\ ` * _ [ ] &lt; &gt; ~), para que el texto original se muestre tal cual.
    /// </summary>
    internal static string EscapeInline(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text!.Length + 8);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\':
                case '`':
                case '*':
                case '_':
                case '[':
                case ']':
                case '<':
                case '>':
                case '~':
                    sb.Append('\\').Append(c);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Evita que un párrafo normal se interprete como encabezado, lista, cita, regla horizontal
    /// o subrayado setext. Procesa cada línea física del bloque (los saltos suaves "  \n"
    /// generan líneas internas que también deben protegerse).
    /// </summary>
    internal static string EscapeLineStart(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (text.IndexOf('\n') < 0)
        {
            return EscapeSingleLineStart(text);
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = EscapeSingleLineStart(lines[i]);
        }

        return string.Join("\n", lines);
    }

    private static string EscapeSingleLineStart(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var ordered = OrderedListLikeStart.Match(line);
        if (ordered.Success)
        {
            return ordered.Groups[1].Value + ordered.Groups[2].Value + "\\" + ordered.Groups[3].Value +
                   line.Substring(ordered.Groups[1].Length + ordered.Groups[2].Length + ordered.Groups[3].Length);
        }

        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        if (i >= line.Length)
        {
            return line;
        }

        var first = line[i];
        var next = i + 1 < line.Length ? line[i + 1] : ' ';
        var startsBlock = first switch
        {
            '>' => true,
            '#' or '+' or '*' => next is ' ' or '\t' || i + 1 >= line.Length,
            // "- x" es lista; "---"/"- - -" es regla horizontal o subrayado setext.
            '-' => next is ' ' or '\t' || i + 1 >= line.Length || IsMarkerRun(line, i, '-'),
            // "===" bajo un párrafo es subrayado setext (encabezado nivel 1).
            '=' => IsMarkerRun(line, i, '='),
            _ => false,
        };

        return startsBlock ? line.Substring(0, i) + "\\" + line.Substring(i) : line;
    }

    private static bool IsMarkerRun(string line, int start, char marker)
    {
        for (var j = start; j < line.Length; j++)
        {
            var c = line[j];
            if (c != marker && c != ' ' && c != '\t')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Prepara texto para ir dentro de una celda de tabla Markdown.</summary>
    internal static string ForTableCell(string text)
    {
        return text
            .Replace("\r\n", "<br>")
            .Replace("\r", "<br>")
            .Replace("\n", "<br>")
            .Replace("|", "\\|")
            .Trim();
    }

    /// <summary>Colapsa cualquier secuencia de espacios/saltos en un solo espacio (útil para encabezados).</summary>
    internal static string CollapseWhitespace(string text)
    {
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
