using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledPainPresentationTests
{
    [Fact]
    public void InstalledTriplePainHitsStoppedGrendelAndRepeatsTheSecondTarget()
    {
        const uint stop = 0x8;
        const uint triple = 0x40000;
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition pain = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == 45);
        const int initialStock = 10;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        NativeCharacterVital[] records = vitals.Records.ToArray();
        uint magicCommand = checked((uint)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf(2)));
        NativeMagicStock[] stock = records[0].MagicInventory.Select(_ => new NativeMagicStock(0, 0)).ToArray();
        stock[0] = new((byte)pain.Id, initialStock);
        records[0] = records[0] with { Junction = records[0].Junction with { Commands = magicCommand }, MagicInventory = stock };
        session.SetCharacterVitals(vitals with { Records = records });
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });
        BattleSession battle = world.PrepareBattleEntry(700, 0, 41, BattleAtbMode.TurnBased).Battle;
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-0");
        BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive).ToArray();
        Assert.Equal(2, enemies.Length);
        BattleCombatant grendel = Assert.Single(enemies, actor => actor.ModelId == 53);
        BattleCombatant other = Assert.Single(enemies, actor => actor != grendel);
        string[] targets = [grendel.Id, other.Id, other.Id];
        for (int frame = 0; frame < 600 && !battle.NativeEntryReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        grendel.NativeStatus1 |= stop;
        grendel.NativeStatusTimers[3] = 3000;
        caster.NativeStatus1 |= triple;
        caster.Atb = BattleSession.AtbMaximum;
        int firstEvent = battle.Events.Count;

        battle.QueueAction(caster.Id, grendel.Id, BattleAbility.Magic, pain.Id, magicTargets: targets);
        Assert.True(caster.IsBusy);
        for (int frame = 0; frame < 600 && caster.IsBusy; frame++)
        {
            battle.Tick();
        }

        Assert.False(caster.IsBusy);
        Assert.Null(battle.Presentation?.ActiveActionToken);
        Assert.Equal(stop, grendel.NativeStatus1 & stop);
        Assert.Equal(initialStock - 3, Assert.Single(caster.MagicStocks, stock => stock.Id == pain.Id).Count);
        BattleEvent[] events = battle.Events.Skip(firstEvent).Where(item => item.ActorId == caster.Id).ToArray();
        Assert.Equal(targets, events.Where(item => item.Kind == "EffectContact").Select(item => item.TargetId));
        Assert.Single(events, item => item.Kind == "ActionComplete");
    }
}
