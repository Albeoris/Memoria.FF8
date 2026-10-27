using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Memoria.Launcher;

internal static class Program
{
    internal static string? StartupError { get; private set; }
    internal static UpdateResume? Resume { get; private set; }

    [STAThread]
    private static void Main(string[] arguments)
    {
        string root = MacBundleUpdate.NormalizeRoot(AppContext.BaseDirectory);
        using Mutex mutex = new(false, UpdateInstaller.MutexName(root));
        bool acquired = false;
        try
        {
            if (arguments.FirstOrDefault() is "--apply-update" or "--recover-update")
            {
                if (!LauncherCapabilities.CanUpdate)
                {
                    throw new PlatformNotSupportedException(LauncherCapabilities.UpdateStatus);
                }
                UpdateInstaller.RunHelper(arguments);
                return;
            }
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                StartupError = "Memoria's launcher is already open for this installation.";
            }
            else
            {
                if (LauncherCapabilities.CanUpdate && UpdateInstaller.NeedsRecovery(root))
                {
                    UpdateInstaller.StartHelperAsync(Path.TrimEndingDirectorySeparator(root), recovery: true).GetAwaiter().GetResult();
                    return;
                }
                if (arguments.Length > 0)
                {
                    if (arguments.Length != 1 || arguments[0] != "--resume-update" || !LauncherCapabilities.CanUpdate)
                    {
                        throw new ArgumentException("Invalid or unavailable launcher arguments.");
                    }
                    Resume = UpdateResume.Read(root);
                }
            }
        }
        catch (Exception exception)
        {
            StartupError = "Memoria could not complete startup or installation. " + exception.Message + "\nIf update recovery failed, keep the .memoria-update folder and its backups, close running game processes, and start the launcher again.";
        }
        try
        {
            AppBuilder.Configure<LauncherApplication>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime([]);
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}

internal sealed class LauncherApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.StartupError is string error ? LauncherDialogs.Create("Memoria", error, false) : new LauncherWindow(Program.Resume);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
