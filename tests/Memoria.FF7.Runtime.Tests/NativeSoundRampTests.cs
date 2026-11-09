using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSoundRampTests
{
    public static void Run()
    {
        Ff7FieldHost host = Host(new() { NativeMusicTrack = 8, MusicTicks = 54 });
        for (int channel = 0; channel < 4; channel++)
        {
            host.Execute(Request(channel + 1, new FieldAudioCommand(0xA4 + channel, 255, 0)));
            Equal(-29, host.Process.SoundChannelVolumeFades[channel]!.RatePerSecond);
        }
        host.AdvanceAudioClock(TimeSpan.FromSeconds(1));
        Equal("127,127,127,127,127", string.Join(',', host.Process.SoundChannelVolumes));
        Equal(true, host.Process.SoundChannelVolumeFades.Take(4).All(fade => fade!.Started));
        host.AdvanceAudioClock(TimeSpan.FromMilliseconds(34));
        Equal(127, host.Process.SoundChannelVolumes[0]);
        Equal(34, host.Process.SoundChannelVolumeFades[0]!.AccumulatedMilliseconds);
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        foreach (Ff7FieldHost current in new[] { host, cold })
        {
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
            Equal("126,126,126,126,127", string.Join(',', current.Process.SoundChannelVolumes));
            Equal(0, current.Process.SoundChannelVolumeFades[0]!.AccumulatedMilliseconds);
            string beforePause = JsonSerializer.Serialize(current.Capture());
            current.AdvanceAudioClock(TimeSpan.FromSeconds(10), true);
            Equal(beforePause, JsonSerializer.Serialize(current.Capture()));
            for (int update = 0; update < 125; update++)
            {
                current.AdvanceAudioClock(TimeSpan.FromMilliseconds(35));
            }
            Equal(1, current.Process.SoundChannelVolumes[0]);
            Equal(false, current.Process.SoundChannelStoppedByFade.Any(value => value));
            current.AdvanceAudioClock(TimeSpan.FromMilliseconds(35));
            Equal("0,0,0,0,127", string.Join(',', current.Process.SoundChannelVolumes));
            Equal(true, current.Process.SoundChannelStoppedByFade.Take(4).All(value => value));
            Equal(true, current.Process.SoundChannelVolumeFades.All(fade => fade is null));
            Equal(8, current.Process.NativeMusicTrack!.Value);
            Equal(54L, current.Process.MusicTicks);
        }
        Equal(JsonSerializer.Serialize(host.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Ff7FieldProcess transition = cold.CreateNextFieldProcess();
        Equal("True,True,True,True,False", string.Join(',', transition.SoundChannelStoppedByFade));
        bool stoppedBeforePlayback = false;
        cold.AudioRequested = _ => stoppedBeforePlayback = cold.Process.SoundChannelStoppedByFade[3];
        cold.Execute(Request(10, new FieldAudioCommand(0x2B, 64, 60)));
        Equal(true, stoppedBeforePlayback);
        Equal("True,True,True,False,False", string.Join(',', cold.Process.SoundChannelStoppedByFade));
        Equal(0, cold.Process.SoundChannelVolumes[3]);
        cold.Execute(Request(19, new FieldAudioCommand(0xA7, 255, 127)));
        Equal(true, cold.Process.SoundChannelVolumeFades[3] is not null);
        cold.Execute(Request(20, new FieldAudioCommand(0xA3, 127)));
        Equal("0,0,0,127,127", string.Join(',', cold.Process.SoundChannelVolumes));
        Equal(true, cold.Process.SoundChannelVolumeFades[3] is null);
        Equal("True,True,True,False,False", string.Join(',', cold.Process.SoundChannelStoppedByFade));
        cold.Execute(Request(21, new FieldAudioCommand(0xA3, 256)));
        Equal(0, cold.Process.SoundChannelVolumes[3]);
        Equal(false, cold.Process.SoundChannelStoppedByFade[3]);
        cold.Execute(Request(22, new FieldAudioCommand(0xA3, 511)));
        Equal(127, cold.Process.SoundChannelVolumes[3]);

        Ff7FieldHost retarget = Host();
        retarget.Execute(Request(11, new FieldAudioCommand(0xA4, 255, 0)));
        Ff7SoundVolumeFade pending = retarget.Process.SoundChannelVolumeFades[0]!;
        retarget.Execute(Request(12, new FieldAudioCommand(0xA4, 1, 127)));
        Equal(pending, retarget.Process.SoundChannelVolumeFades[0]);
        retarget.Execute(Request(13, new FieldAudioCommand(0x28, 64, 60)));
        Equal(pending, retarget.Process.SoundChannelVolumeFades[0]);
        retarget.Execute(Request(14, new FieldAudioCommand(0xA0, 0)));
        Equal(true, retarget.Process.SoundChannelVolumeFades[0] is null);
        Equal(false, retarget.Process.SoundChannelStoppedByFade[0]);
        retarget.Execute(Request(15, new FieldAudioCommand(0xA4, 255, 1)));
        Equal(0, retarget.Process.SoundChannelVolumeFades[0]!.RatePerSecond);
        retarget.AdvanceAudioClock(TimeSpan.FromMilliseconds(1));
        retarget.AdvanceAudioClock(TimeSpan.FromSeconds(100));
        Equal(0, retarget.Process.SoundChannelVolumes[0]);
        Equal(true, retarget.Process.SoundChannelVolumeFades[0] is not null);
        retarget.Execute(Request(16, new FieldAudioCommand(0x20, 64, 0)));
        Equal(127, retarget.Process.SoundChannelVolumes[0]);
        Equal(true, retarget.Process.SoundChannelVolumeFades[0] is null);
        retarget.Execute(Request(17, new FieldAudioCommand(0xA4, 256, 511)));
        Equal(127, retarget.Process.SoundChannelVolumes[0]);
        Equal(true, retarget.Process.SoundChannelVolumeFades[0] is null);
        Ff7FieldHost fractional = Host();
        fractional.AdvanceAudioClock(TimeSpan.FromTicks(9999));
        fractional.Execute(Request(18, new FieldAudioCommand(0xA4, 255, 0)));
        fractional.AdvanceAudioClock(TimeSpan.FromTicks(1));
        fractional.AdvanceAudioClock(TimeSpan.FromTicks(349999));
        Equal(127, fractional.Process.SoundChannelVolumes[0]);
        fractional.AdvanceAudioClock(TimeSpan.FromTicks(1));
        Equal(126, fractional.Process.SoundChannelVolumes[0]);
        Equal(0, fractional.Process.SoundVolumeClockSubmillisecondTicks);
        Equal(5, Host(JsonSerializer.Deserialize<Ff7FieldProcess>("{}")!).Process.SoundChannelVolumeFades.Length);
    }

    private static Ff7FieldHost Host(Ff7FieldProcess? process = null)
    {
        Ff7FieldResources field = new("sound-ramp-fixture", null!, null!, null!, [], [], null!, null!, null!);
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
