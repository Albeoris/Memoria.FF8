using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;

namespace Memoria.FF8;

internal sealed class CatalogMediaSide : IDisposable
{
    private const int Width = 640;
    private const int Height = 360;
    private const int FrameBytes = Width * Height * 4;
    private readonly CatalogMediaAsset? asset;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<VideoFrame> frames = Channel.CreateBounded<VideoFrame>(4);
    private DecoderPipe? videoPipe;
    private DecoderPipe? audioPipe;
    private PcmStreamSource? audioPcm;
    private Task? videoWorker;
    private double videoOffset;
    private long videoAvailableThrough;
    private bool audioFailed;
    private bool disposed;

    private CatalogMediaSide(string? name, CatalogMediaAsset? asset = null, string? error = null)
    {
        Name = name ?? string.Empty;
        this.asset = asset;
        Error = error;
    }

    public string Name { get; }
    public double DurationSeconds => asset?.Duration ?? 0;
    public bool HasAudio => asset?.AudioDuration > 0 && !Volatile.Read(ref audioFailed);
    public bool HasVideo => asset?.VideoDuration > 0;
    public string? Error { get; private set; }
    public PixelImage? Image { get; private set; }
    public long ImageRevision { get; private set; }
    internal double ImageTime { get; private set; }
    internal Task Cleanup { get; private set; } = Task.CompletedTask;

    public long VideoAvailableThrough => videoWorker is null || videoWorker.IsCompleted ? long.MaxValue : Volatile.Read(ref videoAvailableThrough);
    public bool AudioAvailable => Volatile.Read(ref audioFailed) || audioPcm is null || audioPcm.HasBufferedFrame || audioPcm.IsCompleted;

    public static CatalogMediaSide Unavailable(string? name, string error) => new(name, error: error);

