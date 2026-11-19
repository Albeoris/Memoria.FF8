using System.Diagnostics;
using Memoria.FF8.Audio;
using Memoria.FF8.Mods;
using Memoria.FF8.Platform;

namespace Memoria.FF8;

/// <summary>Plays two independently decoded media sources against one presentation clock.</summary>
internal sealed class CatalogMediaPlayback : IDisposable, IAsyncDisposable
{
    internal const int SampleRate = 44100;
    private readonly PlaybackClock clock;
    private readonly Stopwatch elapsed = new();
    private readonly PcmAudioOutput? audioOutput;
    private readonly bool audioEnabled;
    private readonly bool ownsAudioOutput;
    private double pendingFrames;
    private bool sourceAdded;
    private bool disposed;

    private CatalogMediaPlayback(CatalogMediaSide before, CatalogMediaSide after, double startSeconds, bool audioEnabled, PcmAudioOutput? output)
    {
        Before = before;
        After = after;
        DurationSeconds = Math.Max(before.DurationSeconds, after.DurationSeconds);
        PositionSeconds = Math.Min(startSeconds, DurationSeconds);
        clock = new(before, after, ToFrames(PositionSeconds), ToFrames(DurationSeconds));
        this.audioEnabled = audioEnabled;
        if (audioEnabled && (before.HasAudio || after.HasAudio))
        {
            ownsAudioOutput = output is null;
            audioOutput = output ?? new();
        }
        ListenAfter = after.HasAudio || !before.HasAudio;
    }

    public CatalogMediaSide Before { get; }
    public CatalogMediaSide After { get; }
    public double DurationSeconds { get; }
    public double PositionSeconds { get; private set; }
    public bool IsPlaying { get; private set; }

    public bool ListenAfter
    {
        get => clock.ListenAfter;
        set => clock.ListenAfter = value;
    }

    public static Task<CatalogMediaPlayback> OpenAsync(string? tools, ModPreviewMediaInput? before, ModPreviewMediaInput? after, bool video, CancellationToken token, double startSeconds = 0, bool audioEnabled = true, PcmAudioOutput? output = null)
    {
        ValidatePosition(startSeconds);
        token.ThrowIfCancellationRequested();
        string? ffprobe = tools is null ? null : MediaToolPaths.ResolveExecutable(tools, "ffprobe");
        string? ffmpeg = tools is null ? null : MediaToolPaths.ResolveExecutable(tools, "ffmpeg");
        if (!File.Exists(ffprobe) || !File.Exists(ffmpeg))
        {
            const string message = "Set up FFmpeg in the launcher to compare media.";
            return Task.FromResult(new CatalogMediaPlayback(CatalogMediaSide.Unavailable(before?.Name, message), CatalogMediaSide.Unavailable(after?.Name, message), startSeconds, audioEnabled, output));
        }
        return CompleteAsync(CatalogMediaSide.OpenAsync(ffprobe!, ffmpeg!, before, video, startSeconds, token),
            CatalogMediaSide.OpenAsync(ffprobe!, ffmpeg!, after, video, startSeconds, token), startSeconds, audioEnabled, output, token);
    }

    public async Task<CatalogMediaPlayback> SeekAsync(double positionSeconds, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidatePosition(positionSeconds);
        double target = Math.Min(positionSeconds, DurationSeconds);
        CatalogMediaPlayback replacement = await CompleteAsync(Before.ReopenAsync(target, token), After.ReopenAsync(target, token),
            target, audioEnabled, ownsAudioOutput ? null : audioOutput, token).ConfigureAwait(false);
        replacement.ListenAfter = ListenAfter;
        return replacement;
    }

    private static async Task<CatalogMediaPlayback> CompleteAsync(Task<CatalogMediaSide> before, Task<CatalogMediaSide> after, double startSeconds, bool audioEnabled, PcmAudioOutput? output, CancellationToken token)
    {
        try
        {
            await Task.WhenAll(before, after).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new(before.Result, after.Result, startSeconds, audioEnabled, output);
        }
        catch
        {
            if (before.IsCompletedSuccessfully)
            {
                before.Result.Dispose();
            }
            if (after.IsCompletedSuccessfully)
            {
                after.Result.Dispose();
            }
            await Task.WhenAll(before.IsCompletedSuccessfully ? before.Result.Cleanup : Task.CompletedTask,
                after.IsCompletedSuccessfully ? after.Result.Cleanup : Task.CompletedTask).ConfigureAwait(false);
            throw;
        }
    }

