namespace MarkItDown.App;

static class Program
{
    /// <summary>Punto de entrada de la aplicación (.NET Framework 4.8).</summary>
    [STAThread]
    static void Main()
    {
        // En .NET Framework no existe ApplicationConfiguration.Initialize();
        // se configura de la forma clásica.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
    }
}
