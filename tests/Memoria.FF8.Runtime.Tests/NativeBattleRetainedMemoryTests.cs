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
    public void BattleEntryResetsOnlyCountsAndNewGamePreservesObservedProcessMemory()
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
        Assert.Same(retained, process.State.NativeBattleMemory);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(process.State))!);
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(restored.State.NativeBattleMemory));
        BattleSession next = new(Definition() with { NativeRetainedMemory = restored.State.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(2, next.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Equal((ushort)0x20, next.NativePartyStatusMirrors.Saved[0]);
        Assert.Equal(-1, next.NativeEffectIoResult);
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
