namespace LibreriaQR;

/// <summary>Resultado de generar un QR: la imagen en varios formatos y algunos metadatos.</summary>
public sealed class ResultadoQR
{
    /// <summary>Imagen PNG del QR (bytes crudos). Nunca es null.</summary>
    public byte[] Png { get; init; } = Array.Empty<byte>();

    /// <summary>La imagen PNG codificada en Base64, <b>sin</b> el prefijo <c>data:</c>.</summary>
    public string Base64 { get; init; } = string.Empty;

    /// <summary>
    /// Data URI listo para pegar en el atributo <c>src</c> de un <c>&lt;img&gt;</c> HTML:
    /// <c>data:image/png;base64,....</c>.
    /// </summary>
    public string DataUri { get; init; } = string.Empty;

    /// <summary>Tipo de contenido que se codifico.</summary>
    public TipoQR Tipo { get; init; }

    /// <summary>El texto exacto que quedo dentro del QR (por ejemplo <c>WIFI:T:WPA;S:...;</c>).</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Ancho de la imagen en pixeles.</summary>
    public int AnchoPx { get; init; }

    /// <summary>Alto de la imagen en pixeles.</summary>
    public int AltoPx { get; init; }

    /// <summary>Nivel de correccion de errores realmente aplicado.</summary>
    public NivelCorreccion Correccion { get; init; }

    /// <summary>Indica si la imagen lleva logo incrustado.</summary>
    public bool TieneLogo { get; init; }

    /// <summary>Guarda el PNG en disco.</summary>
    /// <param name="ruta">Ruta destino del archivo (se sobrescribe si existe).</param>
    public void GuardarPng(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            throw new ArgumentException("La ruta destino esta vacia.", nameof(ruta));
        File.WriteAllBytes(ruta, Png);
    }

    /// <summary>Devuelve el data URI (util para bindings directos en UI).</summary>
    public override string ToString() => DataUri;
}
