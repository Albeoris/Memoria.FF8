using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeReturnDamageTests
{
    [Theory]
    [InlineData(0, 1000, 250)]
    [InlineData(65530, 994, 248)]
    public void EnemyAiCompletionAggregatesTheWrappedWordAndReturnsAQuarterOnceAcrossRestore(ushort before, ushort accumulated, int returned)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, before);
        for (int frame = 0; frame < 13; frame++)
        {
            battle.Tick();
        }
        BattleSnapshot saved = battle.Capture();
        BattleAttackCommand queued = Assert.Single(saved.QueuedActions);
        Assert.Equal((byte)0, queued.NativeQueuedCommand);
        Assert.Equal(5, queued.ActionId);
        Assert.Null(queued.NativeAiCallbackStage);
        Assert.Equal((ushort)0, saved.Combatants[0].NativeParticipant!.ReturnDamage);
        Assert.Equal(accumulated, saved.Combatants[1].NativeParticipant!.ReturnDamage);
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
    public void ReturnCommandPrecedesTheFollowingCommandFromTheSameAiCallback()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, 0, twice: true);
        for (int frame = 0; frame < 13; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(new byte?[] { 0, 8 }, battle.QueuedActions.Select(command => command.NativeQueuedCommand));
        Assert.Equal((byte)1, battle.QueuedActions[1].NativeAiCallbackStage);
    }

    [Fact]
    public void DeathCallbackCollectsAndClearsPartyWordsButDoesNotIssueReturnDamage()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession initial = Create(game, assets, 0);
        initial.Tick();
        BattleSnapshot saved = initial.Capture();
        BattleSession battle = BattleSession.Restore(saved with { ActiveAction = saved.ActiveAction! with { NativeAiCallbackStage = 3 } });
        for (int frame = 0; frame < 12; frame++)
        {
            battle.Tick();
        }
        Assert.Empty(battle.QueuedActions);
        Assert.Equal((ushort)0, battle.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        Assert.Equal((ushort)1000, battle.Capture().Combatants[1].NativeParticipant!.ReturnDamage);
    }

    [Fact]
    public void UnknownOldWordIsPreservedAndFailsWhenThePassiveNeedsIt()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = Create(game, assets, 0).Capture();
        BattleSession missingParticipant = BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = null }, saved.Combatants[1]] });
        Assert.Null(missingParticipant.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        BattleSession battle = BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = saved.Combatants[0].NativeParticipant! with { ReturnDamage = null } }, saved.Combatants[1]] });
        Assert.Null(battle.Capture().Combatants[0].NativeParticipant!.ReturnDamage);
        for (int frame = 0; frame < 12; frame++)
        {
            battle.Tick();
        }
        Assert.Contains("Return Damage", Assert.Throws<NotSupportedException>(() => battle.Tick()).Message);
    }

    private static BattleSession Create(GameAssets game, BattleAssets assets, ushort before, bool twice = false)
    {
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { Level = 100, NativeStatusPassiveFlags = 8 };
        BattleNativeCharacterGrowth growth = new(0, ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0));
        BattleNativePlayerStatistics stats = rules.Project(Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), growth);
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 1000, AttackType = 29, Power = 0, HitCount = 1, AttackFlags = 3, Element = 0, Statuses0 = 0, Statuses1 = 0 };
        NativeBattleAiAbility ability = new(8, 0, attack.ActionId);
        byte[] turn = twice ? [4, 0, 12, 0, 12, 0, 0] : [4, 0, 12, 0, 0];
        NativeBattleAiDefinition program = new([[0], turn, [0], [0], [0]], Enumerable.Repeat(ability, 48).ToArray(), 0, []);
        BattleSession initial = new(new BattleDefinition
        {
            NativeActions = [.. assets.NativeActions, attack],
            Combatants =
            [
                new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, MaximumHp = stats.MaximumHp, CurrentHp = stats.MaximumHp, NativePlayerStatRules = rules, NativeGrowth = growth, NativeDeathCount = 0, Speed = 1, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, InitialAtb = BattleSession.AtbMaximum, NativeAi = program, Level = 1, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
        BattleSnapshot saved = initial.Capture();
        return BattleSession.Restore(saved with { Combatants = [saved.Combatants[0] with { NativeParticipant = saved.Combatants[0].NativeParticipant! with { ReturnDamage = before } }, saved.Combatants[1]] });
    }
}
