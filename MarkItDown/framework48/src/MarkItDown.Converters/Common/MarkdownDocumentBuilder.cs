using System.Text;

namespace MarkItDown.Converters.Common;

/// <summary>
/// Acumula bloques de Markdown (párrafos, encabezados, tablas, elementos de lista)
/// y los une con la separación correcta: los elementos de una misma lista van en líneas
/// contiguas, entre listas distintas se inserta un separador que obliga a Markdown a
/// cerrar la lista anterior (y reiniciar numeración), y el resto de bloques se separa
/// con una línea en blanco.
/// </summary>
internal sealed class MarkdownDocumentBuilder
{
    // Bloque no-lista entre dos listas: sin él, CommonMark las fusionaría en una sola
    // lista y la numeración de la segunda continuaría la de la primera.
    private const string ListSeparator = "<!-- -->";

    private readonly List<(string Text, bool IsListItem, string? ListKey)> _blocks = new();

    public void AddBlock(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _blocks.Add((text.TrimEnd(), false, null));
        }
    }

    /// <param name="line">Línea completa del elemento, con su indentación y marcador.</param>
    /// <param name="listKey">Identidad de la lista a la que pertenece el elemento; elementos
    /// consecutivos con distinta clave se separan para que Markdown no fusione las listas.</param>
    public void AddListItem(string line, string listKey)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            _blocks.Add((line.TrimEnd(), true, listKey));
        }
    }

    public bool IsEmpty => _blocks.Count == 0;

    /// <summary>Indica si el último bloque agregado es un elemento de lista.</summary>
    public bool LastIsListItem => _blocks.Count > 0 && _blocks[_blocks.Count - 1].IsListItem;

    public string Build()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _blocks.Count; i++)
        {
            if (i > 0)
            {
                var previous = _blocks[i - 1];
                var current = _blocks[i];
                if (previous.IsListItem && current.IsListItem)
                {
                    sb.Append(previous.ListKey == current.ListKey
                        ? "\n"
                        : "\n\n" + ListSeparator + "\n\n");
                }
                else
                {
                    sb.Append("\n\n");
                }
            }

            sb.Append(_blocks[i].Text);
        }

        if (sb.Length > 0)
        {
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
