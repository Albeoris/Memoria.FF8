using System.Runtime.InteropServices;
using System.Text;

namespace Memoria.Launcher;

internal static class GameConsole
{
    public static TextWriter? Open()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        // Reuse an attached console, or keep a new one alive with the launcher after the game exits.
        AllocConsole();
        SetConsoleOutputCP(65001);
        try
        {
            return new ConsoleWriter(new FileStream(@"\\.\CONOUT$", FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // File diagnostics remain available if the console cannot be opened.
            return null;
        }
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleOutputCP(uint codePage);

    internal sealed class ConsoleWriter(Stream stream) : StreamWriter(stream, new UTF8Encoding(false))
    {
        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                // A final flush to an unavailable console must not hide the completed game log.
            }
        }
    }
}
