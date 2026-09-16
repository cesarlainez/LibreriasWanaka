using System.Text;

namespace LibreriaTokens;

/// <summary>
/// Reduce el whitespace redundante de un bloque de texto antes de mandarlo a un LLM.
/// <para>
/// Todos los metodos son estaticos y sin estado: es seguro llamarlos en paralelo desde
/// cualquier hilo. El algoritmo hace una sola pasada por el string usando un
/// <see cref="StringBuilder"/> con capacidad inicial igual a la del texto, por lo que
/// es O(n) en tiempo y memoria.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var opciones = new OpcionesNormalizacion();
/// var limpio = NormalizadorTexto.Normalizar(textoDelPdf, opciones);
/// var envio = new { model = "gpt-4o", input = limpio };
/// </code>
/// </example>
public static class NormalizadorTexto
{
    /// <summary>
    /// Normaliza el texto segun las <paramref name="opciones"/> (o los defaults si es null).
    /// Nunca lanza: entradas <c>null</c> o vacias devuelven <see cref="string.Empty"/>.
    /// </summary>
    public static string Normalizar(string? texto, OpcionesNormalizacion? opciones = null)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return string.Empty;
        }

        return NormalizarInterno(texto, opciones ?? new OpcionesNormalizacion());
    }

    /// <summary>
    /// Igual que <see cref="Normalizar(string?, OpcionesNormalizacion?)"/> pero ademas
    /// devuelve las metricas del ahorro. Util para loggear "el prompt paso de X a Y chars".
    /// </summary>
    public static ResultadoNormalizacion NormalizarConMetricas(string? texto, OpcionesNormalizacion? opciones = null)
    {
        var originales = texto?.Length ?? 0;
        var resultado = Normalizar(texto, opciones);
        var reduccion = originales == 0 ? 0d : (originales - resultado.Length) * 100d / originales;

        return new ResultadoNormalizacion
        {
            Texto = resultado,
            CaracteresOriginales = originales,
            CaracteresResultantes = resultado.Length,
            PorcentajeReduccion = reduccion,
        };
    }

    // ------------------------------------------------------------------ Nucleo

    /// <remarks>
    /// La idea del algoritmo es ir buffereando espacios y saltos pendientes y solo
    /// emitirlos cuando aparece contenido "de verdad". Con eso salen gratis tres cosas
    /// que suelen escribirse en pasadas separadas:
    ///   - los espacios al final de linea nunca se emiten (aparecen sin contenido detras),
    ///   - las lineas en blanco iniciales tampoco (nunca hubo contenido delante),
    ///   - las finales se descartan al terminar el bucle sin haberse volcado.
    /// </remarks>
    private static string NormalizarInterno(string texto, OpcionesNormalizacion o)
    {
        var sb = new StringBuilder(texto.Length);
        var maxSaltos = o.ColapsarSaltosDeLinea ? Math.Max(1, o.MaximoSaltosConsecutivos) : int.MaxValue;
        var espaciosPorTab = Math.Max(0, o.EspaciosPorTab);

        var espaciosPendientes = 0;
        var saltosPendientes = 0;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];

            // --- normalizacion de finales de linea a \n --------------------------------
            if (c == '\r')
            {
                // \r\n se colapsa a \n; \r suelto (Mac clasico) tambien.
                if (i + 1 < texto.Length && texto[i + 1] == '\n')
                {
                    continue;
                }
                c = '\n';
            }

            // --- salto de linea --------------------------------------------------------
            if (c == '\n')
            {
                // Si el modo esta activo, los espacios acumulados en esta linea eran
                // trailing y se descartan. Si el modo esta apagado, los volcamos antes
                // del salto para conservar el layout original.
                if (!o.QuitarEspaciosAlFinalDeLinea)
                {
                    FlushEspacios(sb, ref espaciosPendientes, o.ColapsarEspaciosInternos);
                }
                else
                {
                    espaciosPendientes = 0;
                }

                saltosPendientes++;
                continue;
            }

            // --- caracteres unicode raros ---------------------------------------------
            if (o.NormalizarWhitespaceUnicode)
            {
                if (EsAnchoCero(c))
                {
                    // Los ancho-cero (ZWSP, ZWJ, BOM) son invisibles pero cuentan como
                    // token propio en la mayoria de tokenizadores. Se eliminan sin dejar
                    // rastro.
                    continue;
                }
                if (EsEspacioUnicode(c))
                {
                    c = ' ';
                }
            }

            // --- tabulador ------------------------------------------------------------
            if (c == '\t')
            {
                if (o.ConvertirTabuladores)
                {
                    espaciosPendientes += espaciosPorTab;
                    continue;
                }
                // Con la conversion apagada tratamos el tab como contenido: volcamos
                // pendientes y lo emitimos tal cual para preservar el layout.
                FlushSaltos(sb, ref saltosPendientes, maxSaltos);
                FlushEspacios(sb, ref espaciosPendientes, o.ColapsarEspaciosInternos);
                sb.Append('\t');
                continue;
            }

            // --- espacio --------------------------------------------------------------
            if (c == ' ')
            {
                espaciosPendientes++;
                continue;
            }

            // --- contenido real --------------------------------------------------------
            FlushSaltos(sb, ref saltosPendientes, maxSaltos);

            // Los espacios pendientes solo se emiten si NO estamos al comienzo de una
            // linea (ni al inicio del bloque): la indentacion de linea tampoco aporta.
            var estamosAlInicioDeLinea = sb.Length == 0 || sb[sb.Length - 1] == '\n';
            if (estamosAlInicioDeLinea)
            {
                espaciosPendientes = 0;
            }
            else
            {
                FlushEspacios(sb, ref espaciosPendientes, o.ColapsarEspaciosInternos);
            }

            sb.Append(c);
        }

        // Los espacios y saltos que quedan pendientes al terminar el bucle son trailing
        // del bloque completo: RecortarBloque=true los descarta simplemente no volcandolos.
        // Si el llamador pidio no recortar, los volcamos ahora.
        if (!o.RecortarBloque)
        {
            FlushSaltos(sb, ref saltosPendientes, maxSaltos);
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
            {
                FlushEspacios(sb, ref espaciosPendientes, o.ColapsarEspaciosInternos);
            }
        }

        return sb.ToString();
    }

    private static void FlushSaltos(StringBuilder sb, ref int pendientes, int maximo)
    {
        if (pendientes <= 0)
        {
            return;
        }

        // No emitir saltos iniciales: al comienzo del bloque no hay contenido detras
        // que separar. Esto implementa "quitar lineas 100% en blanco al inicio".
        if (sb.Length == 0)
        {
            pendientes = 0;
            return;
        }

        var aEmitir = Math.Min(pendientes, maximo);
        for (var k = 0; k < aEmitir; k++)
        {
            sb.Append('\n');
        }
        pendientes = 0;
    }

    private static void FlushEspacios(StringBuilder sb, ref int pendientes, bool colapsar)
    {
        if (pendientes <= 0)
        {
            return;
        }

        var aEmitir = colapsar ? 1 : pendientes;
        for (var k = 0; k < aEmitir; k++)
        {
            sb.Append(' ');
        }
        pendientes = 0;
    }

    // ------------------------------------------------------------------ Unicode

    private static bool EsAnchoCero(char c)
    {
        // ZWSP, ZWNJ, ZWJ, WORD JOINER, BOM/ZWNBSP. Nada visible.
        return c == '​' || c == '‌' || c == '‍' || c == '⁠' || c == '﻿';
    }

    private static bool EsEspacioUnicode(char c)
    {
        // Espacios "de imprenta" o justificacion que un tokenizador trata como token
        // propio en vez de fundir con el vecino. Ojo: NO incluimos \t/\n aca, que se
        // manejan aparte.
        if (c == ' ' || c == ' ' || c == ' ' || c == ' ' || c == '　')
        {
            return true;
        }
        // Bloque General Punctuation U+2000..U+200A: EN QUAD, EM QUAD, EN SPACE, EM SPACE,
        // THREE-PER-EM SPACE, FOUR-PER-EM SPACE, SIX-PER-EM SPACE, FIGURE SPACE,
        // PUNCTUATION SPACE, THIN SPACE, HAIR SPACE.
        return c >= ' ' && c <= ' ';
    }
}
