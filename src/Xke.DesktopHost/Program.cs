namespace Xke.DesktopHost;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Handle installer hooks before WPF, data access, or the single-instance mutex.
        // Our own settings control whether a pending update is applied at a normal launch.
        Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
