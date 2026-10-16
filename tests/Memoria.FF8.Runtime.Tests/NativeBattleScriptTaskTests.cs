using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptTaskTests
{
    [Fact]
    public void ClearOnlyOwnsTheNativeClearedCompletionByteAndWritersPreserveRetainedPayload()
    {
        NativeBattleScriptTasks state = NativeBattleScriptTasks.Clear();
        Assert.Throws<NotSupportedException>(() => state.Slots[0].ReadWord(8));
        state = state.AppendWait(1234);
        state = state.Advance((current, slot) => current with { Slots = current.Slots.SetItem(slot, current.Slots[slot].WriteByte(15, 1)) });
        BattleEvent message = new(0, "NativeText", "enemy", null, 30, "Message") { NativeMessageMode = 3 };
        state = state.AppendMessage(message, false, 0);
        Assert.Equal(1234, state.Slots[0].ReadWord(8));
        Assert.Equal(0x48AC60u, state.Slots[0].ReadDword(0));
        Assert.Throws<NotSupportedException>(() => state.Slots[0].ReadDword(4));
        Assert.Equal(message, state.Slots[0].Message);
    }

    [Fact]
    public void CompletionCleanupRunsAfterAllLinkedCallbacksAndPreservesFreedPayloadBytes()
    {
        NativeBattleScriptTasks state = NativeBattleScriptTasks.Clear().AppendWait(2).AppendWait(3);
        List<int> order = [];
        state = state.Advance((current, slot) =>
        {
            order.Add(slot);
            if (slot == 1)
            {
                Assert.Equal((byte)1, current.Slots[0].ReadByte(15));
                Assert.True(current.Allocated(0));
            }
            return slot == 0 ? current with { Slots = current.Slots.SetItem(slot, current.Slots[slot].WriteByte(15, 1)) } : current;
        });
        Assert.Equal(new[] { 0, 1 }, order);
        Assert.False(state.Allocated(0));
        Assert.Equal((byte)255, state.Links[1].Previous);
        Assert.Equal((ushort)2, state.Slots[0].ReadWord(8));
        state.Validate();
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleScriptTasks>(JsonSerializer.Serialize(state))));
    }

    [Fact]
    public void ACallbackAppendedDuringDispatchRunsBeforePhysicalCleanup()
    {
        NativeBattleScriptTasks state = NativeBattleScriptTasks.Clear().AppendWait(0);
        List<int> order = [];
        state = state.Advance((current, slot) =>
        {
            order.Add(slot);
            if (slot == 0)
            {
                current = current.AppendWait(8);
            }
            return current with { Slots = current.Slots.SetItem(slot, current.Slots[slot].WriteByte(15, 1)) };
        });
        Assert.Equal(new[] { 0, 1 }, order);
        Assert.Equal((byte)255, state.Tail);
        Assert.All(Enumerable.Range(0, 16), slot => Assert.False(state.Allocated(slot)));
    }

    [Fact]
    public void OriginalFullBankAllocationAliasesSlotZeroAndLosesTheHeadMarker()
    {
        NativeBattleScriptTasks state = NativeBattleScriptTasks.Clear();
        for (ushort index = 0; index < 17; index++)
        {
            state = state.AppendWait(index);
        }
        Assert.Equal((byte)0, state.Tail);
        Assert.Equal((ushort)16, state.Slots[0].ReadWord(8));
        int calls = 0;
        state = state.Advance((current, _) =>
        {
            calls++;
            return current;
        });
        Assert.Equal(0, calls);
        state.Validate();
    }

    [Fact]
    public void AiMessage26And34UseRealCallbackTimingAndRestoreThePendingDelay()
    {
        BattleSession battle = Create([26, 0, 34, 1, 2, 0]);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "NativeText");
        battle.Tick();
        Assert.Equal("Immediate", Assert.Single(battle.Events, entry => entry.Kind == "NativeText").Message);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        battle.Tick();
        restored.Tick();
        Assert.Single(battle.Events, entry => entry.Kind == "NativeText");
        battle.Tick();
        restored.Tick();
        Assert.Equal(new[] { "Immediate", "Delayed" }, battle.Events.Where(entry => entry.Kind == "NativeText").Select(entry => entry.Message));
        Assert.Equal(new long[] { 1, 3 }, battle.Events.Where(entry => entry.Kind == "NativeText").Select(entry => entry.Frame));
        Assert.Equal((ushort)65535, battle.Capture().NativeScriptState.Tasks.Slots[1].ReadWord(8));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void WaitCallbacksShareDfdAndTheLastCallbackWriteWins()
    {
        BattleSession battle = Create([32, 1, 32, 0, 0]);
        battle.Tick();
        Assert.False(battle.NativeScriptState.AllocationControl!.PauseGate);
        Assert.Equal(new ushort[] { 14 }, battle.NativeScriptState.WaitCounters);
        battle.Tick();
        Assert.True(battle.NativeScriptState.AllocationControl!.PauseGate);
        Assert.Equal(new ushort[] { 13 }, battle.NativeScriptState.WaitCounters);
        int atb = battle.Combatants[0].Atb;
        battle.Tick();
        Assert.Equal(atb, battle.Combatants[0].Atb);
        Assert.Equal(new ushort[] { 12 }, battle.NativeScriptState.WaitCounters);
    }

    private static BattleSession Create(byte[] init)
    {
        NativeBattleAiDefinition ai = new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, ["Immediate", "Delayed"]);
        return new(new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, Speed = 1 }, new() { Id = "enemy", Name = "Enemy", NativeAi = ai, Speed = 1 }] }, BattleAtbMode.Normal);
    }
}
