using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

namespace Memoria.FF8.Media;

public sealed record MovieInformation(int Width, int Height, double FramesPerSecond, double DurationSeconds, int FrameCount);
public sealed record MovieFrame(long Index, byte[] Rgba);

/// <summary>Read-only decoding of installed AVI files. FFmpeg is a separate cached tool, not a runtime recompiler dependency.</summary>
public sealed class FfmpegMovieDecoder : IDisposable
{
    private readonly Process video;
    private readonly Process audio;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Channel<MovieFrame> frames = Channel.CreateBounded<MovieFrame>(3);
    private readonly Channel<byte[]> samples = Channel.CreateBounded<byte[]>(8);
    private readonly Task videoTask;
    private readonly Task audioTask;

    public FfmpegMovieDecoder(string toolsDirectory, string moviePath, double startSeconds = 0, MovieInformation? information = null)
    {
        Information = information ?? Probe(toolsDirectory, moviePath);
        StartSeconds = startSeconds;
        string offset = startSeconds.ToString(CultureInfo.InvariantCulture);
        video = Start(Path.Combine(toolsDirectory, "ffmpeg.exe"), "-v", "error", "-ss", offset, "-i", moviePath, "-an", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1");
        audio = Start(Path.Combine(toolsDirectory, "ffmpeg.exe"), "-v", "error", "-ss", offset, "-i", moviePath, "-vn", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", "pipe:1");
        videoTask = Task.Run(ReadVideo);
        audioTask = Task.Run(ReadAudio);
    }

    public MovieInformation Information { get; }
    public double StartSeconds { get; }
    public bool VideoFinished => videoTask.IsCompleted && !frames.Reader.TryPeek(out _);
    public bool AudioFinished => audioTask.IsCompleted && !samples.Reader.TryPeek(out _);
    public string? Error { get; private set; }

    public bool TryGetFrame(double seconds, out MovieFrame? frame)
    {
        frame = null;
        while (frames.Reader.TryPeek(out MovieFrame? next) && next.Index / Information.FramesPerSecond <= seconds)
        {
            frames.Reader.TryRead(out frame);
        }
        return frame is not null;
    }

    public bool TryGetAudio(out byte[]? buffer)
    {
        return samples.Reader.TryRead(out buffer);
    }

    public static MovieInformation Probe(string toolsDirectory, string moviePath)
    {
        using Process probe = Start(Path.Combine(toolsDirectory, "ffprobe.exe"), "-v", "error", "-select_streams", "v:0", "-count_frames", "-show_entries", "stream=width,height,avg_frame_rate,nb_frames,nb_read_frames:format=duration", "-of", "json", moviePath);
        string json = probe.StandardOutput.ReadToEnd();
        probe.WaitForExit();
        if (probe.ExitCode != 0)
        {
            throw new InvalidDataException(probe.StandardError.ReadToEnd());
        }
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement stream = document.RootElement.GetProperty("streams")[0];
        string[] rate = stream.GetProperty("avg_frame_rate").GetString()!.Split('/');
        double fps = double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture);
        double duration = double.Parse(document.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        int frameCount = 0;
        foreach (string property in new[] { "nb_read_frames", "nb_frames" })
        {
            if (stream.TryGetProperty(property, out JsonElement count) && int.TryParse(count.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out frameCount) && frameCount > 0)
            {
                break;
            }
        }
        if (frameCount <= 0)
        {
            throw new InvalidDataException("FFprobe could not determine the movie's decoded frame count.");
        }
        return new MovieInformation(stream.GetProperty("width").GetInt32(), stream.GetProperty("height").GetInt32(), fps, duration, frameCount);
    }

    private async Task ReadVideo()
    {
        try
        {
            long index = 0;
            while (!cancellation.IsCancellationRequested)
            {
                byte[] buffer = new byte[checked(Information.Width * Information.Height * 4)];
                if (!await ReadFull(video.StandardOutput.BaseStream, buffer, cancellation.Token))
                {
                    break;
                }
                await frames.Writer.WriteAsync(new MovieFrame(index++, buffer), cancellation.Token);
            }
            if (!cancellation.IsCancellationRequested)
            {
                await video.WaitForExitAsync(cancellation.Token);
                if (video.ExitCode != 0)
                {
                    Error = await video.StandardError.ReadToEndAsync(cancellation.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            frames.Writer.TryComplete();
        }
    }

    private async Task ReadAudio()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                byte[] buffer = new byte[17640];
                int count = 0;
                while (count < buffer.Length)
                {
                    int read = await audio.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(count), cancellation.Token);
                    if (read == 0)
                    {
                        break;
                    }
                    count += read;
                }
                if (count == 0)
                {
                    break;
                }
                if (count != buffer.Length)
                {
                    Array.Resize(ref buffer, count - count % 4);
                }
                await samples.Writer.WriteAsync(buffer, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            samples.Writer.TryComplete();
        }
    }

    private static async Task<bool> ReadFull(Stream stream, byte[] buffer, CancellationToken token)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer.AsMemory(offset), token);
            if (count == 0)
            {
                return false;
            }
            offset += count;
        }
        return true;
    }

    private static Process Start(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        return Process.Start(info) ?? throw new InvalidOperationException($"Cannot start {executable}.");
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (Process process in new[] { video, audio })
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        try
        {
            Task.WaitAll([videoTask, audioTask], TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        video.Dispose();
        audio.Dispose();
        cancellation.Dispose();
    }
}