    public static async Task<CatalogMediaSide> OpenAsync(string ffprobe, string ffmpeg, ModPreviewMediaInput? input, bool video, double startSeconds, CancellationToken token)
    {
        if (input is null)
        {
            return Unavailable(null, "Media could not be matched.");
        }
        try
        {
            CatalogMediaAsset asset = await CatalogMediaAsset.OpenAsync(ffprobe, ffmpeg, input, video, token).ConfigureAwait(false);
            return await StartAsync(input.Name, asset, startSeconds, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsMediaFailure(exception))
        {
            return Unavailable(input.Name, exception.Message);
        }
    }

    public Task<CatalogMediaSide> ReopenAsync(double startSeconds, CancellationToken token)
    {
        return asset is null ? Task.FromResult(Unavailable(Name, Error ?? "Media could not be matched.")) : RestartAsync();

        async Task<CatalogMediaSide> RestartAsync()
        {
            try
            {
                return await StartAsync(Name, asset!.Retain(), startSeconds, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsMediaFailure(exception))
            {
                return Unavailable(Name, exception.Message);
            }
        }
    }

    private static bool IsMediaFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException or NotSupportedException or TimeoutException or System.ComponentModel.Win32Exception or JsonException;
    }

    private static async Task<CatalogMediaSide> StartAsync(string name, CatalogMediaAsset asset, double startSeconds, CancellationToken token)
    {
        CatalogMediaSide side = new(name, asset);
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                await Task.WhenAll(side.StartVideoAsync(startSeconds, deadline.Token), side.StartAudioAsync(startSeconds, deadline.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                throw new TimeoutException("Preview decoding did not produce media within 20 seconds.");
            }
            token.ThrowIfCancellationRequested();
            side.UpdateImage(startSeconds);
            return side;
        }
        catch
        {
            side.Dispose();
            await side.Cleanup.ConfigureAwait(false);
            throw;
        }
    }

    private async Task StartVideoAsync(double startSeconds, CancellationToken token)
    {
        if (!HasVideo)
        {
            return;
        }
        double endPreroll = Math.Max(0.08, asset!.VideoFrameSeconds * 2);
        videoOffset = Math.Min(startSeconds, Math.Max(0, asset.VideoDuration - endPreroll));
        ProcessStartInfo start = asset.Decode(videoOffset);
        CatalogMediaAsset.Add(start, "-loglevel", "info", "-nostats", "-hide_banner", "-map", "0:V:0", "-an", "-vf",
            $"scale={Width}:{Height}:force_original_aspect_ratio=decrease,pad={Width}:{Height}:(ow-iw)/2:(oh-ih)/2,setsar=1,showinfo=checksum=0",
            "-fps_mode", "passthrough", "-threads", "1", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1");
        videoPipe = new(start, lifetime.Token, trackVideoTime: true);
        videoWorker = ReadVideoAsync(videoPipe, lifetime.Token);
        if (!await frames.Reader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            await videoWorker.ConfigureAwait(false);
            throw new InvalidDataException("FFmpeg did not produce a video frame.");
        }
        if (startSeconds > videoOffset)
        {
            while (await frames.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                UpdateImage(startSeconds);
                if (frames.Reader.TryPeek(out VideoFrame frame) && frame.Seconds > startSeconds)
                {
                    break;
                }
            }
        }
    }

    private async Task ReadVideoAsync(DecoderPipe decoder, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                byte[] pixels = ArrayPool<byte>.Shared.Rent(FrameBytes);
                bool queued = false;
                try
                {
                    int length = await decoder.Output.ReadAtLeastAsync(pixels.AsMemory(0, FrameBytes), FrameBytes, false, token).ConfigureAwait(false);
                    if (length == 0)
                    {
                        break;
                    }
                    if (length != FrameBytes)
                    {
                        throw new InvalidDataException("FFmpeg produced an incomplete preview frame.");
                    }
                    double timestamp = videoOffset + await decoder.ReadPresentationTimeAsync(token).ConfigureAwait(false);
                    await frames.Writer.WriteAsync(new(timestamp, pixels), token).ConfigureAwait(false);
                    queued = true;
                    Volatile.Write(ref videoAvailableThrough, checked((long)Math.Floor(timestamp * CatalogMediaPlayback.SampleRate) + 1));
                }
                finally
                {
                    if (!queued)
                    {
                        ArrayPool<byte>.Shared.Return(pixels);
                    }
                }
            }
            await decoder.Completion.ConfigureAwait(false);
        }
        finally
        {
            frames.Writer.TryComplete();
        }
    }

