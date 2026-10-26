using System.Diagnostics;
using System.Text;

namespace Memoria.Launcher;

internal sealed class GameProcessDiagnostics : IDisposable
{
    private const int TailCapacity = 6000;
    private readonly object gate = new();
    private readonly StringBuilder errorTail = new();
    private readonly StringBuilder outputTail = new();
    private StreamWriter? log;
    private string? logWarning;
    private TextWriter? console;

    public string? LogPath { get; private set; }

    public GameProcessDiagnostics(string? directory = null, string? fallbackDirectory = null, TextWriter? console = null)
    {
        this.console = console;
        directory ??= Memoria.FF8.Platform.ApplicationPaths.Current.Logs;
        fallbackDirectory ??= Path.Combine(Path.GetTempPath(), "Memoria.FF8", "logs");
        foreach (string candidate in new[] { directory, fallbackDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(candidate);
                string path = Path.Combine(candidate, $"game-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.log");
                log = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                LogPath = path;
                WriteLog("launcher", $"Started {DateTimeOffset.Now:O}; launcher version {typeof(GameProcessDiagnostics).Assembly.GetName().Version}{Environment.NewLine}");
                if (log is not null)
                {
                    break;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logWarning = $"Could not save the game log: {exception.Message}";
            }
        }
        if (log is not null)
        {
            logWarning = null;
        }
    }

    public Task CaptureAsync(Process process)
    {
        return Task.WhenAll(CaptureAsync(process.StandardOutput, "stdout"), CaptureAsync(process.StandardError, "stderr"));
    }

    private async Task CaptureAsync(TextReader reader, string source)
    {
        char[] buffer = new char[2048];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
            {
                string text = new(buffer, 0, count);
                lock (gate)
                {
                    StringBuilder tail = source == "stderr" ? errorTail : outputTail;
                    tail.Append(text);
                    if (tail.Length > TailCapacity)
                    {
                        tail.Remove(0, tail.Length - TailCapacity);
                    }
                    WriteLog(source, text);
                    WriteConsole(text);
                }
            }
        }
        catch (IOException exception)
        {
            lock (gate)
            {
                logWarning = $"Could not read game {source}: {exception.Message}";
                WriteLog("launcher", logWarning);
            }
        }
    }

    public string Complete(int exitCode)
    {
        lock (gate)
        {
            WriteLog("launcher", $"Exited {DateTimeOffset.Now:O}; code {exitCode} (0x{unchecked((uint)exitCode):X8}){Environment.NewLine}");
            CloseLog();
            string location = LogPath is null ? "No game log could be saved." : $"Game log: {LogPath}";
            if (logWarning is not null)
            {
                location += Environment.NewLine + logWarning;
            }
            if (exitCode == 0)
            {
                return $"The game has closed. Ready to play again.{Environment.NewLine}{location}";
            }
            string details = errorTail.ToString().Trim();
            if (details.Length == 0)
            {
                details = "The process ended without reporting an error.";
                if (outputTail.Length > 0)
                {
                    details += $"{Environment.NewLine}Last console output:{Environment.NewLine}{outputTail.ToString().Trim()}";
                }
            }
            else
            {
                details = $"Last error output:{Environment.NewLine}{details}";
            }
            return $"The game exited with code {exitCode} (0x{unchecked((uint)exitCode):X8}).{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}{location}";
        }
    }

    private void WriteLog(string source, string text)
    {
        try
        {
            log?.WriteLine($"[{DateTimeOffset.Now:O} {source}] {text}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logWarning = $"The game log is incomplete: {exception.Message}";
            CloseLog();
        }
    }

    private void WriteConsole(string text)
    {
        try
        {
            console?.Write(text);
            console?.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // A closed console must not interrupt pipe draining or the complete file log.
            console = null;
        }
    }

    private void CloseLog()
    {
        StreamWriter? writer = log;
        log = null;
        try
        {
            writer?.Dispose();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logWarning = $"The game log is incomplete: {exception.Message}";
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            CloseLog();
        }
    }
}
