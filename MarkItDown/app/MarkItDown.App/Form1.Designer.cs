namespace MarkItDown.App;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private Label lblOrigen;
    private TextBox txtOrigen;
    private Button btnExaminarOrigen;
    private Label lblDestino;
    private TextBox txtDestino;
    private Button btnExaminarDestino;
    private CheckBox chkExtraerImagenes;
    private CheckBox chkAbrirAlTerminar;
    private Button btnConvertir;
    private TextBox txtEstado;
    private Label lblEstado;

    private void InitializeComponent()
    {
        lblOrigen = new Label();
        txtOrigen = new TextBox();
        btnExaminarOrigen = new Button();
        lblDestino = new Label();
        txtDestino = new TextBox();
        btnExaminarDestino = new Button();
        chkExtraerImagenes = new CheckBox();
        chkAbrirAlTerminar = new CheckBox();
        btnConvertir = new Button();
        lblEstado = new Label();
        txtEstado = new TextBox();
        SuspendLayout();

        // lblOrigen
        lblOrigen.AutoSize = true;
        lblOrigen.Location = new Point(18, 20);
        lblOrigen.Text = "Archivo a convertir (.docx o .pdf):";

        // txtOrigen
        txtOrigen.Location = new Point(20, 44);
        txtOrigen.ReadOnly = true;
        txtOrigen.Size = new Size(460, 27);

        // btnExaminarOrigen
        btnExaminarOrigen.Location = new Point(488, 43);
        btnExaminarOrigen.Size = new Size(112, 29);
        btnExaminarOrigen.Text = "Examinar...";
        btnExaminarOrigen.UseVisualStyleBackColor = true;
        btnExaminarOrigen.Click += btnExaminarOrigen_Click;

        // lblDestino
        lblDestino.AutoSize = true;
        lblDestino.Location = new Point(18, 84);
        lblDestino.Text = "Guardar Markdown en:";

        // txtDestino
        txtDestino.Location = new Point(20, 108);
        txtDestino.Size = new Size(460, 27);

        // btnExaminarDestino
        btnExaminarDestino.Location = new Point(488, 107);
        btnExaminarDestino.Size = new Size(112, 29);
        btnExaminarDestino.Text = "Examinar...";
        btnExaminarDestino.UseVisualStyleBackColor = true;
        btnExaminarDestino.Click += btnExaminarDestino_Click;

        // chkExtraerImagenes
        chkExtraerImagenes.AutoSize = true;
        chkExtraerImagenes.Location = new Point(20, 148);
        chkExtraerImagenes.Text = "Extraer imágenes a una subcarpeta \"imagenes\"";
        chkExtraerImagenes.UseVisualStyleBackColor = true;

        // chkAbrirAlTerminar
        chkAbrirAlTerminar.AutoSize = true;
        chkAbrirAlTerminar.Checked = true;
        chkAbrirAlTerminar.CheckState = CheckState.Checked;
        chkAbrirAlTerminar.Location = new Point(20, 176);
        chkAbrirAlTerminar.Text = "Abrir el archivo Markdown al terminar";
        chkAbrirAlTerminar.UseVisualStyleBackColor = true;

        // btnConvertir
        btnConvertir.Location = new Point(20, 212);
        btnConvertir.Size = new Size(580, 40);
        btnConvertir.Text = "Convertir a Markdown";
        btnConvertir.UseVisualStyleBackColor = true;
        btnConvertir.Click += btnConvertir_Click;

        // lblEstado
        lblEstado.AutoSize = true;
        lblEstado.Location = new Point(18, 264);
        lblEstado.Text = "Estado:";

        // txtEstado
        txtEstado.Location = new Point(20, 288);
        txtEstado.Multiline = true;
        txtEstado.ReadOnly = true;
        txtEstado.ScrollBars = ScrollBars.Vertical;
        txtEstado.Size = new Size(580, 140);
        txtEstado.BackColor = SystemColors.Window;

        // Form1
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(620, 450);
        Controls.Add(lblOrigen);
        Controls.Add(txtOrigen);
        Controls.Add(btnExaminarOrigen);
        Controls.Add(lblDestino);
        Controls.Add(txtDestino);
        Controls.Add(btnExaminarDestino);
        Controls.Add(chkExtraerImagenes);
        Controls.Add(chkAbrirAlTerminar);
        Controls.Add(btnConvertir);
        Controls.Add(lblEstado);
        Controls.Add(txtEstado);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MarkItDown — Convertir a Markdown";
        ResumeLayout(false);
        PerformLayout();
    }
}
