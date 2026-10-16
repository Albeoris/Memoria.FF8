using System.Windows;

namespace Memoria.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application application = new();
        application.Run(new LauncherWindow());
    }
}
