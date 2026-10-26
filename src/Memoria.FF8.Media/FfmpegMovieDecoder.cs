using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Memoria.FF8.App.Tests")]

namespace Memoria.FF8.Media;

/// <summary>Movie geometry and timing. A zero frame count denotes presentation metadata with an unknown decoded frame count.</summary>
public sealed record MovieInformation(int Width, int Height, double FramesPerSecond, double DurationSeconds, int FrameCount)
{
    public MovieFileVersion? SourceVersion { get; internal init; }
}
public sealed record MovieFrame(long Index, byte[] Rgba);

/// <summary>Read-only decoding of installed AVI files. FFmpeg is a separate cached tool, not a runtime recompiler dependency.</summary>
public sealed partial class FfmpegMovieDecoder : IDisposable
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
        Information.SourceVersion?.Validate();
        StartSeconds = startSeconds;
        string offset = startSeconds.ToString(CultureInfo.InvariantCulture);
        video = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffmpeg"), "-v", "error", "-ss", offset, "-i", moviePath, "-an", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1");
        audio = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffmpeg"), "-v", "error", "-ss", offset, "-i", moviePath, "-vn", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", "pipe:1");
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

    public bool TryGetNextFrame(double seconds, out MovieFrame? frame)
    {
        frame = null;
        return frames.Reader.TryPeek(out MovieFrame? next) && next.Index / Information.FramesPerSecond <= seconds && frames.Reader.TryRead(out frame);
    }

    public bool TryGetAudio(out byte[]? buffer)
    {
        return samples.Reader.TryRead(out buffer);
    }

    public static MovieInformation Probe(string toolsDirectory, string moviePath)
    {
        using Process probe = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffprobe"), "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height,avg_frame_rate:format=duration", "-of", "json", moviePath);
        Task<string> errors = probe.StandardError.ReadToEndAsync();
        string json = probe.StandardOutput.ReadToEnd();
        probe.WaitForExit();
        if (probe.ExitCode != 0)
        {
            throw new InvalidDataException(errors.GetAwaiter().GetResult());
        }
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement stream = document.RootElement.GetProperty("streams")[0];
        string[] rate = stream.GetProperty("avg_frame_rate").GetString()!.Split('/');
        double fps = double.Parse(rate[0], CultureInfo.InvariantCulture) / double.Parse(rate[1], CultureInfo.InvariantCulture);
        double duration = double.Parse(document.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        int frameCount = stream.GetProperty("codec_name").GetString() == "vp8" ? CountVp8DisplayFrames(toolsDirectory, moviePath, stream.GetProperty("width").GetInt32(), stream.GetProperty("height").GetInt32()) : CountDecodedFrames(toolsDirectory, moviePath);
        if (frameCount <= 0)
        {
            throw new InvalidDataException("FFprobe could not determine the movie's decoded frame count.");
        }
        return new MovieInformation(stream.GetProperty("width").GetInt32(), stream.GetProperty("height").GetInt32(), fps, duration, frameCount);
    }

    /// <summary>Reads presentation metadata without counting packets or decoding the complete movie.</summary>
    public static MovieInformation ProbePresentation(string toolsDirectory, string moviePath)
    {
        MovieFileVersion? version = MovieFileVersion.Capture(moviePath);
        using Process probe = Start(Memoria.FF8.Platform.MediaToolPaths.ResolveExecutable(toolsDirectory, "ffprobe"), "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height,avg_frame_rate,duration:format=duration", "-of", "json", moviePath);
        Task<string> errors = probe.StandardError.ReadToEndAsync();
        string json = probe.StandardOutput.ReadToEnd();
        probe.WaitForExit();
        if (probe.ExitCode != 0)
        {
            throw new InvalidDataException(errors.GetAwaiter().GetResult());
        }
        version?.Validate();
        return ParsePresentationInformation(json) with { SourceVersion = version };
    }

    internal static MovieInformation ParsePresentationInformation(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("streams", out JsonElement streams) || streams.ValueKind != JsonValueKind.Array || streams.GetArrayLength() == 0)
        {
            throw new InvalidDataException("FFprobe found no movie video stream.");
        }
        JsonElement stream = streams[0];
        if (!stream.TryGetProperty("width", out JsonElement widthValue) || !widthValue.TryGetInt32(out int width) || width <= 0 || !stream.TryGetProperty("height", out JsonElement heightValue) || !heightValue.TryGetInt32(out int height) || height <= 0)
        {
            throw new InvalidDataException("Movie presentation requires positive video dimensions.");
        }
        string[] rate = stream.TryGetProperty("avg_frame_rate", out JsonElement rateValue) && rateValue.ValueKind == JsonValueKind.String ? rateValue.GetString()!.Split('/') : [];
        if (rate.Length != 2 || !double.TryParse(rate[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator) || !double.TryParse(rate[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator) || !double.IsFinite(numerator) || !double.IsFinite(denominator) || numerator <= 0 || denominator <= 0)
        {
            throw new InvalidDataException("Movie presentation requires a finite positive video frame rate.");
        }
        double fps = numerator / denominator;
        if (!double.IsFinite(fps) || fps <= 0)
        {
            throw new InvalidDataException("Movie presentation requires a finite positive video frame rate.");
        }
        if (!TryReadDuration(stream, out double duration) && (!root.TryGetProperty("format", out JsonElement format) || !TryReadDuration(format, out duration)))
        {
            throw new InvalidDataException("Movie presentation requires a finite positive duration.");
        }
        return new MovieInformation(width, height, fps, duration, 0);
    }

    private static bool TryReadDuration(JsonElement owner, out double duration)
    {
        duration = 0;
        return owner.TryGetProperty("duration", out JsonElement value) && value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration) && double.IsFinite(duration) && duration > 0;
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
