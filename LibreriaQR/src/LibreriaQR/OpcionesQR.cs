namespace LibreriaQR;

/// <summary>
/// Opciones de renderizado del QR: tamano, colores, zona quieta, nivel de correccion y logo.
/// Todas tienen un valor por defecto sensato; se puede pasar <c>null</c> al generador para usarlos todos.
/// </summary>
public sealed class OpcionesQR
{
    /// <summary>
    /// Pixeles por cada modulo (cuadrito) del QR. Por defecto 20. Se ignora si se fija
    /// <see cref="TamanoPx"/>. Valores mayores producen imagenes mas grandes y nitidas.
    /// </summary>
    public int PixelesPorModulo { get; set; } = 20;

    /// <summary>
    /// Tamano objetivo (ancho = alto) de la imagen final en pixeles. Si se indica, tiene
    /// prioridad sobre <see cref="PixelesPorModulo"/>: el generador calcula los pixeles por
    /// modulo mas grandes que quepan. El tamano real puede quedar algo por debajo del pedido
    /// porque siempre es multiplo entero de modulos (bordes nitidos, sin difuminado).
    /// </summary>
    public int? TamanoPx { get; set; }

    /// <summary>
    /// Nivel de correccion de errores. Si es null, se usa el recomendado por el contenido,
    /// o <see cref="NivelCorreccion.Maximo"/> cuando hay logo.
    /// </summary>
    public NivelCorreccion? Correccion { get; set; }

    /// <summary>Color de los modulos oscuros, en hex (<c>#RRGGBB</c> o <c>#RRGGBBAA</c>). Por defecto negro.</summary>
    public string ColorPrimerPlano { get; set; } = "#000000";

    /// <summary>Color del fondo, en hex. Por defecto blanco. Se ignora si <see cref="FondoTransparente"/> es true.</summary>
    public string ColorFondo { get; set; } = "#FFFFFF";

    /// <summary>Si es true, el fondo del PNG queda transparente (canal alfa). Por defecto false.</summary>
    public bool FondoTransparente { get; set; }

    /// <summary>
    /// Si es true (por defecto) se conserva la zona quieta (el margen blanco de 4 modulos
    /// alrededor del codigo) que exige el estandar para que los lectores lo detecten.
    /// Ponerlo en false solo si un contenedor externo ya aporta ese margen.
    /// </summary>
    public bool IncluirZonaQuieta { get; set; } = true;

    /// <summary>Logo a incrustar en el centro. Si es null, no se dibuja logo.</summary>
    public OpcionesLogo? Logo { get; set; }
}
