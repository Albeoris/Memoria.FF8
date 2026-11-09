using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSoundPanTests
{
    public static void Run()
    {
        Ff7FieldResources field = new("pan-fixture", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1, new() { Tick = 1520 });
        host.ConfigureNativeSoundResources(id => new(44100, id == 59, id == 60));
        host.NativeSoundLoopAlreadyPlaying = (_, _) => false;
        FieldHostRequest request = Request(1, 0xAC, 64, 104);
        host.Execute(request);
        Equal(37, host.Process.SoundChannelPanFades[0]!.RatePerSecond);
        Equal(0L, host.Process.SoundChannelPanVersions[0]);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(100));
        Equal(64, host.Process.SoundChannelPans[0]);
        Equal(0L, host.Process.SoundChannelPanVersions[0]);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(20));
        Equal(64, host.Process.SoundChannelPans[0]);
        Equal(1L, host.Process.SoundChannelPanVersions[0]);
        Equal(20, host.Process.SoundChannelPanFades[0]!.AccumulatedMilliseconds);
        Ff7FieldHost cold = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ConfigureNativeSoundResources(id => new(44100, id == 59, id == 60));
        cold.NativeSoundLoopAlreadyPlaying = (_, _) => false;
        cold.Execute(request);
        foreach (Ff7FieldHost current in new[] { host, cold })
        {
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(8));
            Equal(65, current.Process.SoundChannelPans[0]);
            Equal(0, current.Process.SoundChannelPanFades[0]!.AccumulatedMilliseconds);
            current.Execute(Request(2, 0xAC, 255, 65));
            Equal(104, current.Process.SoundChannelPanFades[0]!.Target);
            current.Execute(Request(3, 0xAC, 0, 255));
            Equal(104, current.Process.SoundChannelPanFades[0]!.Target);
            current.AdvanceAudioClock(TimeSpan.FromSeconds(2));
            Equal(104, current.Process.SoundChannelPans[0]);
            Equal(true, current.Process.SoundChannelPanFades[0] is null);
            Equal(false, current.Process.SoundChannelStoppedByFade[0]);
            current.Execute(Request(4, 0xAC, 0, 0));
            Equal(0, current.Process.SoundChannelPans[0]);
            Equal(false, current.Process.SoundChannelStoppedByFade[0]);
            current.Execute(Request(5, 0xAC, 255, 1));
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(30));
            current.AdvanceAudioClock(TimeSpan.FromSeconds(100));
            Equal(0, current.Process.SoundChannelPans[0]);
            Equal(0, current.Process.SoundChannelPanFades[0]!.RatePerSecond);
            long version = current.Process.SoundChannelPanVersions[0];
            current.Execute(Request(6, 0x20, 100, 59));
            Equal(0, current.Process.SoundChannelPans[0]);
            Equal(version, current.Process.SoundChannelPanVersions[0]);
            current.Execute(Request(7, 0x20, 100, 60));
            Equal(100, current.Process.SoundChannelPans[0]);
            Equal(true, current.Process.SoundChannelPanFades[0] is null);
            current.NativeSoundLoopAlreadyPlaying = (_, _) => true;
            current.Execute(Request(8, 0xAC, 60, 120));
            current.Execute(Request(9, 0x20, 64, 60));
            Equal(100, current.Process.SoundChannelPans[0]);
            Equal(120, current.Process.SoundChannelPanFades[0]!.Target);
            current.Execute(Request(10, 0x20, 64, 0));
            Equal(120, current.Process.SoundChannelPanFades[0]!.Target);
            Equal(1520L, current.Process.Tick);
            current.AdvanceAudioClock(TimeSpan.FromSeconds(1), true);
            Equal(false, current.Process.SoundChannelPanFades[0]!.Started);
        }
        Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Ff7FieldProcess next = host.CreateNextFieldProcess();
        Equal(100, next.SoundChannelPans[0]);
        Equal(120, next.SoundChannelPanFades[0]!.Target);
        Equal(host.Process.SoundChannelPanVersions[0], next.SoundChannelPanVersions[0]);
        Equal(true, next.SoundPanStateInitialized);
        for (int channel = 2; channel <= 4; channel++)
        {
            Ff7FieldHost selected = new(field, (_, _) => 1, new() { Tick = 1867 });
            selected.ConfigureNativeSoundResources(_ => new(44100, false));
            selected.Execute(Request(1, 0xA8 + channel - 1, 64, 0));
            selected.Execute(Request(2, 0xAC + channel - 1, 48, 63));
            Equal(-1, selected.Process.SoundChannelPanFades[channel - 1]!.RatePerSecond);
            selected.AdvanceAudioClock(TimeSpan.FromMilliseconds(30));
            selected.AdvanceAudioClock(TimeSpan.FromMilliseconds(999));
            Equal(64, selected.Process.SoundChannelPans[channel - 1]);
            Equal(2L, selected.Process.SoundChannelPanVersions[channel - 1]);
            Ff7FieldHost restored = new(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(selected.Capture()))!);
            foreach (Ff7FieldHost current in new[] { selected, restored })
            {
                current.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
                Equal(63, current.Process.SoundChannelPans[channel - 1]);
                Equal(3L, current.Process.SoundChannelPanVersions[channel - 1]);
                Equal(true, current.Process.SoundChannelPanFades[channel - 1] is null);
                Equal(false, current.Process.SoundChannelStoppedByFade[channel - 1]);
                for (int other = 0; other < 5; other++)
                {
                    if (other != channel - 1)
                    {
                        Equal(64, current.Process.SoundChannelPans[other]);
                        Equal(0L, current.Process.SoundChannelPanVersions[other]);
                    }
                }
            }
            Equal(JsonSerializer.Serialize(selected.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }

    }

    private static FieldHostRequest Request(long id, int operation, int p0, int p1) => new(id, 0, 7, 16, new FieldAudioCommand(operation, p0, p1));

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
