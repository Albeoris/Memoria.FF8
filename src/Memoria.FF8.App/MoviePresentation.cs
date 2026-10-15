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
    private int absoluteFrame;
    private byte[] decodedPixels = [];

    public MoviePresentation(GraphicsDevice device, string toolsDirectory, string path, double startSeconds, MovieInformation? information = null, bool audioEnabled = true)
    {
        sound.Volume = audioEnabled ? 1 : 0;
        decoder = new FfmpegMovieDecoder(toolsDirectory, path, startSeconds, information);
        Texture = new Texture2D(device, decoder.Information.Width, decoder.Information.Height);
        absoluteFrame = MovieClock.AbsoluteFrame(startSeconds, decoder.Information);
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
    public MoviePlaybackState Playback => new(absoluteFrame, decoder.Information.FrameCount - 1, !Finished, Finished, false);

    internal static bool IsComplete(double position, double duration, bool videoFinished, bool audioFinished, int pendingAudioBuffers)
    {
        return position >= duration && videoFinished && audioFinished && pendingAudioBuffers == 0;
    }

    public void Update(Func<MoviePlaybackState, double, bool>? decodedFrame = null)
    {
        while (sound.PendingBufferCount < 3 && decoder.TryGetAudio(out byte[]? buffer))
        {
            sound.SubmitBuffer(buffer!);
        }
        while (decoder.TryGetNextFrame(clock.Elapsed.TotalSeconds, out MovieFrame? frame))
        {
            Texture.SetData(frame!.Rgba);
            decodedPixels = frame.Rgba;
            absoluteFrame = Math.Min(decoder.Information.FrameCount - 1, MovieClock.AbsoluteFrame(decoder.StartSeconds, decoder.Information) + checked((int)frame.Index));
            if (!started)
            {
                Console.WriteLine($"movie-frame={frame.Index} bytes={frame.Rgba.Length}");
                sound.Play();
                Console.WriteLine($"movie-audio={sound.State} buffered={sound.PendingBufferCount} pcm=44100Hz/stereo");
                clock.Start();
                started = true;
            }
            double seconds = decoder.StartSeconds + frame.Index / decoder.Information.FramesPerSecond;
            if (decodedFrame?.Invoke(new MoviePlaybackState(absoluteFrame, decoder.Information.FrameCount - 1, true, false, false), seconds) == false)
            {
                break;
            }
        }
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
