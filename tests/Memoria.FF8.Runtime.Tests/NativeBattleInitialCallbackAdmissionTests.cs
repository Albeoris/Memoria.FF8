using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleInitialCallbackAdmissionTests
{
    [Theory]
    [InlineData(0x10u, true, false, true)]
    [InlineData(0x10u, true, true, false)]
    [InlineData(0x80000010u, true, false, true)]
    [InlineData(0x80000010u, true, true, false)]
    [InlineData(0u, true, false, false)]
    [InlineData(0x80000000u, true, false, false)]
    [InlineData(0x10u, false, false, false)]
    [InlineData(0x80000010u, false, false, false)]
    public void InitialCallbackRequiresAllocatedPresentActorWithClearLowByteSuspensionBit(uint flags, bool allocated, bool suspended, bool admitted)
    {
        BattleSession battle = Create(new BattleCombatantDefinition { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeCombatantFlags = flags, InitialActive = allocated, InitialSuspended = suspended });
        Assert.Empty(battle.QueuedActions);

        QueueInitialCallbacks(battle);

        if (admitted)
        {
            BattleAttackCommand command = Assert.Single(battle.QueuedActions);
            Assert.Equal("enemy", command.ActorId);
            Assert.Equal(new BattleNativeCallback(3, 0), command.NativeCallback);
        }
        else
        {
            Assert.Empty(battle.QueuedActions);
        }
        Assert.Equal(suspended, battle.Combatants.Single(actor => actor.Id == "enemy").Suspended);
    }

    [Fact]
    public void InitialCallbacksKeepPhysicalOrderAndSuspendedActorStateAcrossRestore()
    {
        BattleSession battle = Create(
            new() { Id = "first", Name = "First", NativeCombatantFlags = 0x10 },
            new() { Id = "suspended", Name = "Suspended", NativeCombatantFlags = 0x10, InitialSuspended = true, InitialHidden = true, InitialUntargetable = true },
            new() { Id = "last", Name = "Last", NativeCombatantFlags = 0x80000010 });

        QueueInitialCallbacks(battle);

        Assert.Equal(new[] { "first", "last" }, battle.QueuedActions.Select(command => command.ActorId));
        Assert.Equal(new[] { new BattleNativeCallback(3, 0), new BattleNativeCallback(5, 0) }, battle.QueuedActions.Select(command => command.NativeCallback));
        BattleSnapshot snapshot = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(new[] { "first", "last" }, restored.QueuedActions.Select(command => command.ActorId));
        BattleCombatant suspended = restored.Combatants.Single(actor => actor.Id == "suspended");
        Assert.True(suspended.Suspended && suspended.Hidden && suspended.Untargetable);
    }

    private static BattleSession Create(params BattleCombatantDefinition[] enemies)
    {
        return new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, .. enemies] }, BattleAtbMode.TurnBased);
    }

    private static void QueueInitialCallbacks(BattleSession battle)
    {
        typeof(BattleSession).GetMethod("QueueNativeInitialParticipants", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
    }
}
