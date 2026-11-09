using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSoundFrequencyRampTests
{
    public static void Run()
    {
        Ff7FieldResources field = new("frequency-ramp-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Tick = 1520 });
        host.ConfigureNativeSoundResources(id => new(id == 59 ? 22050 : 44100, id == 59, id == 60));
        host.NativeSoundLoopAlreadyPlaying = (_, _) => false;
        host.Execute(Request(1, 0xB0, 208));
        FieldHostRequest ramp = Request(2, 0xB4, 64, 104);
        host.Execute(ramp);
        Equal(142, host.Process.SoundChannelFrequencyFades[0]!.RatePerSecond);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(30));
        Equal(-48, host.Process.SoundChannelPitches[0]);
        Equal(1L, host.Process.SoundChannelFrequencyVersions[0]);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(7));
        Equal(-48, host.Process.SoundChannelPitches[0]);
        Equal(2L, host.Process.SoundChannelFrequencyVersions[0]);
        Equal(7, host.Process.SoundChannelFrequencyFades[0]!.AccumulatedMilliseconds);
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ConfigureNativeSoundResources(id => new(id == 59 ? 22050 : 44100, id == 59, id == 60));
        cold.NativeSoundLoopAlreadyPlaying = (_, _) => false;
        cold.Execute(ramp);
        foreach (Ff7FieldHost current in new[] { host, cold })
        {
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
            Equal(-47, current.Process.SoundChannelPitches[0]);
            Equal(0, current.Process.SoundChannelFrequencyFades[0]!.AccumulatedMilliseconds);
            current.Execute(Request(3, 0xB4, 255, 209));
            Equal(104, current.Process.SoundChannelFrequencyFades[0]!.Target);
            current.Execute(Request(4, 0x20, 64, 60));
            Equal(104, current.Process.SoundChannelFrequencyFades[0]!.Target);
            current.Execute(Request(5, 0x20, 64, 0));
            Equal(104, current.Process.SoundChannelFrequencyFades[0]!.Target);
            current.Execute(Request(6, 0x20, 64, 59));
            Equal(22050, current.Process.SoundChannelBaseFrequencies[0]);
            current.AdvanceAudioClock(TimeSpan.FromSeconds(2));
            Equal(104, current.Process.SoundChannelPitches[0]);
            Equal(true, current.Process.SoundChannelFrequencyFades[0] is null);
            current.Execute(Request(7, 0xB4, 256, 384));
            Equal(-128, current.Process.SoundChannelPitches[0]);
            current.Execute(Request(8, 0xB4, 255, 129));
            Equal(0, current.Process.SoundChannelFrequencyFades[0]!.RatePerSecond);
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(30));
            current.AdvanceAudioClock(TimeSpan.FromSeconds(100));
            Equal(-128, current.Process.SoundChannelPitches[0]);
            Equal(false, current.Process.SoundChannelStoppedByFade[0]);
            current.Execute(Request(9, 0xB0, 0));
            Equal(true, current.Process.SoundChannelFrequencyFades[0] is null);
            current.Execute(Request(10, 0xB4, 64, 104));
            string beforePause = JsonSerializer.Serialize(current.Capture());
            current.AdvanceAudioClock(TimeSpan.FromSeconds(10), true);
            Equal(beforePause, JsonSerializer.Serialize(current.Capture()));
            Equal(1520L, current.Process.Tick);
        }
        Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Ff7FieldProcess next = host.CreateNextFieldProcess();
        Equal(104, next.SoundChannelFrequencyFades[0]!.Target);
        Equal(host.Process.SoundChannelFrequencyVersions[0], next.SoundChannelFrequencyVersions[0]);
        for (int channel = 2; channel <= 4; channel++)
        {
            Ff7FieldHost selected = new(field, (_, _) => 1, new() { Tick = 1695 });
            selected.ConfigureNativeSoundResources(_ => new(44100, false));
            selected.Execute(Request(1, 0xB0 + channel - 1, 208));
            selected.Execute(Request(2, 0xB4 + channel - 1, 64, 104));
            Equal(-48, selected.Process.SoundChannelPitches[channel - 1]);
            Equal(142, selected.Process.SoundChannelFrequencyFades[channel - 1]!.RatePerSecond);
            selected.AdvanceAudioClock(TimeSpan.FromMilliseconds(30));
            Ff7FieldHost restored = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(selected.Capture()))!);
            foreach (Ff7FieldHost current in new[] { selected, restored })
            {
                current.AdvanceAudioClock(TimeSpan.FromMilliseconds(8));
                Equal(-47, current.Process.SoundChannelPitches[channel - 1]);
                Equal(2L, current.Process.SoundChannelFrequencyVersions[channel - 1]);
                for (int other = 0; other < 5; other++)
                {
                    if (other != channel - 1)
                    {
                        Equal(0, current.Process.SoundChannelPitches[other]);
                        Equal(0L, current.Process.SoundChannelFrequencyVersions[other]);
                    }
                }
            }
            Equal(JsonSerializer.Serialize(selected.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }

    }

    private static FieldHostRequest Request(long id, int operation, int p0, int p1 = 0) => new(id, 0, 7, 16, new FieldAudioCommand(operation, p0, p1));

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
