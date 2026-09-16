namespace LibreriaTokens;

/// <summary>
/// Reglas configurables para <see cref="NormalizadorTexto"/>. Los valores por defecto
/// son los agresivos: quitan todo el whitespace que un LLM no necesita para entender
/// el texto, preservando los saltos dobles (que marcan párrafo).
/// </summary>
public sealed class OpcionesNormalizacion
{
    /// <summary>
    /// Si es true, corta las rachas de saltos de linea al valor de
    /// <see cref="MaximoSaltosConsecutivos"/>. Se conserva el doble salto porque un LLM
    /// lo interpreta como separacion de parrafo y ayuda a la comprension.
    /// </summary>
    public bool ColapsarSaltosDeLinea { get; set; } = true;

    /// <summary>
    /// Maximo de <c>\n</c> consecutivos que se dejan en el resultado. Por defecto 2
    /// (parrafos). Se ignora si <see cref="ColapsarSaltosDeLinea"/> es false.
    /// </summary>
    public int MaximoSaltosConsecutivos { get; set; } = 2;

    /// <summary>
    /// Colapsa 2+ espacios internos consecutivos (dentro de una linea no vacia) a uno solo.
    /// Se activa por defecto porque el padding de columnas de PDF u OCR no aporta al modelo.
    /// </summary>
    public bool ColapsarEspaciosInternos { get; set; } = true;

    /// <summary>
    /// Convierte cada tabulador (<c>\t</c>) a <see cref="EspaciosPorTab"/> espacios. En texto
    /// natural (no codigo) un tab no aporta layout y ademas cuenta como token propio.
    /// </summary>
    public bool ConvertirTabuladores { get; set; } = true;

    /// <summary>
    /// Cantidad de espacios por tabulador cuando <see cref="ConvertirTabuladores"/> es true.
    /// Se combina con <see cref="ColapsarEspaciosInternos"/>: si es 1 el efecto neto es
    /// "tab convertido en un solo espacio".
    /// </summary>
    public int EspaciosPorTab { get; set; } = 1;

    /// <summary>
    /// Elimina los espacios y tabs al final de cada linea (trailing whitespace).
    /// Es whitespace invisible que suma tokens sin razon.
    /// </summary>
    public bool QuitarEspaciosAlFinalDeLinea { get; set; } = true;

    /// <summary>
    /// Recorta el bloque completo: quita saltos y espacios al inicio y al final del texto
    /// resultante. Un bloque enviado a un LLM casi nunca necesita padding externo.
    /// </summary>
    public bool RecortarBloque { get; set; } = true;

    /// <summary>
    /// Convierte los espacios exoticos de Unicode (NBSP <c> </c>, thin space
    /// <c> </c>, ideographic space <c>　</c>, etc.) a un espacio normal, y
    /// elimina los de ancho cero (<c>​</c> ZWSP, BOM). Muchos PDFs y correos meten
    /// estos caracteres y los tokenizadores los tratan como tokens sueltos.
    /// </summary>
    public bool NormalizarWhitespaceUnicode { get; set; } = true;
}
