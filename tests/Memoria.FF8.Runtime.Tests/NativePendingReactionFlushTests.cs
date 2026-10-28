using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativePendingReactionFlushTests
{
    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 4, true)]
    [InlineData(3, 1, true)]
    public void Completion_consumes_both_pending_bytes_even_when_KO_skips_stage2(int stage, int savedStatus, bool queuesCallback)
    {
        BattleSession session = Create();
        SetPending(session.Combatants[1], (byte)stage, 1, (ushort)savedStatus);
        SetOrder(session, 2);
        Assert.True(Pending(session));
        Flush(session);
        BattleSnapshot snapshot = session.Capture();
        Assert.Equal(new NativeBattlePendingReaction(0, 0), ReadPending(session.Combatants[1]));
        Assert.Equal((byte)2, snapshot.NativeReactionOrder);
        Assert.False(Pending(session));
        BattleAttackCommand[] commands = snapshot.NativeCommandBanks!.BoundCommands.ToArray();
        Assert.Equal(queuesCallback ? 1 : 0, commands.Length);
        if (queuesCallback)
        {
            Assert.Equal(new BattleNativeCallback(3, (ushort)stage), commands[0].NativeCallback);
        }
        AssertStableAfterRestoreAndRepeatedFlush(snapshot);
    }

    [Fact]
    public void Completion_keeps_actual_callback_order_and_does_not_republish_consumed_death_or_live_reactions()
    {
        BattleSession session = Create();
        SetPending(session.Combatants[1], 3, 1, 1);
        SetPending(session.Combatants[2], 2, 0, 1);
        SetPending(session.Combatants[3], 2, 2, 0);
        SetOrder(session, 3);
        Flush(session);
        BattleSnapshot snapshot = session.Capture();
        Assert.Equal(new[] { new BattleNativeCallback(3, 3), new BattleNativeCallback(5, 2) }, snapshot.NativeCommandBanks!.BoundCommands.Select(command => command.NativeCallback).ToArray());
        Assert.All(session.Combatants.Skip(1), actor => Assert.Equal(new NativeBattlePendingReaction(0, 0), ReadPending(actor)));
        Assert.Equal((byte)3, snapshot.NativeReactionOrder);
        Assert.False(Pending(session));
        AssertStableAfterRestoreAndRepeatedFlush(snapshot);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(3, false)]
    public void PetrifyDefeatHoldDoesNotBypassNativeReactionGateAndOtherCapablePartyAllowsFlush(int partyCount, bool held)
    {
        BattleSession session = Create(partyCount);
        session.ConfigureDiagnosticTerminalHold(new("Declared Petrify repetition", session.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id).ToImmutableArray(), 100));
        BattleCombatant recipient = session.Combatants[0];
        SetPending(recipient, 0, 0, 4);
        SetOrder(session, 0);
        typeof(BattleSession).GetMethod("QueueNativeReaction", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [recipient, 2]);
        Assert.Equal(new NativeBattlePendingReaction(2, 0), ReadPending(recipient));
        Assert.True(Pending(session));
        typeof(BattleSession).GetMethod("CompleteBattleIfNeeded", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);

        Assert.Equal(BattleOutcome.Active, session.Outcome);
        Assert.Equal(held ? BattleDiagnosticTerminalHoldPhase.Held : BattleDiagnosticTerminalHoldPhase.Armed, session.DiagnosticTerminalHold!.Phase);
        Flush(session);

        Assert.Equal(held, Pending(session));
        Assert.Equal(held ? new NativeBattlePendingReaction(2, 0) : new NativeBattlePendingReaction(0, 0), ReadPending(recipient));
        BattleSnapshot snapshot = session.Capture();
        Assert.Equal((byte)1, snapshot.NativeReactionOrder);
        Assert.Equal((ushort)4, session.ObserveDiagnosticCombatants()[0].Statuses0);
        BattleAttackCommand[] callbacks = snapshot.NativeCommandBanks!.BoundCommands.ToArray();
        if (held)
        {
            Assert.Empty(callbacks);
            Assert.Single(session.DiagnosticTerminalHoldReceipts);
        }
        else
        {
            Assert.Single(callbacks);
            Assert.Equal(new BattleNativeCallback(0, 2), callbacks[0].NativeCallback);
            Assert.Empty(session.DiagnosticTerminalHoldReceipts);
            Assert.All(session.Combatants.Where(actor => actor.IsPlayer).Skip(1), actor => Assert.Equal((ushort)0, session.ObserveDiagnosticCombatants().Single(state => state.Id == actor.Id).Statuses0));
        }
    }

    private static BattleSession Create(int partyCount = 1)
    {
        return new(new()
        {
            Combatants =
            [
                .. Enumerable.Range(0, partyCount).Select(index => new BattleCombatantDefinition { Id = index == 0 ? "party" : $"party{index}", Name = "Party", IsPlayer = true, CurrentHp = 100, MaximumHp = 100 }),
                new() { Id = "enemy0", Name = "Enemy0", CurrentHp = 100, MaximumHp = 100 },
                new() { Id = "enemy1", Name = "Enemy1", CurrentHp = 100, MaximumHp = 100 },
                new() { Id = "enemy2", Name = "Enemy2", CurrentHp = 100, MaximumHp = 100 }
            ]
        }, BattleAtbMode.Normal);
    }

    private static void SetPending(BattleCombatant actor, byte stage, byte order, ushort savedStatus)
    {
        typeof(BattleCombatant).GetProperty("NativePendingReaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, new NativeBattlePendingReaction(stage, order));
        typeof(BattleCombatant).GetProperty("NativeStatus0", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, savedStatus);
        typeof(BattleCombatant).GetProperty(nameof(BattleCombatant.CurrentHp))!.SetValue(actor, (savedStatus & 1) != 0 ? 0 : 100);
    }

    private static NativeBattlePendingReaction ReadPending(BattleCombatant actor)
    {
        return (NativeBattlePendingReaction)typeof(BattleCombatant).GetProperty("NativePendingReaction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
    }

    private static void SetOrder(BattleSession session, byte order)
    {
        typeof(BattleSession).GetField("nativeReactionOrder", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, order);
    }

    private static bool Pending(BattleSession session)
    {
        return (bool)typeof(BattleSession).GetProperty("HasNativePendingReactions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    }

    private static void Flush(BattleSession session)
    {
        typeof(BattleSession).GetMethod("FlushNativeReactions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
    }

    private static void AssertStableAfterRestoreAndRepeatedFlush(BattleSnapshot snapshot)
    {
        BattleSnapshot serialized = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!;
        BattleSession restored = BattleSession.Restore(serialized);
        string before = JsonSerializer.Serialize(restored.Capture());
        Flush(restored);
        Flush(restored);
        Assert.False(Pending(restored));
        Assert.Equal(before, JsonSerializer.Serialize(restored.Capture()));
    }
}
