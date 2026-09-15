using MarkItDown.Core;

namespace MarkItDown.Tests;

public class DocumentToMarkdownCleanerTests
{
    private readonly DocumentToMarkdownCleaner _cleaner = new();

    // ---------------------- Paginación ----------------------

    [Theory]
    [InlineData("Página 5")]
    [InlineData("Pagina 5")]
    [InlineData("Pág. 5")]
    [InlineData("Pag 5")]
    [InlineData("P. 5")]
    [InlineData("Página 5 de 20")]
    [InlineData("Pág 5/20")]
    [InlineData("PÁGINA 5")]
    public void Normaliza_indicadores_de_pagina(string linea)
    {
        var entrada = $"Contenido anterior.\n{linea}\nContenido posterior.";

        var salida = _cleaner.Clean(entrada);

        Assert.Contains("**[Página 5]**", salida);
        Assert.Contains("---", salida);
        Assert.Contains("Contenido anterior.", salida);
        Assert.Contains("Contenido posterior.", salida);
    }

    [Fact]
    public void Conserva_el_numero_de_pagina_para_trazabilidad()
    {
        var salida = _cleaner.Clean("Texto\nPágina 137 de 200\nTexto");

        Assert.Contains("**[Página 137]**", salida);
    }

    [Fact]
    public void No_convierte_referencias_de_pagina_dentro_de_una_frase()
    {
        const string entrada = "Como se indica, vea la Página 5 del manual para más detalles.";

        var salida = _cleaner.Clean(entrada);

        Assert.Equal(entrada, salida);
        Assert.DoesNotContain("**[Página", salida);
    }

    [Fact]
    public void No_convierte_lineas_de_contenido_que_empiezan_similar()
    {
        const string entrada = "Artículo 5 de la Ley de Seguros vigente.";

        var salida = _cleaner.Clean(entrada);

        Assert.Equal(entrada, salida);
    }

    [Fact]
    public void Elimina_fecha_de_impresion_pegada_a_la_paginacion()
    {
        var salida = _cleaner.Clean("Texto\n01/01/2024 10:30 Página 4 de 10\nTexto");

        Assert.Contains("**[Página 4]**", salida);
        Assert.DoesNotContain("01/01/2024", salida);
        Assert.DoesNotContain("10:30", salida);
    }

