namespace LibreriaQR;

/// <summary>
/// Configuracion del logo que se incrusta en el centro del QR. El logo se apoya en la
/// correccion de errores del codigo: por eso, cuando hay logo, el generador sube el nivel
/// de correccion a <see cref="NivelCorreccion.Maximo"/> salvo que se indique otro en
/// <see cref="OpcionesQR.Correccion"/>.
/// <para>
/// Aporte la imagen por <see cref="Bytes"/> o por <see cref="Ruta"/> (uno de los dos).
/// Formatos aceptados: PNG, JPG, BMP, GIF. Un PNG con transparencia se respeta.
/// </para>
/// </summary>
public sealed class OpcionesLogo
{
    /// <summary>Bytes de la imagen del logo. Tiene prioridad sobre <see cref="Ruta"/>.</summary>
    public byte[]? Bytes { get; set; }

    /// <summary>Ruta a un archivo de imagen del logo. Se usa si <see cref="Bytes"/> es null.</summary>
    public string? Ruta { get; set; }

    /// <summary>
    /// Fraccion del ancho del QR que ocupa el logo (0.05–0.35). Por defecto 0.22 (22 %).
    /// Por encima de 0.30 el codigo puede volverse ilegible aun con correccion maxima.
    /// </summary>
    public double Proporcion { get; set; } = 0.22;

    /// <summary>
    /// Si es true (por defecto) se dibuja un recuadro de fondo detras del logo para
    /// separarlo visualmente del patron y mejorar la lectura.
    /// </summary>
    public bool ConFondo { get; set; } = true;

    /// <summary>Color del recuadro de fondo del logo, en hex (<c>#RRGGBB</c> o <c>#RRGGBBAA</c>). Por defecto blanco.</summary>
    public string ColorFondo { get; set; } = "#FFFFFF";

    /// <summary>Margen en pixeles entre el logo y el borde de su recuadro de fondo. Por defecto 8.</summary>
    public int MargenPx { get; set; } = 8;

    /// <summary>
    /// Redondeo de las esquinas del recuadro de fondo, como fraccion del lado del recuadro
    /// (0 = esquinas rectas, 0.5 = circulo). Por defecto 0.18.
    /// </summary>
    public double RadioEsquinas { get; set; } = 0.18;
}
