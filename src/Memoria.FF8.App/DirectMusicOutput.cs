using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Memoria.FF8;

/// <summary>Windows DirectMusic performance is x86-only; its isolated output process owns no game state.</summary>
internal sealed class DirectMusicOutput : IDisposable
{
    private readonly Process process;

    public DirectMusicOutput(nint window, string directory)
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "audio-host", "Memoria.FF8.Music.exe");
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(window.ToInt64().ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(directory);
        process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the DirectMusic output process.");
        try
        {
            ReadResult();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Play(string path, int volume, int startPoint = 0) => Send(new { Command = "Play", Path = path, Volume = volume, StartPoint = startPoint });
    public void Volume(int hundredthDecibels) => Send(new { Command = "Volume", Volume = hundredthDecibels });
    public void Stop() => Send(new { Command = "Stop" });
    public void Pause() => Send(new { Command = "Pause" });
    public void Resume() => Send(new { Command = "Resume" });
    public bool IsPlaying => Send(new { Command = "Playing" });

    private bool Send(object command)
    {
        process.StandardInput.WriteLine(JsonSerializer.Serialize(command));
        process.StandardInput.Flush();
        return ReadResult();
    }

    private bool ReadResult()
    {
        string? response;
        try
        {
            response = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        catch (TimeoutException)
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(2000);
            }
            throw new TimeoutException("DirectMusic output did not answer within10 seconds; its process was stopped.");
        }
        if (response is null)
        {
            throw new IOException("DirectMusic output process closed its command channel.");
        }
        using JsonDocument document = JsonDocument.Parse(response);
        if (document.RootElement.TryGetProperty("Error", out JsonElement error))
        {
            throw new InvalidOperationException($"DirectMusic output: {error.GetString()}");
        }
        return document.RootElement.GetProperty("Result").GetBoolean();
    }

    public void Dispose()
    {
        if (!process.HasExited)
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(2000))
            {
                process.Kill();
                process.WaitForExit();
            }
        }
        process.Dispose();
    }
}
