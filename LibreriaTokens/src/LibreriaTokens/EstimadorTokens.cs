namespace LibreriaTokens;

/// <summary>
/// Estimacion aproximada de tokens sin llamar al modelo. Sirve para loggear el
/// tamano de un prompt antes de mandarlo, decidir si vale la pena resumir o
/// avisar "vas al 80% del contexto" en el UI.
/// <para>
/// <b>NO es exacto.</b> Es una heuristica basada en el ratio caracteres/token
/// observado en espanol e ingles mezclado con JSON. Margen esperado: <b>±15 %</b>.
/// Si necesitas exactitud, usa el <c>usage</c> que devuelve el proveedor en la respuesta.
/// </para>
/// </summary>
public static class EstimadorTokens
{
    // Factores por familia. 1.0 = ratio base (aprox. 4 chars/token para GPT-4o).
    // Se sube levemente para tokenizadores mas antiguos (cl100k) porque el espanol,
    // acentos y palabras compuestas se rompen mas a menudo en subtokens.
    private const double FactorGpt4 = 1.00;
    private const double FactorGpt35 = 1.10;
    private const double FactorClaude = 1.00;
    private const double FactorGenerico = 1.00;

    /// <summary>
    /// Estima cuantos tokens ocupara <paramref name="texto"/> en la familia indicada.
    /// Devuelve 0 si el texto es null o vacio. Nunca lanza.
    /// </summary>
    public static int Estimar(string? texto, FamiliaModelo modelo = FamiliaModelo.Generico)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return 0;
        }

        var caracteres = texto.Length;
        var palabras = ContarPalabras(texto);

        // La aproximacion combina dos cotas:
        //   chars/4 sirve para texto denso con muchos numeros y simbolos.
        //   palabras*0.75 domina cuando hay palabras cortas separadas por espacios.
        // Tomamos el maximo para no subestimar.
        var estimacion = Math.Max(caracteres / 4d, palabras * 0.75d);
        estimacion *= FactorPorFamilia(modelo);

        return (int)Math.Ceiling(estimacion);
    }

    /// <summary>
    /// Version que estima solo a partir de la cantidad de caracteres (cuando no se
    /// tiene el texto a mano, por ejemplo despues de contar bytes de un stream).
    /// </summary>
    public static int EstimarCaracteres(int caracteres, FamiliaModelo modelo = FamiliaModelo.Generico)
    {
        if (caracteres <= 0)
        {
            return 0;
        }

        var estimacion = caracteres / 4d * FactorPorFamilia(modelo);
        return (int)Math.Ceiling(estimacion);
    }

    private static double FactorPorFamilia(FamiliaModelo modelo) => modelo switch
    {
        FamiliaModelo.OpenAiGpt4 => FactorGpt4,
        FamiliaModelo.OpenAiGpt35 => FactorGpt35,
        FamiliaModelo.AnthropicClaude => FactorClaude,
        _ => FactorGenerico,
    };

    private static int ContarPalabras(string texto)
    {
        // Regex seria mas legible pero corre en O(n) con allocations; para textos
        // grandes preferimos el barrido manual, que es lo mismo que "\S+".
        var total = 0;
        var dentroDePalabra = false;
        foreach (var c in texto)
        {
            if (char.IsWhiteSpace(c))
            {
                dentroDePalabra = false;
            }
            else if (!dentroDePalabra)
            {
                total++;
                dentroDePalabra = true;
            }
        }
        return total;
    }
}
