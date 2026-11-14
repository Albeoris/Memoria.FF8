using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NativeMusicResumeTests
{
    [Fact]
    public void OldFiniteLoopUsesNativeAgeAndDoesNotReplayWrappedAudioBeyondNativeEnd()
    {
        Ff7NativeMusicTimeline finite = new(480, 43_200L * 352_941, false);
        Ff7MusicVoicePosition wrapped = new(22, new string('A', 64), 44100, 2, 100, 0, 1_500_000, true);
        Assert.Equal(44100, Ff7Scene.ResolveNativeMusicResumeFrames(wrapped, finite, 30, 44100, 1_500_000, false));
        Assert.Equal(1_500_000, Ff7Scene.ResolveNativeMusicResumeFrames(wrapped, finite, 953, 44100, 1_500_000, false));
        Assert.Equal(1_500_000, Ff7Scene.ResolveNativeMusicResumeFrames(wrapped, finite, 3000, 44100, 1_500_000, false));
    }

    [Fact]
    public void OrdinaryFiniteAndLoopingCapturesKeepTheirExactSavedVoicePositions()
    {
        Ff7NativeMusicTimeline finite = new(480, 43_200L * 352_941, false);
        Ff7MusicVoicePosition saved = new(22, new string('A', 64), 44100, 2, 12345, 0, 1_500_000, false);
        Assert.Equal(12345, Ff7Scene.ResolveNativeMusicResumeFrames(saved, finite, 900, 44100, 1_500_000, false));
        Assert.Equal(12345, Ff7Scene.ResolveNativeMusicResumeFrames(saved with { LoopStarted = true }, finite with { Loops = true }, 900, 44100, 1_500_000, false));
        Assert.Equal(44100, Ff7Scene.ResolveNativeMusicResumeFrames(null, finite, 30, 44100, 1_500_000, true));
        Assert.Equal(0, Ff7Scene.ResolveNativeMusicResumeFrames(null, finite, 30, 44100, 1_500_000, false));
    }
}
