using System.Globalization;
using LibreriaQR;
using LibreriaQR.Contenidos;

namespace LibreriaQR.WinFormsTester;

/// <summary>
/// Ventana de prueba de <see cref="GeneradorQR"/>: permite elegir cualquiera de los 13 tipos de
/// QR, rellenar sus campos, ajustar apariencia (colores, tamaño, corrección) y logo, y ver la
/// vista previa. Desde aquí se puede guardar el PNG o copiar el Base64 / data URI.
/// </summary>
public sealed class MainForm : Form
{
    private readonly GeneradorQR _gen = new();

    // Columna izquierda
    private readonly ComboBox _cboTipo = new();
    private readonly FlowLayoutPanel _panelCampos = new();
    private readonly Dictionary<string, Control> _campos = new();

    // Apariencia
    private readonly TextBox _txtFg = new();
    private readonly TextBox _txtBg = new();
    private readonly CheckBox _chkTransparente = new();
    private readonly NumericUpDown _numTamano = new();
    private readonly ComboBox _cboCorreccion = new();
    private readonly CheckBox _chkZonaQuieta = new();

    // Logo
    private readonly CheckBox _chkLogo = new();
    private readonly TextBox _txtLogoRuta = new();
    private readonly Button _btnLogo = new();
    private readonly NumericUpDown _numProporcion = new();
    private readonly CheckBox _chkLogoFondo = new();
    private readonly NumericUpDown _numRadio = new();

    // Acciones
    private readonly Button _btnGenerar = new();
    private readonly Button _btnGuardar = new();
    private readonly Button _btnCopiarBase64 = new();
    private readonly Button _btnCopiarDataUri = new();

    // Columna derecha
    private readonly PictureBox _pic = new();
    private readonly TextBox _txtInfo = new();

    // Estado
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _status = new("Listo") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private ResultadoQR? _ultimo;

    // Ancho común de las filas de parámetros (controles a lo ancho del panel izquierdo).
    private const int FilaAncho = 470;

    private static readonly (TipoQR tipo, string etiqueta)[] Tipos =
    {
        (TipoQR.EnlaceWeb,         "1 · Enlace web (URL)"),
        (TipoQR.TextoPlano,        "2 · Texto plano"),
        (TipoQR.LlamadaTelefonica, "3 · Llamada telefónica"),
        (TipoQR.MensajeSms,        "4 · Mensaje SMS"),
        (TipoQR.RedWifi,           "5 · Red Wi-Fi"),
        (TipoQR.TarjetaVCard,      "6 · Contacto completo (vCard)"),
        (TipoQR.TarjetaMeCard,     "7 · Contacto simple (MECARD)"),
        (TipoQR.CorreoElectronico, "8 · Correo electrónico"),
        (TipoQR.UbicacionGps,      "9 · Ubicación GPS"),
        (TipoQR.EventoCalendario,  "10 · Evento de calendario"),
        (TipoQR.Autenticacion2Fa,  "11 · Autenticación 2FA"),
        (TipoQR.MensajeriaApp,     "12 · Mensajería (WhatsApp/Telegram)"),
        (TipoQR.Criptomoneda,      "13 · Criptomoneda"),
    };