    private async Task StartAudioAsync(double startSeconds, CancellationToken token)
    {
        if (!HasAudio || startSeconds >= asset!.AudioDuration)
        {
            return;
        }
        try
        {
            ProcessStartInfo start = asset.Decode(startSeconds);
            CatalogMediaAsset.Add(start, "-map", "0:a:0", "-vn", "-af", "aresample=44100:async=1:first_pts=0", "-f", "s16le", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2", "pipe:1");
            audioPipe = new(start, lifetime.Token);
            audioPcm = await PcmStreamSource.CreateAsync(audioPipe.Output, 0, token).ConfigureAwait(false);
            if (audioPcm.IsCompleted)
            {
                await audioPipe.Completion.WaitAsync(token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (IsMediaFailure(exception))
        {
            Error = exception.Message;
            Volatile.Write(ref audioFailed, true);
        }
    }

    public void CheckForFailure()
    {
        if (videoWorker?.IsFaulted == true)
        {
            Error = videoWorker.Exception!.GetBaseException().Message;
        }
        if (!Volatile.Read(ref audioFailed))
        {
            try
            {
                audioPcm?.ThrowIfFailed();
                if (audioPipe?.Completion.IsFaulted == true)
                {
                    audioPipe.Completion.GetAwaiter().GetResult();
                }
            }
            catch (Exception exception) when (IsMediaFailure(exception))
            {
                Error = exception.Message;
                Volatile.Write(ref audioFailed, true);
            }
        }
    }

    public void UpdateImage(double seconds)
    {
        VideoFrame? latest = null;
        while (frames.Reader.TryPeek(out VideoFrame frame) && (frame.Seconds <= seconds || Image is null && latest is null) && frames.Reader.TryRead(out frame))
        {
            if (latest is { } skipped)
            {
                ArrayPool<byte>.Shared.Return(skipped.Pixels);
            }
            latest = frame;
        }
        if (latest is { } ready)
        {
            try
            {
                Image ??= new(Width, Height);
                MemoryMarshal.Cast<byte, Rgba32>(ready.Pixels.AsSpan(0, FrameBytes)).CopyTo(Image.Pixels);
                ImageTime = ready.Seconds;
                ImageRevision++;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(ready.Pixels);
            }
        }
    }

    public void RenderAudio(out double left, out double right)
    {
        if (audioPcm is not null && !Volatile.Read(ref audioFailed))
        {
            audioPcm.RenderFrame(out left, out right);
        }
        else
        {
            left = right = 0;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        videoPipe?.Stop();
        audioPipe?.Stop();
        Cleanup = CleanUpAsync();
    }

    private async Task CleanUpAsync()
    {
        try
        {
            await Task.WhenAll(videoWorker ?? Task.CompletedTask, videoPipe?.Completion ?? Task.CompletedTask,
                audioPipe?.Completion ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException || IsMediaFailure(exception))
        {
            // A terminated child or incomplete frame is expected after closing a preview.
        }
        finally
        {
            audioPcm?.Dispose();
            audioPipe?.Dispose();
            videoPipe?.Dispose();
            while (frames.Reader.TryRead(out VideoFrame frame))
            {
                ArrayPool<byte>.Shared.Return(frame.Pixels);
            }
            lifetime.Dispose();
            asset?.Release();
        }
    }

    private readonly record struct VideoFrame(double Seconds, byte[] Pixels);

    private sealed class DecoderPipe : IDisposable
    {
        private readonly Process process;
        private readonly Channel<double>? timestamps;

        public DecoderPipe(ProcessStartInfo start, CancellationToken token, bool trackVideoTime = false)
        {
            timestamps = trackVideoTime ? Channel.CreateBounded<double>(8) : null;
            process = Process.Start(start) ?? throw new IOException("Could not start the preview decoder.");
            Completion = ObserveExitAsync(token);
        }

        public Stream Output => process.StandardOutput.BaseStream;
        public Task Completion { get; }

        public async ValueTask<double> ReadPresentationTimeAsync(CancellationToken token)
        {
            if (await timestamps!.Reader.WaitToReadAsync(token).ConfigureAwait(false) && timestamps.Reader.TryRead(out double timestamp))
            {
                return timestamp;
            }
            throw new InvalidDataException("FFmpeg produced a preview frame without its presentation time.");
        }

        private async Task ObserveExitAsync(CancellationToken token)
        {
            StringBuilder diagnostic = new();
            try
            {
                while (await process.StandardError.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                {
                    if (timestamps is not null && line.StartsWith("[Parsed_showinfo_", StringComparison.Ordinal))
                    {
                        int begin = line.IndexOf(" pts_time:", StringComparison.Ordinal);
                        if (begin >= 0)
                        {
                            ReadOnlySpan<char> value = line.AsSpan(begin + " pts_time:".Length).TrimStart();
                            int end = value.IndexOf(' ');
                            if (double.TryParse(end < 0 ? value : value[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds))
                            {
                                await timestamps.Writer.WriteAsync(seconds, token).ConfigureAwait(false);
                            }
                        }
                    }
                    else if (diagnostic.Length < 2048)
                    {
                        diagnostic.Append(line.AsSpan(0, Math.Min(line.Length, 2047 - diagnostic.Length))).Append('\n');
                    }
                }
                await process.WaitForExitAsync().ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidDataException($"FFmpeg preview decoding failed. {diagnostic.ToString().Trim()}");
                }
            }
            finally
            {
                timestamps?.Writer.TryComplete();
            }
        }

        public void Stop()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process can exit between HasExited and Kill.
            }
        }

        public void Dispose() => process.Dispose();
    }
}
