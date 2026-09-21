namespace LibreriaQR;

/// <summary>
/// Nivel de correccion de errores del QR. A mayor nivel, mas redundancia (el codigo
/// sigue leyendose aunque este sucio, arrugado o tape parte de el un logo), pero el
/// patron se vuelve mas denso y necesita mas resolucion para escanearse bien.
/// </summary>
public enum NivelCorreccion
{
    /// <summary>~7 % de recuperacion (ECC L). El mas ligero; para pantallas limpias.</summary>
    Bajo = 0,

    /// <summary>~15 % de recuperacion (ECC M). Valor por defecto, buen equilibrio.</summary>
    Medio = 1,

    /// <summary>~25 % de recuperacion (ECC Q).</summary>
    Alto = 2,

    /// <summary>~30 % de recuperacion (ECC H). Recomendado cuando se incrusta un logo.</summary>
    Maximo = 3,
}
