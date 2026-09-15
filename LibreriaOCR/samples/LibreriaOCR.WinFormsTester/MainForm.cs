using System.Diagnostics;
using System.Text;
using LibreriaOCR;

namespace LibreriaOCR.WinFormsTester;

/// <summary>
/// Ventana de prueba: permite escoger un documento (PDF, PNG, JPG o TIFF) y muestra el texto
/// extraído por <see cref="OcrService"/>, junto con origen, número de páginas, confianza y tiempo.
/// </summary>
public sealed class MainForm : Form
{
    private readonly Button _btnSelect;
    private readonly Label _lblInfo;
    private readonly RichTextBox _txtResult;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _status;
    private readonly ToolStripProgressBar _progress;

    private OcrService? _ocr;

    public MainForm()
    {
        Text = "Prueba de OCR — LibreriaOCR";
        Width = 980;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 480);
        Font = new Font("Segoe UI", 9F);

        // ----- Barra superior -----
        var top = new Panel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(12) };

        _btnSelect = new Button
        {
            Text = "📂  Seleccionar documento y procesar…",
            Location = new Point(12, 12),
            Size = new Size(340, 42),
            FlatStyle = FlatStyle.System
        };
        _btnSelect.Click += OnSelectClick;

        _lblInfo = new Label
        {
            Text = "Selecciona un documento (PDF, PNG, JPG o TIFF) para extraer su texto.",
            Location = new Point(14, 64),
            Size = new Size(940, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Color.FromArgb(70, 70, 70)
        };

        top.Controls.Add(_btnSelect);
        top.Controls.Add(_lblInfo);

        // ----- Área de resultado -----
        _txtResult = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            DetectUrls = false,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Consolas", 10.5F),
            Text = string.Empty
        };
        var resultPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
        resultPanel.Controls.Add(_txtResult);

        // ----- Barra de estado -----
        _status = new ToolStripStatusLabel("Listo") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _progress = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false };
        _statusStrip = new StatusStrip();
        _statusStrip.Items.Add(_status);
        _statusStrip.Items.Add(_progress);

        Controls.Add(resultPanel);
        Controls.Add(top);
        Controls.Add(_statusStrip);

        FormClosed += (_, _) => _ocr?.Dispose();
    }

    private async void OnSelectClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Seleccionar documento",
            CheckFileExists = true,
            Filter =
                "Documentos (PDF e imágenes)|*.pdf;*.png;*.jpg;*.jpeg;*.jpe;*.tif;*.tiff|" +
                "PDF|*.pdf|" +
                "Imágenes|*.png;*.jpg;*.jpeg;*.tif;*.tiff|" +
                "Todos los archivos|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        await ProcessAsync(dialog.FileName);
    }

    private async Task ProcessAsync(string path)
    {
        SetBusy(true, $"Procesando: {Path.GetFileName(path)}…");
        _txtResult.Clear();
        _lblInfo.Text = $"Archivo: {Path.GetFileName(path)}";

        try
        {
            var ocr = EnsureOcr();
            var result = await Task.Run(() => ocr.Recognize(path));

            _txtResult.Text = BuildDisplayText(result);
            _txtResult.SelectionStart = 0;
            _txtResult.SelectionLength = 0;
            _txtResult.ScrollToCaret();

            var conf = result.MeanConfidence < 0 ? "n/a (texto digital)" : $"{result.MeanConfidence:0.#}%";
            _lblInfo.Text =
                $"Archivo: {result.FileName}     •     Origen: {DescribeSource(result.SourceKind)}     •     " +
                $"Páginas: {result.PageCount}     •     Confianza: {conf}     •     Tiempo: {result.Duration.TotalSeconds:0.00}s";
            _status.Text = $"Listo — {result.Text.Length:N0} caracteres extraídos";
        }
        catch (FormatoNoSoportadoException ex)
        {
            ShowError("Formato no soportado", ex.Message);
        }
        catch (OcrException ex)
        {
            var detail = ex.InnerException is null ? string.Empty : $"\n\nDetalle: {ex.InnerException.Message}";
            ShowError("Error de OCR", ex.Message + detail);
        }
        catch (Exception ex)
        {
            ShowError("Error inesperado", $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            SetBusy(false, _status.Text);
        }
    }

    private OcrService EnsureOcr() => _ocr ??= new OcrService(new OcrOptions { Languages = "spa" });

    private static string BuildDisplayText(OcrResult result)
    {
        if (result.Pages.Count <= 1)
            return result.Text.Trim();

        var sb = new StringBuilder();
        foreach (var page in result.Pages)
        {
            var conf = page.FromEmbeddedText ? "texto digital" : $"{page.Confidence:0.#}%";
            sb.AppendLine($"════════ Página {page.PageNumber}  ({conf}) ════════");
            sb.AppendLine(page.Text.Trim());
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string DescribeSource(OcrSourceKind kind) => kind switch
    {
        OcrSourceKind.Image => "Imagen (OCR)",
        OcrSourceKind.PdfText => "PDF con texto digital",
        OcrSourceKind.PdfOcr => "PDF escaneado (OCR)",
        OcrSourceKind.Mixed => "PDF mixto (texto digital + OCR)",
        _ => kind.ToString()
    };

    private void SetBusy(bool busy, string status)
    {
        _btnSelect.Enabled = !busy;
        _progress.Visible = busy;
        _status.Text = status;
        UseWaitCursor = busy;
    }

    private void ShowError(string title, string message)
    {
        _status.Text = title;
        _txtResult.Text = $"[{title}]\r\n\r\n{message}";
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