    public MainForm()
    {
        Text = "Generador de QR — LibreriaQR";
        Width = 1180;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 640);
        Font = new Font("Segoe UI", 9F);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6,
            Panel1MinSize = 360,
        };

        ConstruirIzquierda(split.Panel1);
        ConstruirDerecha(split.Panel2);

        _statusStrip.Items.Add(_status);

        Controls.Add(split);
        Controls.Add(_statusStrip);

        // El ancho del panel de parámetros se fija cuando el SplitContainer ya tiene su tamaño
        // real; hacerlo en el inicializador lo recorta (todavía mide el tamaño por defecto).
        Load += (_, _) => split.SplitterDistance = 540;

        // Selección inicial.
        _cboTipo.SelectedIndexChanged += (_, _) => ReconstruirCampos(TipoActual());
        _cboTipo.SelectedIndex = 0; // dispara ReconstruirCampos
    }

    // ------------------------------------------------------------------ UI izquierda

    private void ConstruirIzquierda(Control panel)
    {
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12, 12, 12, 12),
        };

        // Tipo
        var lblTipo = new Label { Text = "Tipo de QR", AutoSize = true, Margin = new Padding(2, 0, 0, 2) };
        _cboTipo.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboTipo.Width = FilaAncho;
        foreach (var (_, etiqueta) in Tipos)
            _cboTipo.Items.Add(etiqueta);

        // Campos dinámicos
        _panelCampos.FlowDirection = FlowDirection.TopDown;
        _panelCampos.WrapContents = false;
        _panelCampos.AutoSize = true;
        _panelCampos.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _panelCampos.Width = FilaAncho + 20;
        _panelCampos.Margin = new Padding(0, 8, 0, 4);

        left.Controls.Add(lblTipo);
        left.Controls.Add(_cboTipo);
        left.Controls.Add(_panelCampos);
        left.Controls.Add(ConstruirGrupoApariencia());
        left.Controls.Add(ConstruirGrupoLogo());
        left.Controls.Add(ConstruirBotones());

        panel.Controls.Add(left);
    }

    private GroupBox ConstruirGrupoApariencia()
    {
        var g = new GroupBox { Text = "Apariencia", Width = FilaAncho, Height = 180, Margin = new Padding(0, 8, 0, 0) };

        var lblFg = new Label { Text = "Color", Location = new Point(12, 26), AutoSize = true };
        _txtFg.Text = "#000000";
        _txtFg.Location = new Point(70, 22);
        _txtFg.Width = 100;
        var btnFg = new Button { Text = "…", Location = new Point(174, 21), Width = 32 };
        btnFg.Click += (_, _) => ElegirColor(_txtFg);

        var lblBg = new Label { Text = "Fondo", Location = new Point(250, 26), AutoSize = true };
        _txtBg.Text = "#FFFFFF";
        _txtBg.Location = new Point(300, 22);
        _txtBg.Width = 100;
        var btnBg = new Button { Text = "…", Location = new Point(404, 21), Width = 32 };
        btnBg.Click += (_, _) => ElegirColor(_txtBg);

        _chkTransparente.Text = "Fondo transparente";
        _chkTransparente.Location = new Point(12, 54);
        _chkTransparente.AutoSize = true;

        var lblTam = new Label { Text = "Tamaño (px)", Location = new Point(12, 90), AutoSize = true };
        _numTamano.Minimum = 64;
        _numTamano.Maximum = 2000;
        _numTamano.Increment = 32;
        _numTamano.Value = 512;
        _numTamano.Location = new Point(96, 86);
        _numTamano.Width = 90;

        var lblEcc = new Label { Text = "Corrección", Location = new Point(210, 90), AutoSize = true };
        _cboCorreccion.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboCorreccion.Items.AddRange(new object[] { "Automática", "Baja (L)", "Media (M)", "Alta (Q)", "Máxima (H)" });
        _cboCorreccion.SelectedIndex = 0;
        _cboCorreccion.Location = new Point(284, 86);
        _cboCorreccion.Width = 152;

        _chkZonaQuieta.Text = "Incluir zona quieta (margen)";
        _chkZonaQuieta.Location = new Point(12, 122);
        _chkZonaQuieta.AutoSize = true;
        _chkZonaQuieta.Checked = true;

        g.Controls.AddRange(new Control[]
        {
            lblFg, _txtFg, btnFg, lblBg, _txtBg, btnBg,
            _chkTransparente, lblTam, _numTamano, lblEcc, _cboCorreccion, _chkZonaQuieta,
        });
        return g;
    }

    private GroupBox ConstruirGrupoLogo()
    {
        var g = new GroupBox { Text = "Logo (opcional)", Width = FilaAncho, Height = 150, Margin = new Padding(0, 8, 0, 0) };

        _chkLogo.Text = "Incrustar logo en el centro";
        _chkLogo.Location = new Point(12, 24);
        _chkLogo.AutoSize = true;
        _chkLogo.CheckedChanged += (_, _) => ActualizarHabilitacionLogo();

        _txtLogoRuta.Location = new Point(12, 50);
        _txtLogoRuta.Width = 360;
        _txtLogoRuta.ReadOnly = true;
        _btnLogo.Text = "Examinar…";
        _btnLogo.Location = new Point(378, 49);
        _btnLogo.Width = 80;
        _btnLogo.Click += (_, _) => ElegirLogo();

        var lblProp = new Label { Text = "Tamaño (%)", Location = new Point(12, 88), AutoSize = true };
        _numProporcion.Minimum = 5;
        _numProporcion.Maximum = 35;
        _numProporcion.Value = 22;
        _numProporcion.Location = new Point(90, 84);
        _numProporcion.Width = 60;

        var lblRadio = new Label { Text = "Redondeo (%)", Location = new Point(180, 88), AutoSize = true };
        _numRadio.Minimum = 0;
        _numRadio.Maximum = 50;
        _numRadio.Value = 18;
        _numRadio.Location = new Point(272, 84);
        _numRadio.Width = 60;

        _chkLogoFondo.Text = "Recuadro de fondo detrás del logo";
        _chkLogoFondo.Location = new Point(12, 116);
        _chkLogoFondo.AutoSize = true;
        _chkLogoFondo.Checked = true;

        g.Controls.AddRange(new Control[]
        {
            _chkLogo, _txtLogoRuta, _btnLogo, lblProp, _numProporcion, lblRadio, _numRadio, _chkLogoFondo,
        });

        ActualizarHabilitacionLogo();
        return g;
    }

    private FlowLayoutPanel ConstruirBotones()
    {
        var p = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            Width = FilaAncho,
            Margin = new Padding(0, 10, 0, 0),
        };

        _btnGenerar.Text = "🔳  Generar QR";
        _btnGenerar.Width = 180;
        _btnGenerar.Height = 40;
        _btnGenerar.FlatStyle = FlatStyle.System;
        _btnGenerar.Font = new Font(Font, FontStyle.Bold);
        _btnGenerar.Click += (_, _) => Generar();

        _btnGuardar.Text = "💾  Guardar PNG…";
        _btnGuardar.Width = 200;
        _btnGuardar.Height = 40;
        _btnGuardar.Enabled = false;
        _btnGuardar.Click += (_, _) => GuardarPng();

        _btnCopiarBase64.Text = "Copiar Base64";
        _btnCopiarBase64.Width = 185;
        _btnCopiarBase64.Height = 30;
        _btnCopiarBase64.Enabled = false;
        _btnCopiarBase64.Click += (_, _) => CopiarAlPortapapeles(_ultimo?.Base64, "Base64");

        _btnCopiarDataUri.Text = "Copiar Data URI";
        _btnCopiarDataUri.Width = 185;
        _btnCopiarDataUri.Height = 30;
        _btnCopiarDataUri.Enabled = false;
        _btnCopiarDataUri.Click += (_, _) => CopiarAlPortapapeles(_ultimo?.DataUri, "Data URI");

        p.Controls.AddRange(new Control[] { _btnGenerar, _btnGuardar, _btnCopiarBase64, _btnCopiarDataUri });
        return p;
    }

    // ------------------------------------------------------------------ UI derecha

    private void ConstruirDerecha(Control panel)
    {
        _pic.Dock = DockStyle.Fill;
        _pic.SizeMode = PictureBoxSizeMode.Zoom;
        _pic.BackColor = Color.White;
        _pic.BorderStyle = BorderStyle.FixedSingle;

        var contenedorPic = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 6) };
        contenedorPic.Controls.Add(_pic);

        _txtInfo.Dock = DockStyle.Fill;
        _txtInfo.Multiline = true;
        _txtInfo.ReadOnly = true;
        _txtInfo.ScrollBars = ScrollBars.Vertical;
        _txtInfo.BackColor = Color.White;
        _txtInfo.Font = new Font("Consolas", 9.5F);
        var contenedorInfo = new Panel { Dock = DockStyle.Bottom, Height = 150, Padding = new Padding(12, 0, 12, 12) };
        contenedorInfo.Controls.Add(_txtInfo);

        panel.Controls.Add(contenedorPic);
        panel.Controls.Add(contenedorInfo);
    }

    // ------------------------------------------------------------------ Campos por tipo

    private TipoQR TipoActual() => Tipos[Math.Max(0, _cboTipo.SelectedIndex)].tipo;

    private void ReconstruirCampos(TipoQR tipo)
    {
        _panelCampos.SuspendLayout();
        foreach (var c in _campos.Values)
            c.Dispose();
        _panelCampos.Controls.Clear();
        _campos.Clear();

        switch (tipo)
        {
            case TipoQR.EnlaceWeb:
                AddTexto("url", "URL", "https://acsa.com.sv");
                break;

            case TipoQR.TextoPlano:
                AddTexto("texto", "Texto", "Hola desde ACSA", multi: true);
                break;

            case TipoQR.LlamadaTelefonica:
                AddTexto("numero", "Número (formato internacional)", "+50322500000");
                break;

            case TipoQR.MensajeSms:
                AddTexto("numero", "Número", "+50322500000");
                AddTexto("mensaje", "Mensaje", "", multi: true);
                break;

            case TipoQR.RedWifi:
                AddTexto("ssid", "Nombre de red (SSID)", "ACSA-Invitados");
                AddTexto("password", "Contraseña", "Clave1234");
                AddCombo("cifrado", "Cifrado", new[] { "WPA / WPA2 / WPA3", "WEP", "Sin cifrado" });
                AddCheck("oculta", "Red oculta");
                break;

            case TipoQR.TarjetaVCard:
                AddTexto("nombre", "Nombre", "Cesar");
                AddTexto("apellido", "Apellido", "Lainez");
                AddTexto("empresa", "Empresa", "ACSA");
                AddTexto("cargo", "Cargo", "TI");
                AddTexto("telefono", "Teléfono");
                AddTexto("movil", "Móvil", "+503 7000-0000");
                AddTexto("teltrabajo", "Teléfono de trabajo");
                AddTexto("correo", "Correo", "soporte@acsa.com.sv");
                AddTexto("web", "Sitio web", "https://acsa.com.sv");
                AddTexto("direccion", "Dirección");
                AddTexto("ciudad", "Ciudad");
                AddTexto("region", "Departamento / Región");
                AddTexto("cp", "Código postal");
                AddTexto("pais", "País", "El Salvador");
                AddTexto("nota", "Nota", "", multi: true);
                AddCombo("version", "Versión vCard", new[] { "2.1", "3.0", "4.0" }, 1);
                break;

            case TipoQR.TarjetaMeCard:
                AddTexto("nombre", "Nombre", "Cesar");
                AddTexto("apellido", "Apellido", "Lainez");
                AddTexto("telefono", "Teléfono", "+50322500000");
                AddTexto("correo", "Correo", "soporte@acsa.com.sv");
                AddTexto("direccion", "Dirección");
                AddTexto("web", "Sitio web");
                AddTexto("nota", "Nota", "", multi: true);
                break;

            case TipoQR.CorreoElectronico:
                AddTexto("destinatario", "Destinatario", "soporte@acsa.com.sv");
                AddTexto("asunto", "Asunto", "Soporte");
                AddTexto("cuerpo", "Cuerpo", "", multi: true);
                break;

            case TipoQR.UbicacionGps:
                AddTexto("lat", "Latitud", "13.6989");
                AddTexto("lng", "Longitud", "-89.1914");
                break;

            case TipoQR.EventoCalendario:
                AddTexto("titulo", "Título", "Reunión de TI");
                AddTexto("descripcion", "Descripción", "", multi: true);
                AddTexto("ubicacion", "Lugar", "ACSA");
                AddFecha("inicio", "Inicio", DateTime.Today.AddDays(1).AddHours(9));
                AddFecha("fin", "Fin", DateTime.Today.AddDays(1).AddHours(10));
                AddCheck("todoeldia", "Todo el día");
                break;

            case TipoQR.Autenticacion2Fa:
                AddTexto("secreto", "Secreto (Base32)", "JBSWY3DPEHPK3PXP");
                AddTexto("emisor", "Emisor", "ACSA");
                AddTexto("cuenta", "Cuenta", "soporte@acsa.com.sv");
                AddCombo("tipo", "Tipo", new[] { "TOTP (por tiempo)", "HOTP (por contador)" });
                AddCombo("algoritmo", "Algoritmo", new[] { "SHA1", "SHA256", "SHA512" });
                AddNumero("digitos", "Dígitos", 6, 8, 6);
                AddNumero("periodo", "Periodo (seg, solo TOTP)", 10, 300, 30);
                AddNumero("contador", "Contador inicial (solo HOTP)", 0, 1_000_000, 0);
                break;

            case TipoQR.MensajeriaApp:
                AddCombo("app", "App", new[] { "WhatsApp", "Telegram" });
                AddTexto("destino", "Número (WhatsApp) o usuario (Telegram)", "+503 2250-0000");
                AddTexto("mensaje", "Mensaje (solo WhatsApp)", "Hola", multi: true);
                break;

            case TipoQR.Criptomoneda:
                AddCombo("moneda", "Moneda", new[] { "Bitcoin", "Bitcoin Cash", "Litecoin", "Ethereum" });
                AddTexto("direccion", "Dirección", "1BvBMSEYstWetqTFn5Au4m4GFg7xJaNVN2");
                AddTexto("monto", "Monto (opcional)", "0.005");
                AddTexto("etiqueta", "Etiqueta (no aplica a Ethereum)");
                AddTexto("mensaje", "Mensaje (no aplica a Ethereum)");
                break;
        }

        _panelCampos.ResumeLayout();
    }

    private TextBox AddTexto(string clave, string etiqueta, string valor = "", bool multi = false)
    {
        var alto = multi ? 56 : 24;
        var cont = NuevaFila(etiqueta, alto, out var y);
        var txt = new TextBox
        {
            Location = new Point(0, y),
            Width = FilaAncho,
            Text = valor,
            Multiline = multi,
            ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None,
            Height = alto,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
        };
        cont.Controls.Add(txt);
        Registrar(clave, txt, cont);
        return txt;
    }

    private ComboBox AddCombo(string clave, string etiqueta, string[] opciones, int sel = 0)
    {
        var cont = NuevaFila(etiqueta, 24, out var y);
        var cbo = new ComboBox
        {
            Location = new Point(0, y),
            Width = FilaAncho,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        cbo.Items.AddRange(opciones);
        cbo.SelectedIndex = Math.Min(sel, opciones.Length - 1);
        cont.Controls.Add(cbo);
        Registrar(clave, cbo, cont);
        return cbo;
    }

    private CheckBox AddCheck(string clave, string etiqueta, bool valor = false)
    {
        var cont = new Panel { Width = FilaAncho + 4, Height = 26, Margin = new Padding(0, 0, 0, 4) };
        var chk = new CheckBox { Text = etiqueta, Location = new Point(0, 2), AutoSize = true, Checked = valor };
        cont.Controls.Add(chk);
        _panelCampos.Controls.Add(cont);
        _campos[clave] = chk;
        return chk;
    }

    private DateTimePicker AddFecha(string clave, string etiqueta, DateTime valor)
    {
        var cont = NuevaFila(etiqueta, 24, out var y);
        var dtp = new DateTimePicker
        {
            Location = new Point(0, y),
            Width = FilaAncho,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd/MM/yyyy  HH:mm",
            Value = valor,
        };
        cont.Controls.Add(dtp);
        Registrar(clave, dtp, cont);
        return dtp;
    }

    private NumericUpDown AddNumero(string clave, string etiqueta, int min, int max, int valor)
    {
        var cont = NuevaFila(etiqueta, 24, out var y);
        var num = new NumericUpDown
        {
            Location = new Point(0, y),
            Width = 120,
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(valor, min, max),
        };
        cont.Controls.Add(num);
        Registrar(clave, num, cont);
        return num;
    }

    private Panel NuevaFila(string etiqueta, int altoControl, out int yControl)
    {
        var cont = new Panel { Width = FilaAncho + 4, Height = 18 + altoControl + 4, Margin = new Padding(0, 0, 0, 4) };
        var lbl = new Label { Text = etiqueta, Location = new Point(0, 0), AutoSize = true, ForeColor = Color.FromArgb(70, 70, 70) };
        cont.Controls.Add(lbl);
        yControl = 18;
        return cont;
    }

    private void Registrar(string clave, Control control, Panel contenedor)
    {
        _panelCampos.Controls.Add(contenedor);
        _campos[clave] = control;
    }

    // ------------------------------------------------------------------ Generación

    private void Generar()
    {
        try
        {
            var contenido = ConstruirContenido(TipoActual());
            var opciones = ConstruirOpciones();
            var r = _gen.Generar(contenido, opciones);
            _ultimo = r;

            MostrarImagen(r.Png);

            _txtInfo.Text =
                $"Tipo       : {r.Tipo}\r\n" +
                $"Tamaño     : {r.AnchoPx} × {r.AltoPx} px\r\n" +
                $"Corrección : {r.Correccion}\r\n" +
                $"Logo       : {(r.TieneLogo ? "sí" : "no")}\r\n" +
                $"PNG        : {r.Png.Length:N0} bytes\r\n" +
                $"--- Payload codificado ---\r\n{r.Payload}";

            _btnGuardar.Enabled = true;
            _btnCopiarBase64.Enabled = true;
            _btnCopiarDataUri.Enabled = true;
            _status.Text = $"QR generado — {r.AnchoPx}px, {r.Png.Length:N0} bytes";
        }
        catch (QrContenidoInvalidoException ex)
        {
            Advertir("Contenido inválido", ex.Message);
        }
        catch (QrLogoInvalidoException ex)
        {
            Advertir("Logo inválido", ex.Message);
        }
        catch (Exception ex)
        {
            Advertir("Error inesperado", $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private ContenidoQR ConstruirContenido(TipoQR tipo) => tipo switch
    {
        TipoQR.EnlaceWeb => new ContenidoUrl(T("url")),

        TipoQR.TextoPlano => new ContenidoTexto(T("texto")),

        TipoQR.LlamadaTelefonica => new ContenidoTelefono(T("numero")),

        TipoQR.MensajeSms => new ContenidoSms(T("numero"), T("mensaje")),

        TipoQR.RedWifi => new ContenidoWifi(T("ssid"), T("password"), (TipoCifradoWifi)Combo("cifrado"), Chk("oculta")),

        TipoQR.TarjetaVCard => new ContenidoVCard
        {
            Nombre = T("nombre"),
            Apellido = T("apellido"),
            Empresa = T("empresa"),
            Cargo = T("cargo"),
            Telefono = T("telefono"),
            Movil = T("movil"),
            TelefonoTrabajo = T("teltrabajo"),
            Correo = T("correo"),
            SitioWeb = T("web"),
            Direccion = T("direccion"),
            Ciudad = T("ciudad"),
            Region = T("region"),
            CodigoPostal = T("cp"),
            Pais = T("pais"),
            Nota = T("nota"),
            Version = (VersionVCard)Combo("version"),
        },

        TipoQR.TarjetaMeCard => new ContenidoMeCard
        {
            Nombre = T("nombre"),
            Apellido = T("apellido"),
            Telefono = T("telefono"),
            Correo = T("correo"),
            Direccion = T("direccion"),
            SitioWeb = T("web"),
            Nota = T("nota"),
        },

        TipoQR.CorreoElectronico => new ContenidoCorreo(T("destinatario"), T("asunto"), T("cuerpo")),

        TipoQR.UbicacionGps => new ContenidoUbicacion(ParsearDouble(T("lat"), "Latitud"), ParsearDouble(T("lng"), "Longitud")),

        TipoQR.EventoCalendario => new ContenidoEvento
        {
            Titulo = T("titulo"),
            Descripcion = T("descripcion"),
            Ubicacion = T("ubicacion"),
            Inicio = Fecha("inicio"),
            Fin = Fecha("fin"),
            TodoElDia = Chk("todoeldia"),
        },

        TipoQR.Autenticacion2Fa => new ContenidoAutenticacion2Fa
        {
            Secreto = T("secreto"),
            Emisor = T("emisor"),
            Cuenta = T("cuenta"),
            Tipo2Fa = (Tipo2Fa)Combo("tipo"),
            Algoritmo = (Algoritmo2Fa)Combo("algoritmo"),
            Digitos = Num("digitos"),
            PeriodoSegundos = Num("periodo"),
            Contador = Num("contador"),
        },

        TipoQR.MensajeriaApp => new ContenidoMensajeria((AppMensajeria)Combo("app"), T("destino"), T("mensaje")),

        TipoQR.Criptomoneda => new ContenidoCripto
        {
            Moneda = (Criptomoneda)Combo("moneda"),
            Direccion = T("direccion"),
            Monto = ParsearDecimalOpcional(T("monto"), "Monto"),
            Etiqueta = VacioANull(T("etiqueta")),
            Mensaje = VacioANull(T("mensaje")),
        },

        _ => throw new QrContenidoInvalidoException("Tipo no soportado."),
    };

    private OpcionesQR ConstruirOpciones()
    {
        var opciones = new OpcionesQR
        {
            TamanoPx = (int)_numTamano.Value,
            ColorPrimerPlano = _txtFg.Text,
            ColorFondo = _txtBg.Text,
            FondoTransparente = _chkTransparente.Checked,
            IncluirZonaQuieta = _chkZonaQuieta.Checked,
            Correccion = _cboCorreccion.SelectedIndex <= 0 ? null : (NivelCorreccion)(_cboCorreccion.SelectedIndex - 1),
        };

        if (_chkLogo.Checked)
        {
            if (string.IsNullOrWhiteSpace(_txtLogoRuta.Text))
                throw new QrLogoInvalidoException("Marcaste 'incrustar logo' pero no seleccionaste ningún archivo.");

            opciones.Logo = new OpcionesLogo
            {
                Ruta = _txtLogoRuta.Text,
                Proporcion = (double)_numProporcion.Value / 100.0,
                ConFondo = _chkLogoFondo.Checked,
                RadioEsquinas = (double)_numRadio.Value / 100.0,
            };
        }

        return opciones;
    }

    // ------------------------------------------------------------------ Acciones auxiliares

    private void GuardarPng()
    {
        if (_ultimo is null)
            return;

        using var dlg = new SaveFileDialog
        {
            Title = "Guardar QR",
            Filter = "Imagen PNG|*.png",
            FileName = $"qr-{_ultimo.Tipo.ToString().ToLowerInvariant()}.png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        _ultimo.GuardarPng(dlg.FileName);
        _status.Text = $"Guardado: {dlg.FileName}";
    }

    private void CopiarAlPortapapeles(string? valor, string nombre)
    {
        if (string.IsNullOrEmpty(valor))
            return;
        Clipboard.SetText(valor);
        _status.Text = $"{nombre} copiado al portapapeles ({valor.Length:N0} caracteres)";
    }

    private void ElegirColor(TextBox destino)
    {
        using var cd = new ColorDialog { FullOpen = true };
        if (TryParseColor(destino.Text, out var actual))
            cd.Color = actual;
        if (cd.ShowDialog(this) == DialogResult.OK)
            destino.Text = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
    }

    private void ElegirLogo()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Seleccionar logo",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        _txtLogoRuta.Text = dlg.FileName;
        if (!_chkLogo.Checked)
            _chkLogo.Checked = true;
    }

    private void ActualizarHabilitacionLogo()
    {
        var on = _chkLogo.Checked;
        _txtLogoRuta.Enabled = on;
        _btnLogo.Enabled = on;
        _numProporcion.Enabled = on;
        _numRadio.Enabled = on;
        _chkLogoFondo.Enabled = on;
    }

    private void MostrarImagen(byte[] png)
    {
        // Se copia a un Bitmap independiente para poder cerrar el stream sin invalidar la imagen.
        using var ms = new MemoryStream(png);
        using var original = Image.FromStream(ms);
        var copia = new Bitmap(original);
        _pic.Image?.Dispose();
        _pic.Image = copia;
    }

    private void Advertir(string titulo, string mensaje)
    {
        _status.Text = titulo;
        MessageBox.Show(this, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ------------------------------------------------------------------ Lectura de campos

    private string T(string clave) => _campos.TryGetValue(clave, out var c) && c is TextBox t ? t.Text.Trim() : string.Empty;

    private int Combo(string clave) => _campos.TryGetValue(clave, out var c) && c is ComboBox cb ? Math.Max(0, cb.SelectedIndex) : 0;

    private bool Chk(string clave) => _campos.TryGetValue(clave, out var c) && c is CheckBox cb && cb.Checked;

    private DateTime Fecha(string clave) => _campos.TryGetValue(clave, out var c) && c is DateTimePicker dtp ? dtp.Value : DateTime.Now;

    private int Num(string clave) => _campos.TryGetValue(clave, out var c) && c is NumericUpDown n ? (int)n.Value : 0;

    private static string? VacioANull(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    private static double ParsearDouble(string valor, string campo)
    {
        var limpio = (valor ?? string.Empty).Trim().Replace(',', '.');
        if (double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        throw new QrContenidoInvalidoException($"'{campo}' no es un número válido: '{valor}'.");
    }

    private static decimal? ParsearDecimalOpcional(string valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var limpio = valor.Trim().Replace(',', '.');
        if (decimal.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        throw new QrContenidoInvalidoException($"'{campo}' no es un número válido: '{valor}'.");
    }

    private static bool TryParseColor(string hex, out Color color)
    {
        color = Color.Black;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            color = ColorTranslator.FromHtml(hex.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }
}
