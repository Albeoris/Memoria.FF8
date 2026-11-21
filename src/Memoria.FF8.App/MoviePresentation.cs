using System.Diagnostics;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed class MoviePresentation : IDisposable
{
    private readonly FfmpegMovieDecoder decoder;
    private readonly PcmAudioOutput audioOutput;
    private readonly MoviePcmPlayback sound;
    private readonly bool audioEnabled;
    private readonly Stopwatch clock = new();
    private bool started;
    private bool suspended;
    private bool soundAttached;
    private readonly MovieFrameTimeline? nativeTimeline;
    private readonly MovieInformation? nativeInformation;
    private readonly MovieSubtitleRenderer? subtitleRenderer;
    private bool callbackActive = true;
    private byte[] decodedPixels = [];
    private int decodedMediaFrame = -1;

    public MoviePresentation(GraphicsDevice device, PcmAudioOutput audioOutput, string toolsDirectory, string path, double startSeconds, MovieInformation? information = null, bool audioEnabled = true, MovieInformation? nativeInformation = null, MovieSubtitleTrack? subtitles = null)
    {
        this.audioOutput = audioOutput;
        this.audioEnabled = audioEnabled;
        decoder = new FfmpegMovieDecoder(toolsDirectory, path, startSeconds, information);
        sound = new MoviePcmPlayback(() => decoder.TryGetAudio(out byte[]? buffer) ? buffer : null);
        Texture = new Texture2D(device, decoder.Information.Width, decoder.Information.Height);
        this.nativeInformation = nativeInformation ?? (decoder.Information.FrameCount > 0 ? decoder.Information : null);
        if (this.nativeInformation is not null)
        {
            nativeTimeline = new MovieFrameTimeline(this.nativeInformation, MovieFrameTimeline.FrameAt(startSeconds, this.nativeInformation));
        }
        if (subtitles is not null)
        {
            subtitleRenderer = new MovieSubtitleRenderer(device, subtitles);
        }
    }

    public Texture2D Texture { get; }
    public ReadOnlySpan<byte> DecodedPixels => decodedPixels;
    public double PositionSeconds => decoder.StartSeconds + clock.Elapsed.TotalSeconds;
    public bool Finished
    {
        get
        {
            if (!started)
            {
                return false;
            }
            bool videoFinished = decoder.VideoFinished;
            bool audioFinished = decoder.AudioFinished;
            bool pendingAudio = videoFinished && audioFinished && (soundAttached ? audioOutput.CapturePlaybackPosition(sound.HasPendingAudio) : sound.HasPendingAudio(0));
            return IsComplete(PositionSeconds, decoder.Information.DurationSeconds, videoFinished, audioFinished, pendingAudio);
        }
    }
    public bool HasFrame => started;
    internal object InspectDecodedFrame()
    {
        int visiblePixels = 0;
        for (int offset = 0; offset + 3 < decodedPixels.Length; offset += 4)
        {
            if (decodedPixels[offset] > 16 || decodedPixels[offset + 1] > 16 || decodedPixels[offset + 2] > 16)
            {
                visiblePixels++;
            }
        }
        return new { MediaFrame = decodedMediaFrame, VisiblePixels = visiblePixels, ByteCount = decodedPixels.Length, decoder.Information.Width, decoder.Information.Height, decoder.Information.FramesPerSecond, SourcePath = decoder.Information.SourceVersion?.Path };
    }
    public void Suspend()
    {
        suspended = true;
        clock.Stop();
        sound.Pause(true);
    }

    public void Resume()
    {
        suspended = false;
        if (started)
        {
            clock.Start();
        }
        sound.Pause(false);
    }
    public string? Error => decoder.Error;
    public MoviePlaybackState Playback => nativeTimeline is not null && nativeInformation is not null ? new(nativeTimeline.Frame, nativeInformation.FrameCount - 1, !Finished, Finished, false) : throw new NotSupportedException("Script movie playback requires an exact native movie timeline.");

    internal static bool IsComplete(double position, double duration, bool videoFinished, bool audioFinished, bool pendingAudio)
    {
        return position >= duration && videoFinished && audioFinished && !pendingAudio;
    }

    public void Update(Func<MoviePlaybackState, double, bool>? decodedFrame = null, Action<int>? mediaFrame = null)
    {
        if (decodedFrame is not null && nativeTimeline is null)
        {
            throw new NotSupportedException("Script movie callbacks require an exact native movie timeline.");
        }
        if (soundAttached)
        {
            audioOutput.ThrowIfFailed();
        }
        while (decoder.TryGetNextFrame(clock.Elapsed.TotalSeconds, out MovieFrame? frame))
        {
            Texture.SetData(frame!.Rgba);
            decodedPixels = frame.Rgba;
            int mediaIndex = checked(MovieClock.AbsoluteFrame(decoder.StartSeconds, decoder.Information) + (int)frame.Index);
            if (decoder.Information.FrameCount > 0)
            {
                mediaIndex = Math.Min(decoder.Information.FrameCount - 1, mediaIndex);
            }
            decodedMediaFrame = mediaIndex;
            mediaFrame?.Invoke(mediaIndex);
            if (!started)
            {
                Console.WriteLine($"movie-frame={frame.Index} bytes={frame.Rgba.Length}");
                if (audioEnabled)
                {
                    audioOutput.AddSource(sound, audioOutput.Movie);
                    soundAttached = true;
                }
                Console.WriteLine($"movie-audio={(soundAttached ? "Playing" : "Muted")} pcm=44100Hz/stereo");
                if (!suspended)
                {
                    clock.Start();
                }
                started = true;
            }
            double seconds = mediaIndex / decoder.Information.FramesPerSecond;
            if (!PublishNativeFrames(seconds, decodedFrame))
            {
                break;
            }
        }
        if (started && !audioEnabled)
        {
            sound.AdvanceSilently(clock.Elapsed.TotalSeconds);
        }
        if (callbackActive && Finished && nativeInformation is not null)
        {
            // A media frame spans an interval. Admit a native boundary inside the final
            // interval even when no additional decoded image begins at that timestamp.
            PublishNativeFrames(Math.Max(decoder.Information.DurationSeconds, (nativeInformation.FrameCount - 1) / nativeInformation.FramesPerSecond), decodedFrame);
        }
    }

    private bool PublishNativeFrames(double seconds, Func<MoviePlaybackState, double, bool>? decodedFrame)
    {
        if (nativeTimeline is null || nativeInformation is null)
        {
            return true;
        }
        foreach (int frame in nativeTimeline.AdvanceTo(seconds))
        {
            callbackActive = decodedFrame?.Invoke(new MoviePlaybackState(frame, nativeInformation.FrameCount - 1, true, false, false), frame / nativeInformation.FramesPerSecond) != false;
            if (!callbackActive)
            {
                return false;
            }
        }
        return true;
    }

    public void Draw(SpriteBatch batch, Rectangle bounds)
    {
        PixelBounds fit = ViewportLayout.Fit(Texture.Width, Texture.Height, bounds.Width, bounds.Height);
        var target = new Rectangle(bounds.X + fit.X, bounds.Y + fit.Y, fit.Width, fit.Height);
        batch.Draw(Texture, target, Color.White);
        DrawSubtitles(batch, target);
    }

    private void DrawSubtitles(SpriteBatch batch, Rectangle videoBounds)
    {
        if (subtitleRenderer is not null && decodedMediaFrame >= 0)
        {
            subtitleRenderer.Draw(batch, videoBounds, decodedMediaFrame / decoder.Information.FramesPerSecond);
        }
    }

    public void DrawSubtitles(SpriteBatch batch, Rectangle target, PixelBounds content, Texture2D surface)
    {
        if (subtitleRenderer is null)
        {
            return;
        }
        double scaleX = (double)target.Width / surface.Width;
        double scaleY = (double)target.Height / surface.Height;
        int left = (int)Math.Round(content.X * scaleX);
        int top = (int)Math.Round(content.Y * scaleY);
        int right = (int)Math.Round((content.X + content.Width) * scaleX);
        int bottom = (int)Math.Round((content.Y + content.Height) * scaleY);
        DrawSubtitles(batch, new Rectangle(target.X + left, target.Y + top, right - left, bottom - top));
    }

    public void Dispose()
    {
        Console.WriteLine($"movie-dispose position={PositionSeconds:F3} video-eof={decoder.VideoFinished} audio-eof={decoder.AudioFinished}");
        if (soundAttached)
        {
            audioOutput.RemoveSource(sound);
            soundAttached = false;
        }
        decoder.Dispose();
        subtitleRenderer?.Dispose();
        Texture.Dispose();
    }
}
