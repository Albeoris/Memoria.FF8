using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSoundProcessTests
{
    [Fact]
    public void Automatic_sounds_share_six_physical_voices_and_explicit_requests_preserve_rotation()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        int[] expected = [26, 27, 28, 29, 24, 25, 26];
        for (int index = 0; index < expected.Length; index++)
        {
            (process, NativeBattleModuleSound automatic) = new NativeBattleModuleSound((uint)(100 + index), index == 1 ? 0xFF000000u : 0, null).Commit46B2A0(process);
            Assert.Equal(expected[index], automatic.ReadCommittedVoice());
            Assert.Equal((uint)(100 + index), process.OriginalEffectIds[expected[index]]);
            byte? rotation = process.Rotation;
            (process, NativeBattleModuleSound explicitSound) = new NativeBattleModuleSound(300000, 0x800100, null).Commit46B2A0(process);
            Assert.Equal((byte)9, explicitSound.ReadCommittedVoice());
            Assert.Equal(rotation, process.Rotation);
            Assert.Equal(300000u, process.OriginalEffectIds[9]);
        }
        Assert.Equal(106u, process.OriginalEffectIds[26]);
    }

    [Theory]
    [InlineData(1u, 1)]
    [InlineData(8u, 4)]
    [InlineData(16u, 5)]
    [InlineData(0x100u, 9)]
    [InlineData(0x8000u, 16)]
    [InlineData(0x800000u, 24)]
    [InlineData(0xFF800008u, 4)]
    public void Committed_receipts_preserve_native_lowest_bit_selection(uint flags, byte voice)
    {
        (NativeGlobalSoundProcess process, NativeBattleModuleSound sound) = new NativeBattleModuleSound(0x12345678, flags, null).Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess());
        Assert.Equal(voice, sound.ReadCommittedVoice());
        Assert.Equal((byte)1, process.Rotation);
        Assert.Equal(0x12345678u, process.OriginalEffectIds[voice]);
    }

    [Fact]
    public void Output_rejection_does_not_undo_the_native_allocation_or_original_ID_write()
    {
        (NativeGlobalSoundProcess process, NativeBattleModuleSound sound) = new NativeBattleModuleSound(uint.MaxValue, 0, null) { RawPan = uint.MaxValue, Volume = uint.MaxValue }.Commit46B2A0(NativeGlobalSoundProcess.FromBufferlessColdProcess());
        Assert.Equal((byte)26, sound.ReadCommittedVoice());
        Assert.Equal(uint.MaxValue, process.OriginalEffectIds[26]);
        Assert.Equal(uint.MaxValue, sound.RawPan);
        Assert.Equal(uint.MaxValue, sound.Volume);
    }

    [Fact]
    public void Missing_owner_and_attempting_to_allocate_an_existing_receipt_fail_without_mutation()
    {
        NativeBattleModuleSound request = new(2, 0, null);
        Assert.Throws<NotSupportedException>(() => request.Commit46B2A0(null));
        Assert.Null(request.Allocation);
        NativeGlobalSoundProcess initial = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        (NativeGlobalSoundProcess next, NativeBattleModuleSound committed) = request.Commit46B2A0(initial);
        Assert.Throws<InvalidDataException>(() => committed.Commit46B2A0(next));
        Assert.Equal((byte)1, initial.Rotation);
        Assert.All(initial.OriginalEffectIds, id => Assert.Equal(0u, id));
    }

    [Fact]
    public void Global_process_and_committed_receipts_roundtrip_independently_before_the_next_sound()
    {
        NativeGlobalSoundProcess process = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        (process, NativeBattleModuleSound first) = new NativeBattleModuleSound(0x87, 0, null).Commit46B2A0(process);
        (process, NativeBattleModuleSound second) = new NativeBattleModuleSound(0x88, 0, null).Commit46B2A0(process);
        NativeGlobalSoundProcess clone = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(process))!;
        NativeBattleModuleSound restored = JsonSerializer.Deserialize<NativeBattleModuleSound>(JsonSerializer.Serialize(second))!;
        Assert.Equal((byte)26, first.ReadCommittedVoice());
        Assert.Equal((byte)27, restored.ReadCommittedVoice());
        var left = new NativeBattleModuleSound(2, 0, null).Commit46B2A0(process);
        var right = new NativeBattleModuleSound(2, 0, null).Commit46B2A0(clone);
        Assert.Equal((byte)28, right.Sound.ReadCommittedVoice());
        Assert.Equal(JsonSerializer.Serialize(left.Process), JsonSerializer.Serialize(right.Process));
        Assert.Equal(JsonSerializer.Serialize(left.Sound), JsonSerializer.Serialize(right.Sound));
    }

    [Fact]
    public void Battle_entry_snapshot_stop_all_and_return_preserve_the_global_process()
    {
        GameSession game = GameSession.CreateColdProcess();
        NativeGlobalSoundProcess incoming = game.GlobalSoundProcess!.RecordEffect(123, 0, out _);
        game.SetGlobalSoundProcess(incoming);
        BattleSession battle = new(Definition(incoming), BattleAtbMode.TurnBased);
        Assert.Same(incoming, battle.NativeGlobalSoundProcess);
        BattleSnapshot saved = battle.Capture();
        saved = saved with { Events = [.. saved.Events, new(0, "NativeSoundStopAll", null, null, 0, string.Empty) { NativeStopAllSounds = true }] };
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        Assert.Equal(JsonSerializer.Serialize(incoming), JsonSerializer.Serialize(restored.NativeGlobalSoundProcess));
        NativeGlobalSoundProcess outgoing = restored.NativeGlobalSoundProcess!.RecordEffect(456, 0, out _);
        restored = BattleSession.Restore(restored.Capture() with { NativeGlobalSoundProcess = outgoing });
        game.SetBattle(restored.Capture());
        Assert.Equal(JsonSerializer.Serialize(outgoing), JsonSerializer.Serialize(game.GlobalSoundProcess));
        game.SetBattle(null);
        Assert.Equal(JsonSerializer.Serialize(outgoing), JsonSerializer.Serialize(game.GlobalSoundProcess));
    }

    [Fact]
    public void Old_battle_absence_stays_unknown_even_when_definition_has_a_known_entry_owner()
    {
        BattleSession battle = new(Definition(NativeGlobalSoundProcess.FromBufferlessColdProcess()), BattleAtbMode.TurnBased);
        BattleSession restored = BattleSession.Restore(battle.Capture() with { NativeGlobalSoundProcess = null });
        Assert.Null(restored.NativeGlobalSoundProcess);
        Assert.Throws<NotSupportedException>(() => new NativeBattleModuleSound(2, 0, null).Commit46B2A0(restored.NativeGlobalSoundProcess));
    }

    private static BattleDefinition Definition(NativeGlobalSoundProcess? sound) => new()
    {
        NativeGlobalSoundProcess = sound,
        Combatants =
        [
            new() { Id = "party", IsPlayer = true, Name = "Party" },
            new() { Id = "enemy", Name = "Enemy" }
        ]
    };
}
