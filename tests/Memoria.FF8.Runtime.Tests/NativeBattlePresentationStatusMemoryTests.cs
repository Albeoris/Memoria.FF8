using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePresentationStatusMemoryTests
{
    [Fact]
    public void Current_ready_contact_observes_the_previous_typed_status_before_refreshing_it()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        BattleCombatant actor = battle.Combatants[0];
        NativeBattleStatusMirror prior = new(0x20, 0x4000);
        actor.NativePresentationStatus = prior;
        BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(BattleSession).GetMethod("InitializeExpandedPartyPresentation", privateInstance)!.Invoke(battle, null);
        BattleModelRoster roster = battle.ReadBattlePresentationRoster();
        BattleModelOwner owner = battle.ReadPresentationModelOwner(actor.Id);
        NativeBattleContactHalf contact = new(owner, 0, 0, 0, 0x40, 0, 0x2000);
        typeof(BattleSession).GetMethod("PublishExpandedReadyContact", privateInstance)!.Invoke(battle, [roster, contact, actor.Id]);

        var ready = Assert.IsType<ImmutableDictionary<string, NativeBattleStatusMirror>>(typeof(BattleSession).GetField("expandedPartyReadyStatusMirrors", privateInstance)!.GetValue(battle));
        Assert.Equal(prior, ready[actor.Id]);
        Assert.Equal(new NativeBattleStatusMirror(0x40, 0x2000), actor.NativePresentationStatus);
        Assert.Equal(actor.NativePresentationStatus, battle.NativePresentationStatusMemory.Rows[0]);
    }

    [Fact]
    public void A_typed_actor_mirror_survives_restore_without_importing_the_physical_bank_into_live_facts()
    {
        BattleSession battle = new(Definition(), BattleAtbMode.Normal);
        NativeBattleStatusMirror typed = new(0x40, 0x2000);
        battle.Combatants[0].NativePresentationStatus = typed;
        BattleSession fork = battle.ForkForContinuation();

        Assert.Equal(typed, fork.Combatants[0].NativePresentationStatus);
        Assert.Equal(new NativeBattleStatusMirror(0x20, 0x4000), fork.NativePresentationStatusMemory.Rows[0]);
        fork.Combatants[0].NativePresentationStatus = new(1, 2);
        Assert.Equal(typed, battle.Combatants[0].NativePresentationStatus);
        Assert.Equal(new NativeBattleStatusMirror(0x80, 0x200), fork.NativePresentationStatusMemory.Rows[3]);
    }

    private static BattleDefinition Definition()
    {
        NativeBattleEnemyLevel level = new(100, 20, 5, 20, 5, 100, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattlePresentationStatusMemory mirrors = NativeBattlePresentationStatusMemory.FromColdProcess().Observe(0, new(0x20, 0x4000)).Observe(3, new(0x80, 0x200));
        return new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { PresentationStatusMemory = mirrors },
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, NativeStatus0 = 0x40, NativeStatus1 = 0x2000 },
                new() { Id = "enemy", Name = "Enemy", SceneSlot = 0, NativeTemplate = new(1, Enumerable.Repeat(level, 101).ToArray()) { Information254 = 0 } }
            ]
        };
    }
}
