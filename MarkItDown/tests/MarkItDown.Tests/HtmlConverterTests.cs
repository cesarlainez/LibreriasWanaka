using System.Text;
using MarkItDown.Converters.Html;
using MarkItDown.Core;

namespace MarkItDown.Tests;

public class HtmlConverterTests
{
    private static string Convertir(string html, bool enlaces = true) =>
        new HtmlToMarkdownConverter().ConvertHtml(html, new ConversionOptions { IncludeLinkUrls = enlaces }).Markdown;

    [Fact]
    public void Encabezados_parrafos_y_enfasis()
    {
        var md = Convertir("<html><head><title>Hola</title><style>p{color:red}</style></head><body><h1>Título</h1><p>Uno <b>dos</b> <em>tres</em></p><p>Cuatro<br>Cinco</p></body></html>");

        Assert.Equal("# Título\n\nUno **dos** *tres*\n\nCuatro\nCinco", md);
    }

    [Fact]
    public void Omite_scripts_y_elementos_ocultos_de_correos()
    {
        var md = Convertir("""
            <body>
              <script>alert(1)</script>
              <div style="display:none">Texto previo de la bandeja</div>
              <span style="max-height:0;max-width:0;opacity:0;font-size:1px">Copia para lectores</span>
              <span style="mso-hide: all">Solo Outlook</span>
              <p>Visible&nbsp;͏ ͏ ­texto</p>
            </body>
            """);

        Assert.Equal("Visible texto", md);
    }

    [Fact]
    public void Tablas_de_maquetacion_se_aplanan_y_las_de_datos_se_dibujan()
    {
        var md = Convertir("""
            <table role="presentation"><tr><td><h2>Cobro del anfitrión</h2></td></tr>
              <tr><td>
                <table><tr><td>Limpieza</td><td>25,00 $</td></tr><tr><td>Ganas</td><td>118,23 $</td></tr></table>
              </td></tr>
            </table>
            """);

        Assert.Equal("## Cobro del anfitrión\n\n| Limpieza | 25,00 $ |\n| --- | --- |\n| Ganas | 118,23 $ |", md);
    }

    [Fact]
    public void Enlace_que_envuelve_bloques_no_pega_los_textos()
    {
        var md = Convertir("<a href=\"https://x.test\"><table><tr><td><h2>Casa</h2></td></tr></table><p>Apto. entero</p></a>");

        Assert.Equal("## Casa\n\nApto. entero", md);
    }

    [Fact]
    public void Enlaces_con_y_sin_url()
    {
        const string html = "<p>Ver <a href=\"https://www.airbnb.com/rooms/123\">anuncio</a> y <a href=\"javascript:void(0)\">nada</a></p>";

        Assert.Equal("Ver [anuncio](https://www.airbnb.com/rooms/123) y nada", Convertir(html));
        Assert.Equal("Ver anuncio y nada", Convertir(html, enlaces: false));
    }

    [Fact]
    public void Listas_anidadas_e_imagenes_por_texto_alternativo()
    {
        var md = Convertir("<ul><li>Uno<ol><li>Uno.a</li></ol></li><li><img alt=\"Logo\" src=\"x.png\"> Dos</li></ul>");

        Assert.Equal("- Uno\n  1. Uno.a\n- Logo Dos", md);
    }

    [Fact]
    public void La_fachada_reconoce_html_y_respeta_la_codificacion()
    {
        var bytes = Encoding.UTF8.GetBytes("<html><head><meta charset=\"utf-8\"></head><body><p>Ubicación</p></body></html>");
        using var stream = new MemoryStream(bytes);

        var resultado = new MarkItDownConverter().Convert(stream, "correo.html");

        Assert.Equal("Ubicación", resultado.Markdown);
        Assert.Contains(".htm", new MarkItDownConverter().SupportedExtensions);
    }
}
