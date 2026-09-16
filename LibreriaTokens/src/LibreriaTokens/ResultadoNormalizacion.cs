namespace LibreriaTokens;

/// <summary>
/// Resultado enriquecido de una normalizacion: texto limpio mas las metricas
/// que permiten loggear cuanto se ahorro. Pensado para poner en la traza
/// junto al costo del envio al LLM.
/// </summary>
public sealed class ResultadoNormalizacion
{
    /// <summary>Texto ya normalizado. Nunca es null.</summary>
    public string Texto { get; init; } = string.Empty;

    /// <summary>Longitud (en caracteres) del texto original.</summary>
    public int CaracteresOriginales { get; init; }

    /// <summary>Longitud (en caracteres) del texto resultante.</summary>
    public int CaracteresResultantes { get; init; }

    /// <summary>Cantidad de caracteres que se eliminaron (puede ser 0).</summary>
    public int CaracteresAhorrados => CaracteresOriginales - CaracteresResultantes;

    /// <summary>
    /// Porcentaje de reduccion en el rango 0..100. Es 0 cuando el texto original estaba vacio,
    /// para evitar divisiones por cero en el llamador.
    /// </summary>
    public double PorcentajeReduccion { get; init; }
}
