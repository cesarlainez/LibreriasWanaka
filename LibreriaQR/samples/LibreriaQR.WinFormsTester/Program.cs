namespace LibreriaQR.WinFormsTester;

internal static class Program
{
    /// <summary>Punto de entrada de la aplicación.</summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
