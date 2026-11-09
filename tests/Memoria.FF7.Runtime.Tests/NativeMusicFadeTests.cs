using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeMusicFadeTests
{
    public static void Run()
    {
        Ff7FieldHost host = Host();
        host.Execute(Request(1, new FieldAudioCommand(0xC2, 127, 60, 0)));
        Equal(60, host.Process.MusicVolume);
        Equal(-28, host.Process.MusicVolumeFade!.RatePerSecond);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(35));
        Equal(60, host.Process.MusicVolume);
        Equal(35, host.Process.MusicVolumeFade!.AccumulatedMilliseconds);
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
        cold.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
        Equal(59, host.Process.MusicVolume);
        Equal(0, host.Process.MusicVolumeFade!.AccumulatedMilliseconds);
        Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        string paused = JsonSerializer.Serialize(host.Capture());
        host.AdvanceAudioClock(TimeSpan.FromSeconds(20), true);
        Equal(paused, JsonSerializer.Serialize(host.Capture()));
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(500));
        Equal(45, host.Process.MusicVolume);
        Ff7FieldHost nextField = Host(host.CreateNextFieldProcess());
        Equal(host.Process.MusicVolumeFade, nextField.Process.MusicVolumeFade);
        nextField.AdvanceAudioClock(TimeSpan.FromSeconds(2));
        Equal(0, nextField.Process.MusicVolume);
        Equal(true, nextField.Process.MusicVolumeFade is null);

        Ff7FieldHost fractional = Host();
        fractional.Execute(Request(2, new FieldAudioCommand(0xC2, 60, 60, 0)));
        for (int tick = 0; tick < 3; tick++)
        {
            fractional.AdvanceAudioClock(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30));
        }
        Equal(57, fractional.Process.MusicVolume);
        Equal(9999, fractional.Process.MusicVolumeFade!.SubmillisecondTicks);
        fractional.Execute(Request(3, new FieldAudioCommand(0xC0, 255)));
        Equal(57, fractional.Process.MusicVolume);
        Equal(true, fractional.Process.MusicVolumeFade is not null);
        fractional.Execute(Request(4, new FieldAudioCommand(0xC0, 127)));
        Equal(127, fractional.Process.MusicVolume);
        Equal(true, fractional.Process.MusicVolumeFade is null);
        Ff7FieldHost channels = Host();
        channels.Execute(Request(7, new FieldAudioCommand(0xA1, 0)));
        Equal("127,0,127,127,127", string.Join(',', channels.Process.SoundChannelVolumes));
        Ff7FieldHost restoredChannels = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(channels.Capture()))!);
        Equal("127,0,127,127,127", string.Join(',', restoredChannels.CreateNextFieldProcess().SoundChannelVolumes));
        restoredChannels.Execute(Request(8, new FieldAudioCommand(0xA1, 511)));
        Equal(127, restoredChannels.Process.SoundChannelVolumes[1]);
        restoredChannels.Execute(Request(20, new FieldAudioCommand(0xA6, 0, 10)));
        Equal("127,127,10,127,127", string.Join(',', restoredChannels.Process.SoundChannelVolumes));
        Ff7FieldHost coldImmediate = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(restoredChannels.Capture()))!);
        Equal("127,127,10,127,127", string.Join(',', coldImmediate.CreateNextFieldProcess().SoundChannelVolumes));
        coldImmediate.AdvanceAudioClock(TimeSpan.FromSeconds(10));
        Equal(10, coldImmediate.Process.SoundChannelVolumes[2]);
        coldImmediate.Execute(Request(21, new FieldAudioCommand(0xA6, 256, 511)));
        Equal(127, coldImmediate.Process.SoundChannelVolumes[2]);
        Ff7FieldHost changing = Host();
        changing.Execute(Request(9, new FieldAudioCommand(0xC2, 127, 60, 0)));
        Ff7MusicVolumeFade originalFade = changing.Process.MusicVolumeFade!;
        changing.Execute(Request(10, new FieldAudioCommand(0xC1, 127, 60)));
        Equal(originalFade, changing.Process.MusicVolumeFade);
        changing.Execute(Request(11, new FieldAudioCommand(0xC1, 60, 127)));
        Equal(67, changing.Process.MusicVolumeFade!.RatePerSecond);
        changing.AdvanceAudioClock(TimeSpan.FromMilliseconds(100));
        Equal(66, changing.Process.MusicVolume);
        changing.Execute(Request(12, new FieldAudioCommand(0xC1, 0, 127)));
        Equal(127, changing.Process.MusicVolume);
        Equal(true, changing.Process.MusicVolumeFade is null);
        channels.Execute(Request(13, new FieldAudioCommand(0xA0, 0)));
        Equal("0,0,127,127,127", string.Join(',', channels.Process.SoundChannelVolumes));
        channels.Execute(Request(14, new FieldAudioCommand(0xA0, 255)));
        Equal("127,0,127,127,127", string.Join(',', channels.Process.SoundChannelVolumes));
        channels.Execute(Request(15, new FieldAudioCommand(0xA2, 64)));
        Equal("127,0,64,127,127", string.Join(',', channels.Process.SoundChannelVolumes));
        Equal(64, Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(channels.Capture()))!).CreateNextFieldProcess().SoundChannelVolumes[2]);
        channels.Execute(Request(16, new FieldAudioCommand(0x2A, 64, 0)));
        Equal("127,0,127,127,127", string.Join(',', channels.Process.SoundChannelVolumes));
        Ff7FieldHost fourth = Host(new() { NativeMusicTrack = 8, MusicTicks = 54, SoundChannelVolumes = [11, 22, 33, 44, 55] });
        FieldHostRequest fourthPlay = Request(22, new FieldAudioCommand(0x2B, 64, 60));
        fourth.Execute(fourthPlay);
        Equal("11,22,33,44,55", string.Join(',', fourth.Process.SoundChannelVolumes));
        Ff7FieldHost coldFourth = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(fourth.Capture()))!);
        Equal(true, coldFourth.Execute(fourthPlay).Completed);
        Equal("11,22,33,44,55", string.Join(',', coldFourth.CreateNextFieldProcess().SoundChannelVolumes));
        coldFourth.Execute(Request(23, new FieldAudioCommand(0x2B, 64, 0)));
        Equal("11,22,33,127,55", string.Join(',', coldFourth.Process.SoundChannelVolumes));
        Equal(8, coldFourth.Process.NativeMusicTrack!.Value);
        Equal(54L, coldFourth.Process.MusicTicks);
        fractional.Execute(Request(5, new FieldAudioCommand(0xC2, 0, 255, 17)));
        Equal(17, fractional.Process.MusicVolume);
        fractional.Execute(Request(6, new FieldAudioCommand(0xC2, 60, 200, 0)));
        Equal(17, fractional.Process.MusicVolume);
        Equal(true, fractional.Process.MusicVolumeFade is null);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        Ff7FieldResources field = new("audio-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
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
