using System.Diagnostics;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed class MoviePresentation : IDisposable
{
    private readonly FfmpegMovieDecoder decoder;
    private readonly DynamicSoundEffectInstance sound = new(44100, AudioChannels.Stereo);
    private readonly Stopwatch clock = new();
    private bool started;
    private readonly MovieFrameTimeline nativeTimeline;
    private readonly MovieInformation nativeInformation;
    private bool callbackActive = true;
    private byte[] decodedPixels = [];

    public MoviePresentation(GraphicsDevice device, string toolsDirectory, string path, double startSeconds, MovieInformation? information = null, bool audioEnabled = true, MovieInformation? nativeInformation = null)
    {
        sound.Volume = audioEnabled ? 1 : 0;
        decoder = new FfmpegMovieDecoder(toolsDirectory, path, startSeconds, information);
        Texture = new Texture2D(device, decoder.Information.Width, decoder.Information.Height);
        this.nativeInformation = nativeInformation ?? decoder.Information;
        nativeTimeline = new MovieFrameTimeline(this.nativeInformation, MovieFrameTimeline.FrameAt(startSeconds, this.nativeInformation));
    }

    public Texture2D Texture { get; }
    public ReadOnlySpan<byte> DecodedPixels => decodedPixels;
    public double PositionSeconds => decoder.StartSeconds + clock.Elapsed.TotalSeconds;
    public bool Finished => started && IsComplete(PositionSeconds, decoder.Information.DurationSeconds, decoder.VideoFinished, decoder.AudioFinished, sound.PendingBufferCount);
    public bool HasFrame => started;
    public void Suspend()
    {
        clock.Stop();
        sound.Pause();
    }

    public void Resume()
    {
        if (started)
        {
            clock.Start();
            sound.Resume();
        }
    }
    public string? Error => decoder.Error;
    public MoviePlaybackState Playback => new(nativeTimeline.Frame, nativeInformation.FrameCount - 1, !Finished, Finished, false);

    internal static bool IsComplete(double position, double duration, bool videoFinished, bool audioFinished, int pendingAudioBuffers)
    {
        return position >= duration && videoFinished && audioFinished && pendingAudioBuffers == 0;
    }

    public void Update(Func<MoviePlaybackState, double, bool>? decodedFrame = null, Action<int>? mediaFrame = null)
    {
        while (sound.PendingBufferCount < 3 && decoder.TryGetAudio(out byte[]? buffer))
        {
            sound.SubmitBuffer(buffer!);
        }
        while (decoder.TryGetNextFrame(clock.Elapsed.TotalSeconds, out MovieFrame? frame))
        {
            Texture.SetData(frame!.Rgba);
            decodedPixels = frame.Rgba;
            int mediaIndex = Math.Min(decoder.Information.FrameCount - 1, MovieClock.AbsoluteFrame(decoder.StartSeconds, decoder.Information) + checked((int)frame.Index));
            mediaFrame?.Invoke(mediaIndex);
            if (!started)
            {
                Console.WriteLine($"movie-frame={frame.Index} bytes={frame.Rgba.Length}");
                sound.Play();
                Console.WriteLine($"movie-audio={sound.State} buffered={sound.PendingBufferCount} pcm=44100Hz/stereo");
                clock.Start();
                started = true;
            }
            double seconds = mediaIndex / decoder.Information.FramesPerSecond;
            if (!PublishNativeFrames(seconds, decodedFrame))
            {
                break;
            }
        }
        if (callbackActive && Finished)
        {
            // A media frame spans an interval. Admit a native boundary inside the final
            // interval even when no additional decoded image begins at that timestamp.
            PublishNativeFrames(Math.Max(decoder.Information.DurationSeconds, (nativeInformation.FrameCount - 1) / nativeInformation.FramesPerSecond), decodedFrame);
        }
    }

    private bool PublishNativeFrames(double seconds, Func<MoviePlaybackState, double, bool>? decodedFrame)
    {
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
    }

    public void Dispose()
    {
        Console.WriteLine($"movie-dispose position={PositionSeconds:F3} video-eof={decoder.VideoFinished} audio-eof={decoder.AudioFinished} pending-audio={sound.PendingBufferCount} state={sound.State}");
        sound.Stop();
        sound.Dispose();
        decoder.Dispose();
        Texture.Dispose();
    }
}
