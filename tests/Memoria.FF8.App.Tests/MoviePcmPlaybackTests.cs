using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF8.Audio;

namespace Memoria.FF8.App.Tests;

public sealed class MoviePcmPlaybackTests
{
    [Fact]
    public void PausePreservesTheNextFrameAndCompletionWaitsForQueuedMovieAudio()
    {
        Queue<byte[]> chunks = new([[1, 0, 2, 0, 3, 0, 4, 0]]);
        MoviePcmPlayback movie = new(() => chunks.TryDequeue(out byte[]? chunk) ? chunk : null);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        output.AddSource(movie, output.Movie);
        short[] frame = new short[2];

        mixer.Render(frame);
        Assert.Equal(new short[] { 1, 2 }, frame);
        movie.Pause(true);
        mixer.Render(frame);
        Assert.Equal(new short[] { 0, 0 }, frame);
        Assert.True(movie.HasPendingAudio(0));
        movie.Pause(false);
        mixer.Render(frame);
        Assert.Equal(new short[] { 3, 4 }, frame);
        Assert.True(movie.HasPendingAudio(1));
        mixer.Render(frame);
        Assert.False(movie.HasPendingAudio(1));

        output.RemoveSource(movie);
    }

    [Fact]
    public void ResumingBeforeFirstImageClearsTheAudioPause()
    {
        Queue<byte[]> chunks = new([[1, 0, 2, 0]]);
        MoviePcmPlayback sound = new(() => chunks.TryDequeue(out byte[]? chunk) ? chunk : null);
        MoviePresentation presentation = (MoviePresentation)RuntimeHelpers.GetUninitializedObject(typeof(MoviePresentation));
        Stopwatch clock = new();
        typeof(MoviePresentation).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(presentation, sound);
        typeof(MoviePresentation).GetField("clock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(presentation, clock);

        presentation.Suspend();
        sound.RenderFrame(out double left, out double right);
        Assert.Equal((0d, 0d), (left, right));
        presentation.Resume();
        Assert.False(clock.IsRunning);
        sound.RenderFrame(out left, out right);
        Assert.Equal((1d, 2d), (left, right));
    }

    [Fact]
    public void MovieBusGainLeavesOtherAudioAndPlaybackPositionsIndependent()
    {
        Queue<byte[]> chunks = new([[10, 0, 20, 0, 30, 0, 40, 0]]);
        MoviePcmPlayback movie = new(() => chunks.TryDequeue(out byte[]? chunk) ? chunk : null);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer effect = output.Create([100, 0], 44100, 1);
        effect.Play(true);
        output.AddSource(movie, output.Movie);
        short[] frame = new short[2];

        output.Movie.Gain = 0;
        mixer.Render(frame);
        Assert.Equal(new short[] { 100, 100 }, frame);
        output.Movie.Gain = 0.5;
        mixer.Render(frame);
        Assert.Equal(new short[] { 115, 120 }, frame);
        Assert.False(movie.HasPendingAudio(0));
    }

    [Fact]
    public void MutedPlaybackDrainsAudioAgainstTheMovieClockWithoutADevice()
    {
        Queue<byte[]> chunks = new([new byte[44100 * 4]]);
        MoviePcmPlayback movie = new(() => chunks.TryDequeue(out byte[]? chunk) ? chunk : null);

        movie.AdvanceSilently(0.5);
        Assert.Empty(chunks);
        Assert.True(movie.HasPendingAudio(0));
        movie.Pause(true);
        movie.AdvanceSilently(1);
        Assert.True(movie.HasPendingAudio(0));
        movie.Pause(false);
        movie.AdvanceSilently(1);
        Assert.False(movie.HasPendingAudio(0));
    }
}
