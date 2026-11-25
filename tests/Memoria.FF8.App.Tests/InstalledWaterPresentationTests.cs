using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledWaterPresentationTests
{
    [Theory]
    [InlineData(701)]
    [InlineData(703)]
    public void OrdinaryWaterCompletesConsecutiveCastsAcrossOriginalAndAddedEnemySlots(ushort encounter)
    {
        const int water = 10;
        const int stockCount = 10;
        const uint stop = 0x8;
        string directory = TestResources.GameDirectory;
        GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        NativeCharacterVital[] records = vitals.Records.ToArray();
        uint magicCommand = checked((uint)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf(2)));
        NativeMagicStock[] stock = records[0].MagicInventory.Select(_ => new NativeMagicStock(0, 0)).ToArray();
        stock[0] = new(water, stockCount);
        records[0] = records[0] with { Junction = records[0].Junction with { Commands = magicCommand }, MagicInventory = stock };
        session.SetCharacterVitals(vitals with { Records = records });
        world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { AdditionalEnemies = 1 });
        BattleSession battle = world.PrepareBattleEntry(encounter, 0, 41, BattleAtbMode.TurnBased).Battle;
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-0");
        BattleCombatant original = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-0" && actor.IsActive);
        BattleCombatant added = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-8" && actor.IsActive);
        for (int frame = 0; frame < 600 && !battle.NativeEntryReady; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        foreach (BattleCombatant enemy in battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive))
        {
            enemy.NativeStatus1 |= stop;
            enemy.NativeStatusTimers[3] = 3000;
        }

        int remaining = stockCount;
        foreach (BattleCombatant target in new[] { original, added, added, original })
        {
            Assert.True(target.IsActive && target.CurrentHp > 0, target.Id);
            caster.Atb = BattleSession.AtbMaximum;
            int firstEvent = battle.Events.Count;
            battle.QueueAction(caster.Id, target.Id, BattleAbility.Magic, water);
            Assert.True(caster.IsBusy);
            bool reachedWaterCallback = false;
            for (int frame = 0; frame < 600 && caster.IsBusy; frame++)
            {
                battle.Tick();
                reachedWaterCallback |= battle.Presentation?.Graphics.OriginalEffect is { Binding.Effect: 222 } effect && effect.Pools.Any(pool => pool.Slots.Any(task => task.Callback == NativeBattleWater222Controller.Callback && (task.Flags & 1) != 0));
            }

            Assert.True(reachedWaterCallback, target.Id);
            Assert.False(caster.IsBusy);
            Assert.Null(battle.Presentation?.ActiveActionToken);
            BattleEvent[] events = battle.Events.Skip(firstEvent).Where(item => item.ActorId == caster.Id).ToArray();
            Assert.Equal(target.Id, Assert.Single(events, item => item.Kind == "EffectContact").TargetId);
            Assert.Single(events, item => item.Kind == "ActionComplete");
            remaining--;
            Assert.Equal(remaining, Assert.Single(caster.MagicStocks, item => item.Id == water).Count);
        }
    }
}
