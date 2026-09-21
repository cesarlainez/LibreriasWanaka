namespace LibreriaQR.Contenidos;

/// <summary>(2) Texto plano. El lector solo muestra el texto (no ejecuta ninguna accion).</summary>
public sealed class ContenidoTexto : ContenidoQR
{
    /// <summary>Texto a codificar tal cual.</summary>
    public string Texto { get; set; } = string.Empty;

    public ContenidoTexto() { }

    public ContenidoTexto(string texto) => Texto = texto;

    public override TipoQR Tipo => TipoQR.TextoPlano;

    public override string ConstruirPayload()
    {
        // Aqui no exigimos "no vacio" con Trim: el texto puede ser deliberadamente algo con
        // espacios; pero si es null o totalmente vacio no tiene sentido un QR.
        if (string.IsNullOrEmpty(Texto))
            throw new QrContenidoInvalidoException($"El campo '{nameof(Texto)}' es obligatorio.");
        return Texto;
    }
}
