using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Runtime.InteropServices;

namespace Memoria.FF8.App.Tests;

public sealed class SpuWaveAudioOutputTests
{
    [Fact]
    public void ClassicCueUsesARelativeResourceAndRejectsEscapingPaths()
    {
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.Equal("Data/Music/eyes_on_me.wav", resource.RelativePath);
        resource.Validate();
        Assert.Throws<InvalidDataException>(() => new MusicCueResource(MusicCue.EyesOnMe, "../eyes_on_me.wav").Validate());
        Assert.Throws<InvalidDataException>(() => new MusicCueResource(MusicCue.EyesOnMe, Path.GetFullPath("eyes_on_me.wav")).Validate());
    }

    [Fact]
    public void InstalledCueDecodesWithoutClaimingPlaybackForMutedOutput()
    {
        string game = TestResources.GameDirectory;
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.SkipUnless(File.Exists(Path.Combine(game, resource.RelativePath)), "The installed EyesOnMe resource is required.");
        MovieClock tools = new(new GameSession(new GameState()), game);
        using SpuWaveAudioOutput output = new(game, tools.ToolsDirectory, resource, 0, false);
        NativeSpuAudioAttempt receipt = output.Prepare();
        Assert.Equal("EyesOnMe", receipt.ResourceId);
        Assert.Equal(64, receipt.SourceSha256.Length);
        Assert.Equal(44100, receipt.SampleRate);
        Assert.Equal(2, receipt.Channels);
        Assert.Equal(59914848, receipt.PcmBytes);
        Assert.False(receipt.PlaybackStarted);
        Assert.False(output.HasLiveAudio);
        output.Stop();
        Assert.False(output.HasLiveAudio);
    }

    [Fact]
    public void InstalledCueStartsAndStopsExistingWindowsAudioOutput()
    {
        string game = TestResources.GameDirectory;
        MusicCueResource resource = new ClassicMusicCueResolver().Resolve(MusicCue.EyesOnMe);
        Assert.SkipUnless(File.Exists(Path.Combine(game, resource.RelativePath)), "The installed EyesOnMe resource is required.");
        MovieClock tools = new(new GameSession(new GameState()), game);
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using SpuWaveAudioOutput output = new(game, tools.ToolsDirectory, resource, GetDesktopWindow(), true);
                NativeSpuAudioAttempt receipt = output.Prepare();
                Assert.True(receipt.PlaybackStarted);
                Assert.True(output.HasLiveAudio);
                output.Stop();
                Assert.False(output.HasLiveAudio);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "EyesOnMe start/stop did not finish.");
        Assert.Null(failure);
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
