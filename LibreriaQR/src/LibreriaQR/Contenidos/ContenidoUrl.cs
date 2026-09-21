using QRCoder;

namespace LibreriaQR.Contenidos;

/// <summary>(1) Enlace web. Al escanear, abre la URL en el navegador.</summary>
public sealed class ContenidoUrl : ContenidoQR
{
    /// <summary>URL a abrir. Si no trae esquema, se le antepone <c>http://</c>.</summary>
    public string Url { get; set; } = string.Empty;

    public ContenidoUrl() { }

    public ContenidoUrl(string url) => Url = url;

    public override TipoQR Tipo => TipoQR.EnlaceWeb;

    public override string ConstruirPayload()
        => new PayloadGenerator.Url(Exigir(Url, nameof(Url))).ToString();
}