    [Fact]
    public void Elimina_caratula_pegada_a_la_paginacion()
    {
        var salida = _cleaner.Clean("Texto\nPágina 4 de 10 Carátula\nTexto");

        Assert.Contains("**[Página 4]**", salida);
        Assert.DoesNotContain("Carátula", salida, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Conserva_una_fecha_que_no_esta_pegada_a_paginacion()
    {
        // Una fecha en su propia línea de contenido NO debe borrarse.
        const string entrada = "El contrato inicia el 01/01/2024 y es válido por un año.";

        var salida = _cleaner.Clean(entrada);

        Assert.Contains("01/01/2024", salida);
    }

    [Fact]
    public void Palabras_clave_adicionales_configurables()
    {
        var cleaner = new DocumentToMarkdownCleaner(new DocumentToMarkdownCleaner.CleanerOptions
        {
            AdditionalNoiseKeywords = new[] { "Confidencial" },
        });

        var salida = cleaner.Clean("Texto\nPágina 2 Confidencial\nTexto");

        Assert.Contains("**[Página 2]**", salida);
        Assert.DoesNotContain("Confidencial", salida);
    }

    [Fact]
    public void El_separador_de_pagina_queda_rodeado_de_lineas_en_blanco()
    {
        // Evita que "---" se interprete como subrayado setext del texto anterior.
        var salida = _cleaner.Clean("Fin de la página uno.\nPágina 1\nInicio de la página dos.");

        Assert.Contains("uno.\n\n---\n\n**[Página 1]**\n\n---\n\nInicio", salida);
    }

    // ---------------------- Artefactos de OCR ----------------------

    [Fact]
    public void Elimina_comentarios_html_vacios()
    {
        var salida = _cleaner.Clean("Antes <!-- --> después");

        Assert.DoesNotContain("<!--", salida);
        Assert.Contains("Antes", salida);
        Assert.Contains("después", salida);
    }

    [Fact]
    public void Elimina_secuencias_largas_de_guiones_bajos()
    {
        var salida = _cleaner.Clean("Firma: ________________");

        Assert.DoesNotContain("__", salida);
        Assert.Contains("Firma:", salida);
    }

    [Fact]
    public void Conserva_secuencias_cortas_de_guiones_bajos()
    {
        // 4 guiones bajos no se tocan (umbral conservador de 6+).
        const string entrada = "valor____campo";

        var salida = _cleaner.Clean(entrada);

        Assert.Equal(entrada, salida);
    }

    // ---------------------- Compactación ----------------------

    [Fact]
    public void Compacta_tres_o_mas_saltos_de_linea()
    {
        var salida = _cleaner.Clean("Párrafo uno.\n\n\n\n\nPárrafo dos.");

        Assert.Equal("Párrafo uno.\n\nPárrafo dos.", salida);
    }

    [Fact]
    public void No_une_parrafos_separados_por_un_solo_doble_salto()
    {
        const string entrada = "Párrafo uno.\n\nPárrafo dos.";

        var salida = _cleaner.Clean(entrada);

        Assert.Equal(entrada, salida);
    }

    [Fact]
    public void Trata_lineas_de_solo_espacios_como_vacias()
    {
        var salida = _cleaner.Clean("Uno.\n   \n   \n   \nDos.");

        Assert.Equal("Uno.\n\nDos.", salida);
    }

    // ---------------------- Robustez / propiedades ----------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Entrada_nula_o_vacia_devuelve_cadena_vacia(string? entrada)
    {
        Assert.Equal(string.Empty, _cleaner.Clean(entrada));
    }

    [Fact]
    public void Normaliza_fin_de_linea_windows_y_mac()
    {
        var salida = _cleaner.Clean("uno\r\ndos\rtres");

        Assert.DoesNotContain("\r", salida);
        Assert.Equal("uno\ndos\ntres", salida);
    }

    [Fact]
    public void Es_idempotente()
    {
        const string entrada =
            "Introducción del documento.\n" +
            "01/01/2024 Página 1 de 3 Carátula\n" +
            "Cuerpo con <!-- --> y firma ________________\n\n\n\n" +
            "Página 2 de 3\n" +
            "Conclusión.";

        var unaVez = _cleaner.Clean(entrada);
        var dosVeces = _cleaner.Clean(unaVez);

        Assert.Equal(unaVez, dosVeces);
    }

    [Fact]
    public void Documento_sin_ruido_no_se_altera_salvo_recorte_de_extremos()
    {
        const string contenido = "# Título\n\nUn párrafo normal.\n\n- Elemento uno\n- Elemento dos";

        var salida = _cleaner.Clean(contenido);

        Assert.Equal(contenido, salida);
    }

    // ---------------------- Regresiones de la revisión adversarial ----------------------

    [Fact]
    public void Marcador_al_inicio_no_empieza_con_regla_horizontal()
    {
        // Evita que "---" en la primera línea se lea como front matter YAML.
        var salida = _cleaner.Clean("Página 1\nContenido de la primera página.");

        Assert.StartsWith("**[Página 1]**", salida);
        Assert.False(salida.StartsWith("---"), "El documento no debe empezar con ---");
        Assert.Contains("**[Página 1]**", salida);
    }

    [Fact]
    public void Marcador_al_inicio_tras_lineas_en_blanco_tampoco_empieza_con_regla()
    {
        var salida = _cleaner.Clean("\n\nPágina 1\nContenido.");

        Assert.StartsWith("**[Página 1]**", salida);
    }

    [Fact]
    public void Marcador_interior_si_conserva_la_regla_superior()
    {
        var salida = _cleaner.Clean("Contenido previo.\nPágina 2\nContenido siguiente.");

        Assert.Contains("previo.\n\n---\n\n**[Página 2]**", salida);
    }

    [Theory]
    [InlineData("P 5")]        // "p" suelta: código, no paginación
    [InlineData("Pago 5")]     // palabra que empieza por "pag" pero es contenido
    [InlineData("Plan 5")]
    public void No_convierte_p_suelta_ni_palabras_que_empiezan_por_pag(string linea)
    {
        var salida = _cleaner.Clean($"Texto anterior.\n{linea}\nTexto posterior.");

        Assert.Contains(linea, salida);
        Assert.DoesNotContain("**[Página", salida);
    }

    [Fact]
    public void Trim_conserva_la_sangria_de_la_primera_linea()
    {
        // Un bloque de código indentado al inicio no debe perder su sangría.
        var salida = _cleaner.Clean("    var x = 1;\n\n\n\nSiguiente párrafo.");

        Assert.StartsWith("    var x = 1;", salida);
    }

    [Fact]
    public void Opciones_permiten_desactivar_pasos()
    {
        var cleaner = new DocumentToMarkdownCleaner(new DocumentToMarkdownCleaner.CleanerOptions
        {
            NormalizePagination = false,
        });

        var salida = cleaner.Clean("Texto\nPágina 5\nTexto");

        Assert.Contains("Página 5", salida);
        Assert.DoesNotContain("**[Página 5]**", salida);
    }
}
