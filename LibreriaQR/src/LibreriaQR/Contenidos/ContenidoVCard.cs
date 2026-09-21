using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(6) Tarjeta de contacto completa (vCard). Al escanear, ofrece guardar el contacto.</summary>
public sealed class ContenidoVCard : ContenidoQR
{
    /// <summary>Nombre de pila.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellidos.</summary>
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Empresa u organizacion.</summary>
    public string? Empresa { get; set; }

    /// <summary>Cargo o puesto.</summary>
    public string? Cargo { get; set; }

    /// <summary>Telefono fijo.</summary>
    public string? Telefono { get; set; }

    /// <summary>Telefono movil.</summary>
    public string? Movil { get; set; }

    /// <summary>Telefono de trabajo.</summary>
    public string? TelefonoTrabajo { get; set; }

    /// <summary>Correo electronico.</summary>
    public string? Correo { get; set; }

    /// <summary>Sitio web.</summary>
    public string? SitioWeb { get; set; }

    /// <summary>Calle y numero (linea de direccion).</summary>
    public string? Direccion { get; set; }

    /// <summary>Ciudad.</summary>
    public string? Ciudad { get; set; }

    /// <summary>Departamento / estado / region.</summary>
    public string? Region { get; set; }

    /// <summary>Codigo postal.</summary>
    public string? CodigoPostal { get; set; }

    /// <summary>Pais.</summary>
    public string? Pais { get; set; }

    /// <summary>Nota libre.</summary>
    public string? Nota { get; set; }

    /// <summary>Version del formato vCard. Por defecto 3.0.</summary>
    public VersionVCard Version { get; set; } = VersionVCard.V3;

    public override TipoQR Tipo => TipoQR.TarjetaVCard;

    // Una vCard es larga; con correccion media el QR queda muy denso. Subimos a Alto.
    public override NivelCorreccion CorreccionRecomendada => NivelCorreccion.Alto;

    public override string ConstruirPayload()
    {
        if (string.IsNullOrWhiteSpace(Nombre) && string.IsNullOrWhiteSpace(Apellido))
            throw new QrContenidoInvalidoException("Se requiere al menos Nombre o Apellido.");

        var salida = Version switch
        {
            VersionVCard.V21 => PayloadGenerator.ContactData.ContactOutputType.VCard21,
            VersionVCard.V4 => PayloadGenerator.ContactData.ContactOutputType.VCard4,
            _ => PayloadGenerator.ContactData.ContactOutputType.VCard3,
        };

        return new PayloadGenerator.ContactData(
            outputType: salida,
            firstname: Nombre ?? string.Empty,
            lastname: Apellido ?? string.Empty,
            nickname: null,
            phone: Telefono,
            mobilePhone: Movil,
            workPhone: TelefonoTrabajo,
            email: Correo,
            birthday: null,
            website: SitioWeb,
            street: Direccion,
            houseNumber: null,
            city: Ciudad,
            zipCode: CodigoPostal,
            country: Pais,
            note: Nota,
            stateRegion: Region,
            org: Empresa ?? string.Empty,
            orgTitle: Cargo ?? string.Empty).ToString();
    }
}
