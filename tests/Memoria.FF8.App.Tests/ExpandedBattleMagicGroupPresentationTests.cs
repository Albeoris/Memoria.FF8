using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleMagicGroupPresentationTests
{
    [Theory]
    [InlineData(0x20000u, 2, false)]
    [InlineData(0x40000u, 3, false)]
    [InlineData(0x20000u, 2, true)]
    public void InstalledRepeatedFirePlaysEachEffectAgainstItsSelectedRosterSlot(uint status, int count, bool reflect)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        BattleActionDefinition fire = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.Name == "Fire");
        using GameWorld world = ExpandedBattleEffectMatrixTests.StartBattle(directory, ExpandedBattleEffectMatrixTests.PrepareField(directory), 0, fire, prepareParty: records =>
        {
            NativeMagicStock[] stock = records[10].MagicInventory;
            stock[0] = new((byte)fire.Id, (byte)count);
        });
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == "party-10");
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        caster.NativeStatus1 |= status;
        string[] targets = new[] { "enemy-10", "enemy-7", "enemy-9" }[..count];
        if (reflect)
        {
            battle.Combatants.Single(actor => actor.Id == targets[0]).NativeStatus1 |= 0x80;
        }
        int before = battle.Events.Count;
        battle.QueueAction(caster.Id, targets[0], BattleAbility.Magic, fire.Id, magicTargets: targets);

        List<NativeBattleEffectPresentation> presentations = [];
        for (int frame = 0; frame < 2048 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.ExpandedEffect is { } effect && !presentations.Contains(effect))
            {
                Assert.Equal(fire.NativeAction!.MagicId, effect.Graphics.OriginalEffect?.RequestedEffect);
                Assert.Throws<NotSupportedException>(() => battle.Capture());
                presentations.Add(effect);
            }
        }

        Assert.False(caster.IsBusy);
        Assert.Null(battle.ExpandedEffect);
        Assert.Equal(count + (reflect ? 1 : 0), presentations.Count);
        Assert.All(presentations, effect => Assert.True(effect.Complete));
        BattleEvent[] events = battle.Events.Skip(before).Where(entry => entry.ActorId == caster.Id).ToArray();
        string?[] recipients = events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId).ToArray();
        if (reflect)
        {
            Assert.StartsWith("party-", recipients[1]);
            Assert.Single(events, entry => entry.Kind == "Reflect" && entry.TargetId == targets[0]);
            recipients = [recipients[0], .. recipients.Skip(2)];
        }
        Assert.Equal(targets, recipients);
        Assert.Single(events, entry => entry.Kind == "ActionComplete");
        Assert.Empty(caster.MagicStocks);
        Assert.Empty(battle.Capture().MagicVitals!.MagicSystem.Reservations);
    }
}
