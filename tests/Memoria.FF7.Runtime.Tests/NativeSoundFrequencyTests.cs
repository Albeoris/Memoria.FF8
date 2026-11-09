using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSoundFrequencyTests
{
    public static void Run()
    {
        Equal(35832, Ff7NativeSoundFrequency.Frequency(44100, -48));
        Equal(17916, Ff7NativeSoundFrequency.Frequency(22050, -48));
        Equal(22050, Ff7NativeSoundFrequency.Frequency(44100, -128));
        Equal(88200, Ff7NativeSoundFrequency.Frequency(44100, 127));
        Equal(966u, Ff7NativeSoundFrequency.LoopDelayMilliseconds(85180, 44100));
        Equal(false, Ff7NativeSoundFrequency.Accepted(99));
        Equal(true, Ff7NativeSoundFrequency.Accepted(100));
        Equal(true, Ff7NativeSoundFrequency.Accepted(100000));
        Equal(false, Ff7NativeSoundFrequency.Accepted(100001));
        Ff7FieldResources field = new("frequency-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Tick = 1420, NativeMusicTrack = 8 });
        Ff7NativeSoundDescription Describe(int sound) => sound switch
        {
            60 => new(22050, false, true),
            59 => new(32000, true),
            _ => new(44100, false)
        };
        host.ConfigureNativeSoundResources(Describe, restoring: true);
        Equal(1420L, host.Process.Tick);
        Equal("44100,44100,44100,44100,44100", string.Join(',', host.Process.SoundChannelBaseFrequencies));
        host.Execute(Request(1, new FieldAudioCommand(0x20, 64, 60)));
        Equal(22050, host.Process.SoundChannelBaseFrequencies[0]);
        host.Execute(Request(2, new FieldAudioCommand(0x20, 64, 59)));
        Equal(32000, host.Process.SoundChannelBaseFrequencies[0]);
        host.NativeSoundLoopAlreadyPlaying = (_, _) => true;
        host.Execute(Request(3, new FieldAudioCommand(0x20, 64, 60)));
        Equal(32000, host.Process.SoundChannelBaseFrequencies[0]);
        FieldHostRequest b0 = Request(4, new FieldAudioCommand(0xB0, 208));
        host.Execute(b0);
        Equal(-48, host.Process.SoundChannelPitches[0]);
        Equal("-48,0,0,0,0", string.Join(',', host.Process.SoundChannelPitches));
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ConfigureNativeSoundResources(Describe);
        Equal(true, cold.Execute(b0).Completed);
        Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        cold.NativeSoundLoopAlreadyPlaying = (_, _) => false;
        cold.Execute(Request(5, new FieldAudioCommand(0x20, 64, 60)));
        Equal(22050, cold.Process.SoundChannelBaseFrequencies[0]);
        Equal(-48, cold.Process.SoundChannelPitches[0]);
        cold.Execute(Request(6, new FieldAudioCommand(0x20, 64, 0)));
        Equal(22050, cold.Process.SoundChannelBaseFrequencies[0]);
        Equal(-48, cold.Process.SoundChannelPitches[0]);
        cold.Execute(Request(7, new FieldAudioCommand(0x2B, 64, 59)));
        Equal(32000, cold.Process.SoundChannelBaseFrequencies[0]);
        Equal(44100, cold.Process.SoundChannelBaseFrequencies[3]);
        Ff7FieldProcess transitioned = cold.CreateNextFieldProcess();
        Equal(-48, transitioned.SoundChannelPitches[0]);
        Equal(32000, transitioned.SoundChannelBaseFrequencies[0]);
        Equal(8, transitioned.NativeMusicTrack!.Value);
        int callbacks = 0;
        cold.NativeSoundClockAdvanced = (_, paused) => callbacks += paused ? 10 : 1;
        cold.AdvanceAudioClock(TimeSpan.FromMilliseconds(10));
        cold.AdvanceAudioClock(TimeSpan.FromMilliseconds(10), true);
        Equal(11, callbacks);
        cold.Execute(Request(8, new FieldAudioCommand(0xB0, 464)));
        Equal(-48, cold.Process.SoundChannelPitches[0]);
        cold.Execute(Request(9, new FieldAudioCommand(0xB0, 255)));
        Equal(-1, cold.Process.SoundChannelPitches[0]);
        cold.Execute(Request(10, new FieldAudioCommand(0xB0, 128)));
        Equal(-128, cold.Process.SoundChannelPitches[0]);
    }

    private static FieldHostRequest Request(long id, FieldCommand command) => new(id, 0, 7, 16, command);

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
