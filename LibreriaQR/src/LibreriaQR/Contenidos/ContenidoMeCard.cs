using System.Text;

namespace LibreriaQR.Contenidos;

/// <summary>
/// (7) Tarjeta de contacto simple (MECARD). Mas compacta que la vCard; util cuando solo se
/// quieren los datos basicos y un QR mas ligero. Muy soportada por lectores de Android.
/// <para>
/// Se genera el formato MECARD estandar de NTT DoCoMo (una sola linea, prefijo <c>MECARD:</c>,
/// campos separados por <c>;</c> y cierre <c>;;</c>), en vez del formato propio de QRCoder,
/// porque es el que los lectores reconocen como "agregar contacto".
/// </para>
/// </summary>
public sealed class ContenidoMeCard : ContenidoQR
{
    /// <summary>Nombre de pila.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellidos.</summary>
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Telefono.</summary>
    public string? Telefono { get; set; }

    /// <summary>Correo electronico.</summary>
    public string? Correo { get; set; }

    /// <summary>Direccion (linea unica).</summary>
    public string? Direccion { get; set; }

    /// <summary>Sitio web.</summary>
    public string? SitioWeb { get; set; }

    /// <summary>Nota libre.</summary>
    public string? Nota { get; set; }

    public override TipoQR Tipo => TipoQR.TarjetaMeCard;

    public override string ConstruirPayload()
    {
        if (string.IsNullOrWhiteSpace(Nombre) && string.IsNullOrWhiteSpace(Apellido))
            throw new QrContenidoInvalidoException("Se requiere al menos Nombre o Apellido.");

        var sb = new StringBuilder("MECARD:");

        // N:apellido,nombre  (los lectores esperan el apellido primero)
        sb.Append("N:")
          .Append(Escapar(Apellido))
          .Append(',')
          .Append(Escapar(Nombre))
          .Append(';');

        Campo(sb, "TEL", Telefono);
        Campo(sb, "EMAIL", Correo);
        Campo(sb, "ADR", Direccion);
        Campo(sb, "URL", SitioWeb);
        Campo(sb, "NOTE", Nota);

        sb.Append(';'); // cierre del registro -> queda ";;"
        return sb.ToString();
    }

    private static void Campo(StringBuilder sb, string etiqueta, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return;
        sb.Append(etiqueta).Append(':').Append(Escapar(valor)).Append(';');
    }

    private static string Escapar(string valor)
    {
        // MECARD escapa con '\' los caracteres estructurales. Los dos puntos NO se escapan
        // (los valores como URL "http://..." los llevan) porque el lector separa por etiqueta y ';'.
        var sb = new StringBuilder(valor.Length + 4);
        foreach (var c in valor)
        {
            switch (c)
            {
                case '\\':
                case ';':
                case ',':
                    sb.Append('\\').Append(c);
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append(' ');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
