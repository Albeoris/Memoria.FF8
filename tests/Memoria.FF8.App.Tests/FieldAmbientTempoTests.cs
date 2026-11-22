using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldAmbientTempoTests
{
    [Fact]
    public void Restored_cockpit_loop_has_identical_pcm_and_cursor_when_fast_forward_is_toggled()
    {
        string source = TestResources.FixturePath("App.Tests.FieldAmbientTempoTests.1");
        string directory = TestResources.GameDirectory;
        var session = new GameSession(PlayerStateLoader.Load(source, directory));
        using var world = new GameWorld(session, directory);
        Assert.True(world.EnsureField());
        Assert.Equal(827, world.Events.FieldId);
        GameplaySaveAudioPosition saved = Assert.Single(session.State.AudioPresentation!.Ambient, voice => voice.EffectId == 129).Position;

        PcmMixer normalMixer = new();
        PcmMixer fastMixer = new();
        using FieldAudioPresentation normal = new(directory, "") { OutputFactory = () => new(normalMixer) };
        using FieldAudioPresentation fast = new(directory, "") { OutputFactory = () => new(fastMixer) };
        foreach (FieldAudioPresentation presentation in new[] { normal, fast })
        {
            presentation.Attach(world.Events);
            presentation.SetPlaybackResumePositions(session.State.AudioPresentation);
        }
        world.Events.ResumeGameplayLoopSounds();
        Assert.Equal(saved, Assert.Single(fast.CapturePlaybackPositions()!.Ambient).Position);

        GameplayClock clock = new();
        short[] expected = new short[44100 * 2];
        short[] actual = new short[expected.Length];
        for (int segment = 0; segment < 3; segment++)
        {
            clock.ToggleSpeed();
            fast.SetSoundSpeed(clock.EffectiveSpeed(SceneKind.Field));
            normalMixer.Render(expected);
            fastMixer.Render(actual);
            Assert.Contains(expected, sample => sample != 0);
            Assert.True(expected.AsSpan().SequenceEqual(actual), $"The cockpit waveform changed at {clock.SpeedMultiplier}x.");
            Assert.Equal(Assert.Single(normal.CapturePlaybackPositions()!.Ambient), Assert.Single(fast.CapturePlaybackPositions()!.Ambient));
        }
        Assert.Null(normal.Error);
        Assert.Null(fast.Error);
    }

    [Fact]
    public void Newly_started_field_loop_enters_repeating_region_at_wall_clock_time()
    {
        string directory = TestResources.GameDirectory;
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory, 129))!;
        int delay = resource.LoopPolicy.LoopTransitionDelayMilliseconds!.Value;
        var clock = new ManualClock();
        PcmMixer mixer = new();
        using FieldAudioPresentation audio = new(directory, "") { OutputFactory = () => new(mixer), SoundTimeProvider = clock };
        audio.SetSoundSpeed(3);
        audio.Execute(new GlobalSoundRequest(129, 8, 8, 4, 128, 64, 127, 100, resource));
        mixer.Render(new short[400]);
        GameplaySaveAudioPosition intro = Assert.Single(audio.CapturePresentationSounds(0)).Position;
        Assert.Equal(100, intro.Position);
        Assert.Equal(0, intro.StartPoint);

        clock.Advance(delay - 1);
        audio.Update();
        mixer.Render(new short[4]);
        Assert.Equal(0, Assert.Single(audio.CapturePresentationSounds(0)).Position.StartPoint);
        clock.Advance(1);
        audio.Update();
        mixer.Render(new short[4]);
        GameplaySaveAudioPosition loop = Assert.Single(audio.CapturePresentationSounds(0)).Position;
        Assert.Equal(1, loop.StartPoint);
        Assert.Equal(loop.LoopStart + 1, loop.Position);
        Assert.Null(audio.Error);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;

        public void Advance(int milliseconds)
        {
            timestamp += milliseconds;
        }
    }
}
