using System.Windows;

namespace Memoria.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] arguments)
    {
        try
        {
            if (arguments.FirstOrDefault() is "--apply-update" or "--recover-update")
            {
                UpdateInstaller.RunHelper(arguments);
                return;
            }
            string root = AppContext.BaseDirectory;
            using Mutex mutex = new(false, UpdateInstaller.MutexName(root));
            bool acquired;
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
                MessageBox.Show("Memoria's launcher is already open for this installation.", "Memoria");
                return;
            }
            try
            {
                if (UpdateInstaller.NeedsRecovery(root))
                {
                    UpdateInstaller.StartHelperAsync(Path.TrimEndingDirectorySeparator(root), recovery: true).GetAwaiter().GetResult();
                    return;
                }
                Application application = new();
                application.Run(new LauncherWindow());
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show("Memoria could not complete startup or installation. " + exception.Message + "\nIf update recovery failed, keep the .memoria-update folder and its backups, close running game processes, and start the launcher again.", "Memoria", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
