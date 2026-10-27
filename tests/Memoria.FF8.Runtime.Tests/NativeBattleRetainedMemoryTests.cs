using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRetainedMemoryTests
{
    [Fact]
    public void ColdProcessKnowsLoaderBytesWhileAnUnspecifiedBattleDoesNotInventThem()
    {
        NativeBattleRetainedMemory cold = NativeBattleRetainedMemory.FromColdProcess();
        cold.Validate();
        Assert.Equal(0, cold.ActionPackets.Headers[0].ReadByte(17));
        Assert.Equal(new(0, false, false), cold.AllocationControl);
        Assert.All(cold.PartyStatusMirrors.Saved, value => Assert.Equal((ushort)0, value));
        Assert.False(cold.ActionScratch.FinisherPendingE03);
        Assert.Equal((byte)0, cold.ActionScratch.GuardianBoostPercent);
        Assert.Equal(0, cold.EffectIoResult);
        BattleSession unknown = new(Definition(), BattleAtbMode.Normal);
        Assert.Throws<NotSupportedException>(() => unknown.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Null(unknown.NativeScriptState.AllocationControl);
        Assert.Null(unknown.NativeEffectIoResult);
        Assert.All(unknown.NativePartyStatusMirrors.Saved, Assert.Null);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleAllocationControl>("{}"));
    }

    [Fact]
    public void BattleEntryResetsOnlyCountsAndNewGameResetsObservedProcessMemory()
    {
        NativeBattleRetainedMemory initial = NativeBattleRetainedMemory.FromColdProcess();
        NativeBattleActionPacketsState packets = initial.ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 1, -1, 12, 12, null));
        (packets, _) = packets.AppendContact(new(new(3, 4, 0, 0, 0, 37, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(0, 3).ActivateHeader(0);
        NativeBattleRetainedMemory observed = initial with { ActionPackets = packets, AllocationControl = new(5, true, true), PartyStatusMirrors = initial.PartyStatusMirrors.Observe(0, 0x20, 0x4000), EffectIoResult = -1 };
        BattleSession battle = new(Definition() with { NativeRetainedMemory = observed }, BattleAtbMode.Normal);
        Assert.Equal(0, battle.NativeActionPackets.ActionCount);
        Assert.Equal(0, battle.NativeActionPackets.ContactCount);
        Assert.Equal(2, battle.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Equal(37, battle.NativeActionPackets.ReadContact(0).Primary.Amount6);
        Assert.Equal(0, battle.NativeActionPackets.ActiveHeaderIndex);
        Assert.Equal(observed.AllocationControl, battle.NativeScriptState.AllocationControl);
        Assert.Equal(-1, battle.NativeEffectIoResult);
        GameSession process = GameSession.CreateColdProcess();
        process.SetBattle(battle.Capture());
        process.SetBattle(null);
        NativeBattleRetainedMemory retained = process.State.NativeBattleMemory!;
        process.StartNewGame();
        Assert.NotSame(retained, process.State.NativeBattleMemory);
        Assert.Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(process.State.NativeBattleMemory));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(process.State))!);
        Assert.Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(restored.State.NativeBattleMemory));
        BattleSession next = new(Definition() with { NativeRetainedMemory = restored.State.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(0, next.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Equal((ushort)0, next.NativePartyStatusMirrors.Saved[0]);
        Assert.Equal(0, next.NativeEffectIoResult);
    }

    [Fact]
    public void RetainedStateRejectsContradictoryActiveBattleAndMissingPartyMirrors()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativePartyStatusMirrors = null }));
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.Capture(snapshot, null);
        GameState state = new() { Battle = snapshot, NativeBattleMemory = memory with { PartyStatusMirrors = memory.PartyStatusMirrors.Observe(0, 0, 0) } };
        Assert.Throws<InvalidDataException>(() => new GameSession(state));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.PartyStatusMirrors.Observe(3, 0, 0));
    }

    [Fact]
    public void RetainedPhysicalScratchExcludesComponentOnlyPendingCompletion()
    {
        BattleSnapshot snapshot = new BattleSession(Definition(), BattleAtbMode.Normal).Capture();
        NativeBattleActionScratch scratch = snapshot.NativeActionScratch! with { FinisherPendingE03 = true, ComponentCompletion = 0x47DE70 };
        NativeBattleRetainedMemory memory = NativeBattleRetainedMemory.Capture(snapshot with { NativeActionScratch = scratch }, null);
        Assert.True(memory.ActionScratch.FinisherPendingE03);
        Assert.Null(memory.ActionScratch.ComponentCompletion);
        Assert.Equal((uint)0x47DE70, scratch.ComponentCompletion);
        GameSession restored = new(new GameState { Scene = SceneKind.Field, Battle = snapshot with { NativeActionScratch = scratch }, NativeBattleMemory = memory });
        Assert.Equal((uint)0x47DE70, restored.State.Battle!.NativeActionScratch!.ComponentCompletion);
        Assert.Null(restored.State.NativeBattleMemory!.ActionScratch.ComponentCompletion);
    }

    [Fact]
    public void In_process_checkpoint_shares_physical_owners_and_record_equality_implies_identical_JSON()
    {
        GameSession process = PhysicalCheckpoint();
        GameState source = process.State;
        NativeBattleRetainedMemory memory = source.NativeBattleMemory!;
        NativeBattleRetainedMemory observed = NativeBattleRetainedMemory.Capture(source.Battle!, memory.ColdExecutableSha256);
        Assert.Same(source.Battle!.NativeEffectMemory, memory.EffectMemory);
        Assert.Same(memory.EffectMemory, observed.EffectMemory);
        Assert.Equal(memory, observed);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(memory), JsonSerializer.SerializeToElement(observed)));
        Assert.NotNull(new GameSession(source).State.Battle);
    }

    [Fact]
    public void Independent_JSON_owners_use_structural_fallback_when_backing_arrays_are_distinct()
    {
        GameState source = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(PhysicalCheckpoint().State))!;
        NativeBattleRetainedMemory memory = source.NativeBattleMemory!;
        NativeBattleRetainedMemory observed = NativeBattleRetainedMemory.Capture(source.Battle!, memory.ColdExecutableSha256);
        Assert.NotEqual(memory, observed);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(memory), JsonSerializer.SerializeToElement(observed)));
        Assert.NotNull(new GameSession(source).State.Battle);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distinct_retained_physical_byte_or_unknown_mask_still_rejects_the_checkpoint(bool changeUnknownMask)
    {
        GameState source = PhysicalCheckpoint().State;
        NativeBattleRetainedMemory memory = source.NativeBattleMemory!;
        NativeBattleEffectArena arena = memory.EffectMemory!.Arena!;
        NativeBattleEffectArena working = NativeBattleStorageSnapshots.Restore(arena);
        NativeBattleEffectArena changed = NativeBattleStorageSnapshots.Capture(changeUnknownMask ? working.MarkUnknown(17, 1) : working.Write(17, [0xA5]));
        NativeBattleRetainedMemory contradictory = memory with { EffectMemory = memory.EffectMemory with { Arena = changed } };
        contradictory.Validate();
        Assert.NotEqual(memory, contradictory);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => new GameSession(source with { NativeBattleMemory = contradictory }));
        Assert.Contains("physical owners", error.Message);
    }

    private static GameSession PhysicalCheckpoint()
    {
        NativeBattleRetainedMemory retained = NativeBattleRetainedMemory.FromColdProcess();
        retained = retained with { EffectMemory = retained.EffectMemory! with { Arena = NativeBattleEffectArena.Clear().Write(16, [1, 2, 3]) } };
        BattleSession battle = new(Definition() with { NativeRetainedMemory = retained }, BattleAtbMode.Normal);
        GameSession process = new(new GameState { Scene = SceneKind.Field });
        process.SetBattle(battle.Capture());
        return process;
    }

    private static BattleDefinition Definition() => new()
    {
        Combatants =
        [
            new() { Id = "party", IsPlayer = true, Name = "Party", MaximumHp = 1000, CurrentHp = 1000 },
            new() { Id = "enemy", Name = "Enemy", MaximumHp = 1000, CurrentHp = 1000 }
        ]
    };
}
