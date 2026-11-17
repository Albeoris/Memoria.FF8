using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCoverTests
{
    [Theory]
    [InlineData(0, 1, false, 3)]
    [InlineData(1, 0, false, 3)]
    [InlineData(2, 1, false, 3)]
    [InlineData(0, 1, true, 3)]
    [InlineData(1, 0, true, 4)]
    [InlineData(0, 3, true, 4)]
    [InlineData(3, 1, true, 4)]
    public void SinglePhysicalEnemyHitUsesTheEligibleCoverActorAndHalfDamage(int target, int cover, bool expanded, int partySize)
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleNativeAction action = Attack(assets);
        BattleSession control = Create(game, assets, action, target, -1, expanded, partySize);
        BattleSession covered = Create(game, assets, action, target, cover, expanded, partySize);
        int originalHp = control.Combatants[target].CurrentHp;
        int coverHp = covered.Combatants[cover].CurrentHp;
        Complete(control);
        Complete(covered);
        int damage = originalHp - control.Combatants[target].CurrentHp;
        Assert.True(damage > 0);
        Assert.Equal(originalHp, covered.Combatants[target].CurrentHp);
        Assert.Equal(coverHp - damage / 2, covered.Combatants[cover].CurrentHp);
        Assert.Equal($"party{cover}", Assert.Single(covered.Events, entry => entry.Kind == "Damage").TargetId);
        if (!expanded)
        {
            NativeBattleContactHalf contact = covered.NativeActionPackets.ReadContact(new BattleContactOccurrence(0)).Primary;
            Assert.Equal(covered.ReadPresentationModelOwner($"party{cover}"), contact.Recipient);
            Assert.Equal(covered.ReadPresentationModelOwner($"party{target}"), contact.CoverSource);
        }
        Assert.Equal(control.Capture().Random!.Indexes.ToArray(), covered.Capture().Random!.Indexes.ToArray());
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void TwoEligibleFlanksPreserveTheOriginalCoverDraw(bool expanded, int partySize)
    {
        using GameAssets game = new(GameDirectory());
        using BattleAssets assets = new(GameDirectory());
        BattleNativeAction action = Attack(assets);
        BattleSession control = Create(game, assets, action, 1, -1, expanded, partySize);
        BattleSession covered = Create(game, assets, action, 1, 0, expanded, partySize, secondCover: 2);
        BattleSnapshot before = control.Capture();
        NativeBattleRandomState afterCover = before.Random!.Draw(out byte roll);
        control = BattleSession.Restore(before with { Random = afterCover });
        Complete(control);
        Complete(covered);
        Assert.Equal(roll <= 127 ? "party2" : "party0", Assert.Single(covered.Events, entry => entry.Kind == "Damage").TargetId);
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
        battle = BattleSession.Restore(snapshot with { QueuedActions = [command with { TargetRequest = NativeBattleTargetRequestAdapter.Decode(0x8001, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true) }] });
        int originalHp = battle.Combatants[0].CurrentHp;
        Complete(battle);
        Assert.True(battle.Combatants[0].CurrentHp < originalHp);
    }

    private static BattleNativeAction Attack(BattleAssets assets)
    {
        return assets.NativeActions.First(action => action.Command == 8 && action.AttackType == 1 && (action.AttackFlags & 3) == 0 && action.HitCount == 1 && action.Power is > 0 and <= 40 && action.Hit >= 100 && action.Statuses0 == 0 && action.Statuses1 == 0);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, BattleNativeAction attack, int target, int cover, bool expanded = false, int partySize = 3, int secondCover = -1)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        BattleNativePlayerStatistics stats = rules.Project(Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), growth);
        List<BattleCombatantDefinition> actors = [];
        for (int index = 0; index < partySize; index++)
        {
            actors.Add(new() { Id = $"party{index}", Name = $"Party{index}", ModelId = index, IsPlayer = true, MaximumHp = stats.MaximumHp, CurrentHp = index == target ? stats.MaximumHp / 10 : stats.MaximumHp, NativeGrowth = growth, Defense = stats.Vitality, Speed = 1, NativeDeathCount = 0, NativePlayerStatRules = rules with { NativeStatusPassiveFlags = index == cover || index == secondCover ? 0x10u : 0u }, NativeStatusResistances = new byte[40] });
        }
        actors.Add(new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Attack = 10, Speed = 1, NativeStatusResistances = new byte[40] });
        if (expanded && partySize == 3)
        {
            actors.Add(new() { Id = "donor", Name = "Donor", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] });
        }
        BattleSession battle = new(new BattleDefinition { NativeActions = assets.NativeActions.ToArray(), Combatants = actors.ToArray() }, BattleAtbMode.TurnBased);
        BattleTargetRequest request = expanded
            ? new(new BattleTargetSelection.Explicit([new BattlePartyActorReference($"party{target}")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None)
            : NativeBattleTargetRequestAdapter.Decode(checked((ushort)(1 << target)), BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true);
        BattleAttackCommand command = new("enemy", $"party{target}", request, BattleAbility.EnemyAbility, attack.ActionId) { NativeQueuedCommand = 8, NativeAction = attack };
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
