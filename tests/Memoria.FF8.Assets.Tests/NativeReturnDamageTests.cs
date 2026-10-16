using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeReturnDamageTests
{
    [Fact]
    public void SynchronousReturnFactsAndPresentationReferencesFollowThePublishedHeaderOrder()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 0, twice: true);
        Until(battle, () => battle.NativeActionPackets.ActionCount != 0);

        Assert.Equal((byte)4, battle.NativeActionPackets.ActionCount);
        Assert.Equal(new byte[] { 8, 0, 8, 0 }, battle.NativeActionPackets.Headers.Take(4).Select(header => header.ReadByte(1)));
        Assert.Null(battle.ActiveAction);
        Assert.Equal(new int?[] { 0, 1, 2, 3 }, battle.QueuedActions.Select(command => command.NativeHeaderIndex));
        Assert.Equal(9500, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)0, battle.Capture().Combatants[1].NativeParticipant!.ReturnDamage);
        Assert.Equal(new[] { "party", "enemy", "party", "enemy" }, battle.Events.Where(item => item.Kind == "Damage").Select(item => item.TargetId));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int tick = 0; tick < 100; tick++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(4, battle.Events.Count(item => item.Kind == "Damage"));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(0, 250)]
    [InlineData(65530, 248)]
    public void EnemyAiCompletionAggregatesTheWrappedWordAndReturnsAQuarterOnceAcrossRestore(ushort before, int returned)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, before);
        Until(battle, () => battle.NativeActionPackets.ActionCount != 0);
        BattleSnapshot saved = battle.Capture();
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativeQueuedCommand == 0);
        Assert.Equal((byte)0, queued.NativeQueuedCommand);
        Assert.Equal(5, queued.ActionId);
        Assert.Equal((byte)1, queued.NativeAiCallbackStage);
        Assert.Equal((ushort)0, saved.Combatants[0].NativeParticipant!.ReturnDamage);
        Assert.Equal((ushort)0, saved.Combatants[1].NativeParticipant!.ReturnDamage);
        Assert.Equal(10000 - returned, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)returned, battle.NativeActionPackets.ReadContact(1).Primary.Amount6);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(saved))!);
        for (int frame = 0; frame < 40; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(10000 - returned, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)0, battle.Capture().Combatants[1].NativeParticipant!.ReturnDamage);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Return Damage:") && item.Amount == returned);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void FollowingAiInstructionObservesReturnedDamageBeforeItQueuesItsNextAttack()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 0, twice: true);
        Until(battle, () => battle.NativeActionPackets.ActionCount != 0);
        Assert.Equal(new byte?[] { 8, 0, 8, 0 }, battle.QueuedActions.Select(command => command.NativeQueuedCommand));
        Assert.All(battle.QueuedActions, command => Assert.Equal((byte)1, command.NativeAiCallbackStage));
        Assert.Equal(77u, battle.Capture().NativeAiVariables![0]);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(65530, 994)]
    public void ActualDeathCallbackCollectsWrappedPartyWordsButDoesNotIssueReturnDamage(ushort before, ushort accumulated)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, before, deathCallback: true);
        battle.QueueAction("party", "enemy", BattleAbility.Magic, 1);
        Until(battle, () => battle.Capture().Combatants[1].NativeParticipant!.DeathHandled);
        Assert.Equal((byte)3, Assert.Single(battle.QueuedActions, command => command.NativeQueuedCommand == 8).NativeAiCallbackStage);
        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)0, battle.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        Assert.Equal(accumulated, battle.Capture().Combatants[1].NativeParticipant!.ReturnDamage);
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativeQueuedCommand == 0 && command.ActionId == 5);
        Assert.DoesNotContain(battle.Events, item => item.Message.StartsWith("Return Damage:", StringComparison.Ordinal));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 40; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(accumulated, battle.Capture().Combatants[1].NativeParticipant!.ReturnDamage);
        Assert.DoesNotContain(battle.Events, item => item.Message.StartsWith("Return Damage:", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void UnknownOldWordIsPreservedAndFailsWhenThePassiveNeedsIt()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = Create(game, assets, 0).Capture();
        BattleSession missingParticipant = BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = null }, saved.Combatants[1]] });
        Assert.Null(missingParticipant.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        BattleSession battle = BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = saved.Combatants[0].NativeParticipant! with { ReturnDamage = null } }, saved.Combatants[1]] });
        Assert.Null(battle.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        Assert.Contains("Return Damage", Assert.Throws<NotSupportedException>(() => Until(battle, () => battle.NativeActionPackets.ActionCount != 0)).Message);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, ushort before, bool twice = false, bool deathCallback = false)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { Level = 100, NativeStatusPassiveFlags = 8 };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        BattleNativePlayerStatistics stats = rules.Project(Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), growth);
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = 29, Power = 0, HitCount = 1, AttackFlags = 3, Element = 0, Statuses0 = 0, Statuses1 = 0 };
        NativeBattleAiAbility ability = new(8, 0, attack.ActionId);
        byte[] turn = twice ? [4, 0, 12, 0, 2, 0, 200, 1, 0x10, 0x27, 3, 0, 15, 96, 77, 12, 0, 0] : [4, 0, 12, 0, 0];
        NativeBattleAiDefinition program = new([[0], deathCallback ? [0] : turn, [0], deathCallback ? turn : [0], [0]], Enumerable.Repeat(ability, 48).ToArray(), 0, []);
        BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1);
        BattleNativeAction lethal = spell.NativeAction! with { AttackType = 35, Power = 0, HitCount = 1, AttackFlags = 3, Element = 0, Statuses0 = 0, Statuses1 = 0 };
        BattleSession initial = new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = [.. assets.NativeActions, attack],
            ActionDefinitions = deathCallback ? [spell with { NativeAction = lethal }] : [],
            Combatants =
            [
                new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, InitialAtb = deathCallback ? BattleSession.AtbMaximum : 0, Abilities = deathCallback ? [BattleAbility.Attack, BattleAbility.Magic] : [BattleAbility.Attack], MagicStocks = deathCallback ? [new(1, 1)] : [], NativePlayerStatRules = rules, NativeGrowth = growth, NativeKillCount = 0, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = deathCallback ? 1 : 10000, CurrentHp = deathCallback ? 1 : 10000, InitialAtb = deathCallback ? 0 : BattleSession.AtbMaximum, NativeCombatantFlags = deathCallback ? 0x20u : 0u, NativeAi = program, Level = 1, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
        BattleSnapshot saved = initial.Capture();
        return BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = saved.Combatants[0].NativeParticipant! with { ReturnDamage = before } }, saved.Combatants[1]] });
    }

    private static void Until(BattleSession battle, Func<bool> condition)
    {
        for (int tick = 0; tick < 100 && !condition(); tick++)
        {
            battle.Tick();
        }
        Assert.True(condition());
    }
}
