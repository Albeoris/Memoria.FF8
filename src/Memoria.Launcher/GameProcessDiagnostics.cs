using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Memoria.Launcher;

internal sealed record GraphicsRestartRecovery(string StatePath, string Sha256)
{
    internal void ApplyTo(ProcessStartInfo start)
    {
        if (!Path.IsPathFullyQualified(StatePath) || Path.GetExtension(StatePath) != ".ff8state" || Sha256.Length != 64 || !Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("The preserved graphics restart save has an invalid path or hash.");
        }
        UpdateInstaller.RejectLinks(StatePath);
        using FileStream stream = File.OpenRead(StatePath);
        if (stream.Length == 0 || !Convert.ToHexString(SHA256.HashData(stream)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The preserved graphics restart save changed. Select a known save manually; the current file has been retained.");
        }
        start.ArgumentList.Add("--state");
        start.ArgumentList.Add(StatePath);
    }
}

internal sealed class GameProcessDiagnostics : IDisposable
{
    private const int TailCapacity = 6000;
    private readonly object gate = new();
    private readonly StringBuilder errorTail = new();
    private readonly StringBuilder outputTail = new();
    private readonly StringBuilder readinessLine = new();
    private string? graphicsReady;
    private GraphicsRestartRecovery? restartRecovery;
    public GraphicsRestartRecovery? RestartRecovery
    {
        get
        {
            lock (gate)
            {
                return restartRecovery;
            }
        }
    }
    public string? GraphicsReady
    {
        get
        {
            lock (gate)
            {
                return graphicsReady;
            }
        }
    }
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
                    if (source == "stdout")
                    {
                        RecordReadiness(text);
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

    internal void RecordReadiness(string text)
    {
        lock (gate)
        {
            foreach (char character in text)
            {
                if (character == '\n')
                {
                    string line = readinessLine.ToString().TrimEnd('\r');
                    if (line.StartsWith("Graphics ready: requested=", StringComparison.Ordinal) && line.Contains(" actual=", StringComparison.Ordinal))
                    {
                        graphicsReady = line;
                        restartRecovery = null;
                    }
                    const string recoveryPrefix = "Graphics restart recovery: ";
                    if (line.StartsWith(recoveryPrefix, StringComparison.Ordinal))
                    {
                        graphicsReady = null;
                        restartRecovery = null;
                        try
                        {
                            using JsonDocument document = JsonDocument.Parse(line[recoveryPrefix.Length..]);
                            string? state = document.RootElement.GetProperty("statePath").GetString();
                            string? hash = document.RootElement.GetProperty("sha256").GetString();
                            if (state is not null && Path.IsPathFullyQualified(state) && Path.GetExtension(state) == ".ff8state" && hash is { Length: 64 } && hash.All(Uri.IsHexDigit))
                            {
                                restartRecovery = new GraphicsRestartRecovery(state, hash);
                            }
                        }
                        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
                        {
                            WriteLog("launcher", "Invalid graphics restart recovery information: " + exception.Message);
                        }
                    }
                    readinessLine.Clear();
                }
                else
                {
                    readinessLine.Append(character);
                    if (readinessLine.Length > TailCapacity)
                    {
                        readinessLine.Remove(0, readinessLine.Length - TailCapacity);
                    }
                }
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
