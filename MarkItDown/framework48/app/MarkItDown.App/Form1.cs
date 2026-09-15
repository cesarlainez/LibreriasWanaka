using System.Diagnostics;
using System.Text;
using MarkItDown;
using MarkItDown.Core;

namespace MarkItDown.App;

public partial class Form1 : Form
{
    private readonly MarkItDownConverter _converter = new();

    public Form1()
    {
        InitializeComponent();
    }

    private void btnExaminarOrigen_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Seleccione el archivo a convertir",
            Filter = "Documentos soportados (*.docx;*.pdf)|*.docx;*.pdf|" +
                     "Word (*.docx)|*.docx|PDF (*.pdf)|*.pdf|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        txtOrigen.Text = dialog.FileName;

        // Propone un destino por defecto: mismo nombre y carpeta, con extensión .md.
        if (string.IsNullOrWhiteSpace(txtDestino.Text))
        {
            txtDestino.Text = Path.ChangeExtension(dialog.FileName, ".md");
        }
    }

    private void btnExaminarDestino_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Guardar Markdown como",
            Filter = "Markdown (*.md)|*.md|Todos los archivos (*.*)|*.*",
            DefaultExt = "md",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (!string.IsNullOrWhiteSpace(txtOrigen.Text))
        {
            dialog.FileName = Path.GetFileNameWithoutExtension(txtOrigen.Text) + ".md";
            var origenDir = Path.GetDirectoryName(txtOrigen.Text);
            if (!string.IsNullOrEmpty(origenDir))
            {
                dialog.InitialDirectory = origenDir;
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtDestino.Text = dialog.FileName;
        }
    }

    private async void btnConvertir_Click(object? sender, EventArgs e)
    {
        var origen = txtOrigen.Text.Trim();
        var destino = txtDestino.Text.Trim();

        if (string.IsNullOrEmpty(origen) || !File.Exists(origen))
        {
            MostrarAdvertencia("Seleccione un archivo de origen válido.");
            return;
        }

        if (string.IsNullOrEmpty(destino))
        {
            MostrarAdvertencia("Indique la ruta del archivo Markdown de destino.");
            return;
        }

        var opciones = new ConversionOptions();
        if (chkExtraerImagenes.Checked)
        {
            var destinoDir = Path.GetDirectoryName(Path.GetFullPath(destino)) ?? ".";
            opciones.ExtractImages = true;
            opciones.ImageOutputDirectory = Path.Combine(destinoDir, "imagenes");
            opciones.ImageLinkPrefix = "imagenes/";
        }

        SetBusy(true);
        txtEstado.Text = "Convirtiendo...";

        try
        {
            var resultado = await Task.Run(() => _converter.ConvertToFile(origen, destino, opciones));

            var mensaje = new StringBuilder();
            mensaje.AppendLine("✔ Conversión completada.");
            mensaje.AppendLine($"Archivo generado: {Path.GetFullPath(destino)}");
            if (!string.IsNullOrEmpty(resultado.Title))
            {
                mensaje.AppendLine($"Título detectado: {resultado.Title}");
            }

            if (resultado.Warnings.Count > 0)
            {
                mensaje.AppendLine();
                mensaje.AppendLine("Advertencias:");
                foreach (var advertencia in resultado.Warnings)
                {
                    mensaje.AppendLine($"  • {advertencia}");
                }
            }

            txtEstado.Text = mensaje.ToString();

            if (chkAbrirAlTerminar.Checked)
            {
                AbrirArchivo(destino);
            }
        }
        catch (FileNotFoundException ex)
        {
            MostrarError(ex.Message);
        }
        catch (UnsupportedFileFormatException ex)
        {
            MostrarError(ex.Message);
        }
        catch (MarkdownConversionException ex)
        {
            MostrarError(ex.Message);
        }
        catch (Exception ex)
        {
            MostrarError("Error inesperado: " + ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        btnConvertir.Enabled = !busy;
        btnExaminarOrigen.Enabled = !busy;
        btnExaminarDestino.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void MostrarAdvertencia(string mensaje)
    {
        txtEstado.Text = mensaje;
        MessageBox.Show(this, mensaje, "MarkItDown", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void MostrarError(string mensaje)
    {
        txtEstado.Text = "✖ " + mensaje;
        MessageBox.Show(this, mensaje, "No se pudo convertir", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void AbrirArchivo(string ruta)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            txtEstado.AppendText(Environment.NewLine + "No se pudo abrir el archivo automáticamente: " + ex.Message);
        }
    }
}
