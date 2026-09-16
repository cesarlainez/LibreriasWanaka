namespace LibreriaTokens;

/// <summary>
/// Familia de tokenizador a asumir en <see cref="EstimadorTokens"/>. Solo afecta
/// el factor de ajuste: la estimacion es siempre aproximada.
/// </summary>
public enum FamiliaModelo
{
    /// <summary>Aproximacion neutra, sin ajustes. Es el default cuando no se conoce el modelo.</summary>
    Generico = 0,

    /// <summary>Modelos gpt-4o / gpt-4.1 / gpt-5 (tokenizador o200k_base).</summary>
    OpenAiGpt4 = 1,

    /// <summary>Modelos gpt-3.5 / gpt-4 clasico (tokenizador cl100k_base). Espanol es levemente mas caro.</summary>
    OpenAiGpt35 = 2,

    /// <summary>Familia Claude de Anthropic. Ratio de caracteres por token comparable a GPT-4o.</summary>
    AnthropicClaude = 3,
}
