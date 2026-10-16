using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCoverTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void OriginalSinglePhysicalEnemyHitUsesTheEligiblePhysicalCoverSlotAndHalfDamage(int target, int cover)
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleNativeAction action = Attack(assets);
        BattleSession control = Create(game, assets, action, target, -1);
        BattleSession covered = Create(game, assets, action, target, cover);
        int originalHp = control.Combatants[target].CurrentHp;
        int coverHp = covered.Combatants[cover].CurrentHp;
        Complete(control);
        Complete(covered);
        int damage = originalHp - control.Combatants[target].CurrentHp;
        Assert.True(damage > 0);
        Assert.Equal(originalHp, covered.Combatants[target].CurrentHp);
        Assert.Equal(coverHp - damage / 2, covered.Combatants[cover].CurrentHp);
        Assert.Equal(control.Capture().Random!.Indexes.ToArray(), covered.Capture().Random!.Indexes.ToArray());
    }

    [Fact]
    public void IneligiblePresentMiddleSlotDoesNotFallBackToTheOtherCoverSlot()
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleSession battle = Create(game, assets, Attack(assets), 0, 2);
        int originalHp = battle.Combatants[0].CurrentHp;
        int otherHp = battle.Combatants[2].CurrentHp;
        Complete(battle);
        Assert.True(battle.Combatants[0].CurrentHp < originalHp);
        Assert.Equal(otherHp, battle.Combatants[2].CurrentHp);
    }

    [Fact]
    public void AllTargetsModifierBypassesCoverEvenWhenOnlyOneRecipientIsSelected()
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleSession battle = Create(game, assets, Attack(assets), 0, 1);
        BattleSnapshot snapshot = battle.Capture();
        BattleAttackCommand command = Assert.Single(snapshot.QueuedActions);
        battle = BattleSession.Restore(snapshot with { QueuedActions = [command with { NativeTargetMask = 0x8001 }] });
        int originalHp = battle.Combatants[0].CurrentHp;
        Complete(battle);
        Assert.True(battle.Combatants[0].CurrentHp < originalHp);
    }

    private static BattleNativeAction Attack(BattleAssets assets)
    {
        return assets.NativeActions.First(action => action.Command == 8 && action.AttackType == 1 && (action.AttackFlags & 3) == 0 && action.HitCount == 1 && action.Power is > 0 and <= 40 && action.Hit >= 100 && action.Statuses0 == 0 && action.Statuses1 == 0);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, BattleNativeAction attack, int target, int cover)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        BattleNativePlayerStatistics stats = rules.Project(Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), growth);
        List<BattleCombatantDefinition> actors = [];
        for (int index = 0; index < 3; index++)
        {
            actors.Add(new() { Id = $"party{index}", Name = $"Party{index}", ModelId = index, IsPlayer = true, MaximumHp = stats.MaximumHp, CurrentHp = index == target ? stats.MaximumHp / 10 : stats.MaximumHp, NativeGrowth = growth, Defense = stats.Vitality, Speed = 1, NativeDeathCount = 0, NativePlayerStatRules = rules with { NativeStatusPassiveFlags = index == cover ? 0x10u : 0u }, NativeStatusResistances = new byte[40] });
        }
        actors.Add(new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Attack = 10, Speed = 1, NativeStatusResistances = new byte[40] });
        BattleSession battle = new(new BattleDefinition { NativeActions = assets.NativeActions.ToArray(), Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
        BattleAttackCommand command = new("enemy", $"party{target}", BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack, NativeTargetMask = checked((ushort)(1 << target)) };
        return BattleSession.Restore(battle.Capture() with { QueuedActions = [command] });
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 13; frame++)
        {
            battle.Tick();
        }
    }

    private static string GameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle resources are required.");
        return directory;
    }
}
