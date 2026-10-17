using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMusicEntryPresentationTests
{
    [Fact]
    public void Early_stop_and_changed_play_keep_both_native_stops_before_the_sound_tail()
    {
        NativeBattleMusicEntryReceipt entry = Create(false, true, new(0, 5, true, true, false, 63, 127));
        string before = JsonSerializer.Serialize(entry);
        Assert.Equal(new[] { "stop0", "stop0", "play0:5:63", "gain0:127", "stop-sfx", "gain-sfx:0:32:127", "sfx:10:26", "sfx:11:27", "sfx:12:28" }, Trace(entry));
        Assert.Equal(before, JsonSerializer.Serialize(entry));
    }

    [Fact]
    public void Keep_music_preserves_the_backend_and_still_executes_all_three_encounter_sounds()
    {
        NativeBattleMusicEntryReceipt entry = Create(true, false, null);
        Assert.Equal(new[] { "stop-sfx", "gain-sfx:0:32:127", "sfx:10:26", "sfx:11:27", "sfx:12:28" }, Trace(entry));
    }

    [Fact]
    public void Decoded_zero_after_early_stop_does_not_duplicate_stop_or_invent_a_play()
    {
        NativeBattleMusicEntryReceipt entry = Create(false, true, new(0, 0, false, false, false, 63, 127));
        Assert.Equal(new[] { "stop0", "gain0:127", "stop-sfx", "gain-sfx:0:32:127", "sfx:10:26", "sfx:11:27", "sfx:12:28" }, Trace(entry));
    }

    [Fact]
    public void Same_song_without_early_stop_keeps_only_the_actual_final_gain_before_the_sound_tail()
    {
        NativeBattleMusicEntryReceipt entry = Create(false, false, new(0, 17, false, false, false, 63, 127));
        Assert.Equal(new[] { "gain0:127", "stop-sfx", "gain-sfx:0:32:127", "sfx:10:26", "sfx:11:27", "sfx:12:28" }, Trace(entry));
    }

    [Fact]
    public void Invalid_tail_refuses_before_any_music_or_sound_device_callback()
    {
        NativeBattleMusicEntryReceipt entry = Create(false, true, new(0, 5, true, true, false, 63, 127));
        entry = entry with { Sounds = entry.Sounds.SetItem(2, entry.Sounds[2] with { Volume = 0 }) };
        List<string> calls = [];
        Assert.Throws<InvalidDataException>(() => Dispatch(entry, calls));
        Assert.Empty(calls);
    }

    private static NativeBattleMusicEntryReceipt Create(bool keep, bool earlyStop, NativeMusicPlaybackDecision? playback)
    {
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>(3);
        for (uint index = 0; index < 3; index++)
        {
            NativeBattleModuleSound request = new(10 + index, 0, null) { RawPan = 128, Pan = 128 };
            (sound, request) = request.Commit46B2A0(sound);
            sounds.Add(request);
        }
        return new(512, keep ? (ushort)0x10 : (ushort)0, 0, keep ? (byte)0 : (byte)1, keep ? (byte)1 : (byte)0, earlyStop, false, playback, sounds.MoveToImmutable());
    }

    private static List<string> Trace(NativeBattleMusicEntryReceipt entry)
    {
        List<string> calls = [];
        Dispatch(entry, calls);
        return calls;
    }

    private static void Dispatch(NativeBattleMusicEntryReceipt entry, List<string> calls)
    {
        FieldAudioPresentation.DispatchNativeBattleMusicEntry(entry, slot => calls.Add($"stop{slot}"), decision => FieldAudioPresentation.DispatchNativeMusicPlayback(decision, slot => calls.Add($"stop{slot}"), (slot, id, gain) => calls.Add($"play{slot}:{id}:{gain}"), (slot, gain) => calls.Add($"gain{slot}:{gain}"), () => Assert.Fail("Battle entry does not select concert instruments.")), () => calls.Add("stop-sfx"), () => calls.Add("gain-sfx:0:32:127"), sound => calls.Add($"sfx:{sound.Sound}:{sound.ReadCommittedVoice()}"));
    }
}
