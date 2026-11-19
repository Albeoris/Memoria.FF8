using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePendingMusicTransitionTests
{
    internal static void Run()
    {
        Ff7FieldHost host = Host(new() { NativeMusicTrack = 2, MusicTicks = 91, MusicVolume = 127 });
        host.SubmitNativeAudio(new(0x18, 4, 30));
        Check(host.Process.NativeMusicTrack == 2 && host.Process.MusicTicks == 91 && host.Process.MusicVolume == 126 && host.Process.MusicTransition == new Ff7MusicTransition(4, 15, Ff7MusicTransitionPhase.FadeOut) && host.Process.MusicVolumeFade?.RatePerSecond == -448, "Long MIDI transition replaced the old track before its native fade-out or lost the signed half duration/immediate gain step.");
        host.AdvanceAudioClock(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * 100 + 3));
        Check(host.Process.MusicVolume == 82 && host.Process.NativeMusicTrack == 2, "Native transition gain did not use signed integer milliseconds and truncation.");
        Ff7FieldHost cold = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Ff7FieldHost next = Host(host.CreateNextFieldProcess());
        Check(next.Process.MusicTransition == host.Process.MusicTransition && next.Process.MusicVolumeFade == host.Process.MusicVolumeFade, "A field transition discarded its process-wide pending MIDI owner.");
        string paused = JsonSerializer.Serialize(host.Capture());
        host.AdvanceAudioClock(TimeSpan.FromSeconds(10), paused: true);
        Check(paused == JsonSerializer.Serialize(host.Capture()), "A paused audio clock advanced its pending native music transition.");
        for (int update = 0; update < 5; update++)
        {
            host.AdvanceAudioClock(TimeSpan.FromMilliseconds(100));
            cold.AdvanceAudioClock(TimeSpan.FromMilliseconds(100));
            Check(JsonSerializer.Serialize(host.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold pending MIDI phase lost its exact gain, fractional clock, track or native age.");
            if (update == 1)
            {
                Check(host.Process.NativeMusicTrack == 4 && host.Process.MusicTicks == 0 && host.Process.MusicVolume == 16 && host.Process.MusicTransition?.Phase == Ff7MusicTransitionPhase.FadeIn, "The old fade endpoint did not start the new track and its native fade-in exactly once.");
            }
        }
        Check(host.Process.NativeMusicTrack == 4 && host.Process.MusicVolume == 127 && host.Process.MusicTransition is null && host.Process.MusicVolumeFade is null, "The new native track did not finish its half-duration fade-in.");
        foreach (int duration in new[] { 0, 9, 10, 19 })
        {
            Ff7FieldHost immediate = Host(new() { NativeMusicTrack = 2, MusicVolume = 127 });
            immediate.SubmitNativeAudio(new(0x18, 4, duration));
            Check(immediate.Process.NativeMusicTrack == 4 && immediate.Process.MusicVolume == 127 && immediate.Process.MusicTransition is null, "A native half duration below ten was incorrectly made pending or rejected.");
        }
        Ff7FieldHost byteDuration = Host(new() { NativeMusicTrack = 2, MusicVolume = 127 });
        byteDuration.SubmitNativeAudio(new(0x18, 4, -1));
        Check(byteDuration.Process.MusicTransition?.Duration == 127, "The MIDI dispatch did not narrow its signed field duration to the actual unsigned byte before splitting it.");
        Ff7FieldHost same = Host(new() { NativeMusicTrack = 2, MusicTicks = 71, MusicVolume = 30 });
        same.SubmitNativeAudio(new(0x18, 2, 30));
        Check(same.Process.NativeMusicTrack == 2 && same.Process.MusicTicks == 71 && same.Process.MusicTransition?.Phase == Ff7MusicTransitionPhase.Gain && same.Process.MusicTransition.Duration == 30 && same.Process.MusicVolume == 31, "Same-track MIDI transition restarted playback or incorrectly halved its gain duration.");
        same.AdvanceAudioClock(TimeSpan.FromSeconds(1));
        Check(same.Process.MusicVolume == 127 && same.Process.MusicTransition is null, "Same-track transition did not release its gain owner.");
        Ff7FieldHost silent = Host(new() { NativeMusicTrack = 0, MusicVolume = 12 });
        silent.SubmitNativeAudio(new(0x18, 4, 30));
        Check(silent.Process.NativeMusicTrack == 4 && silent.Process.MusicVolume == 127 && silent.Process.MusicTransition is null, "An absent old native track acquired a fictitious fade-out.");
        same.SubmitNativeAudio(new(0x18, 0, 30));
        Check(same.Process.NativeMusicTrack == 0 && same.Process.MusicTransition is null, "The native stop selector left a pending target alive.");
        Ff7FieldProcess legacy = new() { NativeMusicTrack = 2, MusicTicks = 9, MusicVolume = 42 };
        Ff7FieldHost restored = Host(JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(legacy))!);
        Check(restored.Process.MusicTransition is null && restored.Process.NativeMusicTrack == 2 && restored.Process.MusicTicks == 9 && restored.Process.MusicVolume == 42, "Missing optional music transition state altered an older save's playback facts.");
        Console.WriteLine("PASS native pending MIDI transition halves, gain timing, track ownership, muted clock, pause, field/cold continuation and legacy defaults");
    }

    private static Ff7FieldHost Host(Ff7FieldProcess process)
    {
        Ff7FieldResources field = new("music-transition-fixture", null!, null!, null!, [], [], null!, null!, null!);
        return new(field, (_, _) => 1, process);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
