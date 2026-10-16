using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeCallbackQueueTests
{
    [Fact]
    public void InitialAiEffectPrecedesDeferredGuardianExpansionAndRestoresTheRemainingCallback()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(assets);
        NativeBattleRandomState random = battle.Capture().Random!;

        battle.Tick();

        Assert.Equal((byte)8, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Equal(100, battle.ActiveAction.ActionId);
        Assert.Equal(new BattleNativeCallback(0, 7), Assert.Single(battle.QueuedActions).NativeCallback);
        Assert.Equal(random, battle.Capture().Random);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 60 && battle.ActiveAction?.NativeQueuedCommand != 245; frame++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal((byte)245, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(new[] { "enemy", "party-0" }, battle.Events.Where(item => item.Kind == "Windup").Select(item => item.ActorId));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ReleasedPhysicalSlotSkipsLaterCallbackAndStillExpandsTheFollowingGuardian()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets).Capture();
        NativeBattleAiDefinition initializer = saved.Definition.Combatants[1].NativeAi! with { Scripts = [[44, 0], [0], [0], [0], [0]] };
        BattleSession battle = BattleSession.Restore(saved with
        {
            Definition = saved.Definition with { Combatants = [saved.Definition.Combatants[0], saved.Definition.Combatants[1] with { NativeAi = initializer }] },
            QueuedActions = [saved.QueuedActions[0], saved.QueuedActions[0], saved.QueuedActions[1]]
        });

        battle.Tick();

        Assert.False(battle.Combatants[1].IsActive);
        Assert.NotEqual(0, battle.Capture().Combatants[1].NativeStatus!.Saved & 1);
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
        Assert.Contains(battle.Events, item => item.Kind == "Queued" && item.ActorId == "party-0" && item.Amount == 0);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Windup" && item.ActorId == "enemy");
    }

    [Fact]
    public void PeriodicAngeloCallbackReadsTheSavedSharedSelectorAndMaskOnlyWhenDispatched()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets).Capture();
        BattleSession battle = BattleSession.Restore(saved with
        {
            Definition = saved.Definition with { LimitProgress = game.ReadNewGameLimitProgress() with { AngeloFlags = 2 } },
            NativeSupport = saved.NativeSupport! with { SelectedAction = 12, SelectedTargets = 1 },
            QueuedActions = [new("party-0", "party-0", BattleAbility.EnemyAbility) { NativeCallback = new(0, 8) }]
        });
        NativeBattleRandomState random = battle.Capture().Random!;
        string json = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);

        battle.Tick();
        restored.Tick();

        Assert.Equal((byte)240, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Equal(12, battle.ActiveAction.ActionId);
        Assert.Equal((ushort)1, battle.ActiveAction.NativeTargetMask);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, battle.ActiveAction.NativePlayerReaction);
        Assert.Equal(random, battle.Capture().Random);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void CallbackRequiresItsScratchOwnersAndCannotAlsoContainAnEffect()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets).Capture();
        BattleAttackCommand callback = saved.QueuedActions[1];

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeSupport = saved.NativeSupport! with { SelectedAction = null } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { QueuedActions = [callback with { NativeCallback = new(0, 8) }] }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { QueuedActions = [callback with { NativeTargetMask = 1 }] }));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleScriptState>("{\"PendingInitializations\":[3]}"));
    }

    private static BattleSession CreateBattle(BattleAssets assets)
    {
        NativeBattleAiDefinition inert = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 100, AttackType = 35, Power = 0, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleDefinition definition = new()
        {
            NativeFieldBattleRules = 0,
            NativeSavedBattleFlagsKnownMask = 255,
            NativeActions = [.. assets.NativeActions.Where(action => action.Command != 8 || action.ActionId != 100), attack],
            ActionDefinitions = assets.ActionDefinitions.Where(action => action.NativeAction?.Command != 8 || action.NativeAction.ActionId != 100).ToArray(),
            NativeSupportRules = assets.ReadNativeSupportRules(),
            GuardianForces = [new(0, 100, 100)],
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", IsPlayer = true, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", NativeAi = inert, MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        };
        BattleSnapshot initial = new BattleSession(definition, BattleAtbMode.TurnBased).Capture();
        NativeBattleAiDefinition initializer = inert with { Scripts = [[4, 200, 7, 100, 6, 0], [0], [0], [0], [0]] };
        return BattleSession.Restore(initial with
        {
            Definition = initial.Definition with { Combatants = [initial.Definition.Combatants[0], initial.Definition.Combatants[1] with { NativeAi = initializer }] },
            NativeSupport = new(true, assets.ReadNativeSupportRules().Cooldown, 0) { SelectedAction = 0 },
            QueuedActions =
            [
                new("enemy", "enemy", BattleAbility.EnemyAbility) { NativeCallback = new(3, 0) },
                new("party-0", "party-0", BattleAbility.EnemyAbility) { NativeCallback = new(0, 7) }
            ]
        });
    }
}
