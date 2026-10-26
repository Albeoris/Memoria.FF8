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

    private static BattleSession Create()
    {
        return new(new()
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = 100, MaximumHp = 100 },
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
