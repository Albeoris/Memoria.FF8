using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;
using Memoria.Launcher.Controller;

namespace Memoria.Launcher;

internal static class Program
{
    private const string CrashReportArgument = "--report-launcher-crash";
    private static int reportingCrash;

    internal static string? StartupError { get; private set; }
    internal static UpdateResume? Resume { get; private set; }
    internal static ErrorIncident? StartupIncident { get; private set; }
    internal static string? MigrationWarning { get; private set; }

    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.FirstOrDefault() == CrashReportArgument)
        {
            try
            {
                return ShowCrashReport(arguments);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Could not open the launcher crash report: " + exception);
                return 1;
            }
        }

        // Native window callbacks and worker threads can terminate the process without unwinding Main.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.IsTerminating && args.ExceptionObject is Exception exception)
            {
                ReportCrash(exception);
            }
        };
        try
        {
            RunLauncher(arguments);
            return 0;
        }
        catch (Exception exception)
        {
            ReportCrash(exception);
            return 1;
        }
    }

    private static void ReportCrash(Exception exception)
    {
        if (Interlocked.Exchange(ref reportingCrash, 1) != 0)
        {
            return;
        }
        try
        {
            ErrorIncident incident = ErrorIncident.Capture(exception, "Launcher crash", saveUnavailableReason: "The launcher process failed; no game snapshot was captured.");
            incident.Persist();

            // Run the report dialog in a fresh process with its own Avalonia dispatcher.
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the launcher executable.");
            ProcessStartInfo start = new(executable) { UseShellExecute = false };
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, typeof(Program).Assembly.GetName().Name + ".dll"));
            }
            start.ArgumentList.Add(CrashReportArgument);
            start.ArgumentList.Add(incident.Id);
            using Process? reporter = Process.Start(start);
        }
        catch (Exception reportFailure)
        {
            Console.Error.WriteLine("Could not open or preserve the launcher crash report: " + reportFailure);
            Console.Error.WriteLine(exception);
        }
    }

    private static int ShowCrashReport(string[] arguments)
    {
        if (arguments.Length != 2 || !Guid.TryParseExact(arguments[1], "N", out Guid id))
        {
            return 1;
        }
        StartupIncident = ErrorIncident.DiscoverAll().FirstOrDefault(incident => incident.Id == id.ToString("N"));
        if (StartupIncident is null)
        {
            return 1;
        }
        StartupError = "The Memoria launcher closed unexpectedly. You can review the error and prepare a report for GitHub.";
        RunDesktop();
        return 0;
    }

    private static void RunLauncher(string[] arguments)
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
                try
                {
                    UpdateInstaller.RetireTransientFiles(root);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    Console.Error.WriteLine("Compatibility payload cleanup will retry on the next start. " + exception.Message);
                }
                try
                {
                    GirlNextDoorMigration.FromLauncher(root);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
                {
                    MigrationWarning = "Girl Next Door migration will retry on the next start. " + exception.Message;
                    Console.Error.WriteLine(MigrationWarning);
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
            StartupIncident = ErrorIncident.Capture(exception, "Launcher startup", saveUnavailableReason: "The launcher failed before starting the game.");
            try
            {
                StartupIncident.Persist();
            }
            catch (Exception captureFailure) when (captureFailure is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Could not preserve launcher incident: " + captureFailure.Message);
            }
            StartupError = "Memoria could not complete startup or installation. " + exception.Message + "\nIf update recovery failed, keep the .memoria-update folder and its backups, close running game processes, and start the launcher again.";
        }
        try
        {
            RunDesktop();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static void RunDesktop()
    {
        AppBuilder builder = AppBuilder.Configure<LauncherApplication>().UsePlatformDetect();
        if (OperatingSystem.IsLinux())
        {
            builder.With(new X11PlatformOptions { WmClass = Environment.GetEnvironmentVariable("SDL_VIDEO_X11_WMCLASS") ?? "org.memoria.ff8" });
        }
        builder.LogToTrace().StartWithClassicDesktopLifetime([]);
    }
}

internal sealed class LauncherApplication : Application
{
    internal ControllerNavigation? Navigation { get; private set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        Styles.Add(new Style(selector => selector.OfType<ListBox>())
        {
            Setters =
            {
                new Setter(InputElement.FocusableProperty, true),
                new Setter(InputElement.IsTabStopProperty, false)
            }
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Navigation = new ControllerNavigation(desktop);
            desktop.MainWindow = Program.StartupError is string error ? Program.StartupIncident is { } incident ? new ErrorReportDialog(error, [incident]) : LauncherDialogs.Create("Memoria", error, false) : new LauncherWindow(Program.Resume);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
