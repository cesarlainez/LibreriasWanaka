namespace LibreriaQR.Contenidos;

/// <summary>
/// Clase base de todos los contenidos de QR. Cada tipo (URL, WiFi, vCard, etc.) es una
/// subclase que sabe (a) que <see cref="Tipo"/> representa y (b) como construir su cadena
/// de texto final (<see cref="ConstruirPayload"/>) con el formato que los lectores entienden.
/// </summary>
public abstract class ContenidoQR
{
    /// <summary>Tipo de contenido que representa esta instancia.</summary>
    public abstract TipoQR Tipo { get; }

    /// <summary>
    /// Nivel de correccion de errores recomendado para este contenido cuando el llamador
    /// no fija uno. Por defecto <see cref="NivelCorreccion.Medio"/>.
    /// </summary>
    public virtual NivelCorreccion CorreccionRecomendada => NivelCorreccion.Medio;

    /// <summary>
    /// Valida los datos y construye la cadena de texto que se codificara en el QR.
    /// Lanza <see cref="QrContenidoInvalidoException"/> si faltan campos obligatorios o
    /// hay valores fuera de rango.
    /// </summary>
    public abstract string ConstruirPayload();

    /// <summary>Atajo interno para exigir un campo obligatorio.</summary>
    private protected static string Exigir(string? valor, string nombreCampo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new QrContenidoInvalidoException($"El campo '{nombreCampo}' es obligatorio.");
        return valor.Trim();
    }
}
