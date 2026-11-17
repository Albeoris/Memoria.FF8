using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSavedMugTests
{
    [Theory]
    [InlineData((ushort)236, false)]
    [InlineData((ushort)236, true)]
    [InlineData((ushort)237, false)]
    [InlineData((ushort)237, true)]
    public void SavedMugPartyExecutesSmartAttackAfterJsonRestore(ushort formation, bool forcePhysical)
    {
        string input = TestResources.FixturePath("App.Tests.BattleSavedMugTests.1");
        GameSession session = new(GameplaySaveSessionLoader.Load(input, TestResources.GameDirectory));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        BattleDefinition definition = world.CreateEncounterBattle(formation) with { NativeGraphics = null, NativeClockRules = null };
        BattleCombatantDefinition equipped = Assert.Single(definition.Combatants, actor => actor.IsPlayer && actor.NativeCommandSlots[0] == 12);
        Assert.Contains(equipped.NativeCommands, command => command.Command == 12);
        Assert.DoesNotContain(equipped.NativeCommands, command => command.Command == 1);
        Assert.Contains(BattleAbility.Attack, equipped.Abilities);
        definition = definition with { Combatants = definition.Combatants.Select(actor => actor with { InitialAtb = actor.Id == equipped.Id ? BattleSession.AtbMaximum : 0 }).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        Assert.True(battle.NativeEntryReady);
        BattleCombatant target = battle.Combatants.First(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive && !actor.Untargetable);
        Assert.NotNull(target.NativeMug);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack(equipped.Id, target.Id, forcePhysical);
        Assert.Equal(BattleAbility.Attack, preview.Ability);
        Assert.Equal(Assert.Single(equipped.NativeCommands, command => command.Command == 12).Name, preview.Name);
        battle.QueueSmartAttack(preview);
        Assert.Contains(battle.QueuedActions, command => command.ActorId == equipped.Id && command.NativeAction?.Command == 12);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 300; frame++)
        {
            battle.Tick();
            restored.Tick();
            if (!battle.Combatants.Single(actor => actor.Id == equipped.Id).IsBusy && battle.Events.Any(entry => entry.Kind == "Damage" && entry.ActorId == equipped.Id && entry.TargetId == target.Id))
            {
                break;
            }
        }
        Assert.False(battle.Combatants.Single(actor => actor.Id == equipped.Id).IsBusy);
        Assert.Contains(battle.Events, entry => entry.Kind == "NativeText" && entry.ActorId == equipped.Id && entry.TargetId == target.Id);
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == equipped.Id && entry.TargetId == target.Id);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