    public void SetPlaying(bool playing)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (playing == IsPlaying || playing && PositionSeconds >= DurationSeconds)
        {
            return;
        }
        if (playing && !sourceAdded && audioOutput is not null)
        {
            audioOutput.AddSource(clock, audioOutput.Movie);
            sourceAdded = true;
        }
        lock (clock)
        {
            clock.Playing = playing;
        }
        IsPlaying = playing;
        elapsed.Restart();
    }

    public void Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Before.CheckForFailure();
        After.CheckForFailure();
        if (sourceAdded)
        {
            PositionSeconds = Math.Min(DurationSeconds, audioOutput!.CapturePlaybackPosition(clock.PositionBeforeQueuedFrames) / (double)SampleRate);
        }
        else if (IsPlaying)
        {
            pendingFrames += elapsed.Elapsed.TotalSeconds * SampleRate;
            elapsed.Restart();
            long frames = (long)pendingFrames;
            if (frames > 0)
            {
                pendingFrames -= frames;
                clock.AdvanceSilently(frames);
                PositionSeconds = Math.Min(DurationSeconds, clock.PositionBeforeQueuedFrames(0) / (double)SampleRate);
            }
        }
        Before.UpdateImage(PositionSeconds);
        After.UpdateImage(PositionSeconds);
        if (IsPlaying && PositionSeconds >= DurationSeconds)
        {
            SetPlaying(false);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (sourceAdded)
        {
            audioOutput!.RemoveSource(clock);
        }
        if (ownsAudioOutput)
        {
            audioOutput!.Dispose();
        }
        Before.Dispose();
        After.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.WhenAll(Before.Cleanup, After.Cleanup).ConfigureAwait(false);
    }

    private static void ValidatePosition(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds >= (double)long.MaxValue / SampleRate)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
    }

    private static long ToFrames(double seconds) => (long)Math.Ceiling(seconds * SampleRate);

    private sealed class PlaybackClock : IAudioSource
    {
        private readonly CatalogMediaSide before;
        private readonly CatalogMediaSide after;
        private readonly long end;
        private readonly AudioPlaybackTimeline timeline = new();
        private long position;
        private bool listenAfter;

        public PlaybackClock(CatalogMediaSide before, CatalogMediaSide after, long initial, long end)
        {
            this.before = before;
            this.after = after;
            this.end = end;
            position = initial;
            timeline.Reset(initial);
        }

        public bool Playing { get; set; }

        public bool ListenAfter
        {
            get => Volatile.Read(ref listenAfter);
            set => Volatile.Write(ref listenAfter, value);
        }

        public long PositionBeforeQueuedFrames(int queuedFrames)
        {
            lock (this)
            {
                return timeline.RecordedFrames == 0 ? position : timeline.PositionBeforeQueuedFrames(queuedFrames);
            }
        }

        public void AdvanceSilently(long frames)
        {
            long target = Math.Min(position + Math.Min(frames, end - position), Math.Min(before.VideoAvailableThrough, after.VideoAvailableThrough));
            if (!before.HasAudio && !after.HasAudio)
            {
                position = Math.Max(position, target);
                return;
            }
            while (position < target && before.AudioAvailable && after.AudioAvailable)
            {
                before.RenderAudio(out _, out _);
                after.RenderAudio(out _, out _);
                position++;
            }
        }

        public void RenderFrame(out double left, out double right)
        {
            lock (this)
            {
                left = right = 0;
                if (Playing && position < end && position < before.VideoAvailableThrough && position < after.VideoAvailableThrough && before.AudioAvailable && after.AudioAvailable)
                {
                    before.RenderAudio(out double beforeLeft, out double beforeRight);
                    after.RenderAudio(out double afterLeft, out double afterRight);
                    (left, right) = ListenAfter ? (afterLeft, afterRight) : (beforeLeft, beforeRight);
                    position++;
                }
                timeline.Record(position);
            }
        }
    }
}
