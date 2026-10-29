using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeCallbackQueueTests
{
    [Fact]
    public void InitialAiEffectPrecedesDeferredGuardianExpansionAndRestoresTheRemainingCallback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreateBattle(assets);
        NativeBattleRandomState random = battle.Capture().Random!;

        battle.Tick();

        Assert.Null(battle.ActiveAction);
        BattleAttackCommand fact = Assert.Single(battle.QueuedActions, command => command.NativeHeaderIndex is not null);
        Assert.Equal((byte)8, fact.NativeQueuedCommand);
        Assert.Equal(100, fact.ActionId);
        Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(new BattleNativeCallback(0, 7), Assert.Single(battle.QueuedActions, command => command.NativeCallback is not null).NativeCallback);
        Assert.Equal(JsonSerializer.Serialize(random), JsonSerializer.Serialize(battle.Capture().Random));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 128 && battle.ActiveAction?.NativeQueuedCommand != 245; frame++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal((byte)245, battle.ActiveAction!.NativeQueuedCommand);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(new[] { "enemy", "party-0" }, battle.Events.Where(item => item.Kind == "Windup").Select(item => item.ActorId));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void ReleasedPhysicalSlotSkipsLaterCallbackAndStillExpandsTheFollowingGuardian()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets, additionalEnemy: true).Capture();
        NativeBattleAiDefinition initializer = saved.Definition.Combatants[1].NativeAi! with { Scripts = [[44, 0], [0], [0], [0], [0]] };
        BattleSession battle = BattleSession.Restore(saved with
        {
            Definition = saved.Definition with { Combatants = saved.Definition.Combatants.Select((actor, index) => index == 1 ? actor with { NativeAi = initializer } : actor).ToArray() },
            NativeCommandBanks = Callbacks(new("enemy", "enemy", BattleAbility.EnemyAbility) { NativeCallback = new(3, 0) }, new("enemy", "enemy", BattleAbility.EnemyAbility) { NativeCallback = new(3, 0) }, new("party-0", "party-0", BattleAbility.EnemyAbility) { NativeCallback = new(0, 7) })
        });

        battle.Tick();

        Assert.False(battle.Combatants[1].IsActive);
        Assert.True(battle.Combatants[2].IsAlive);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        for (int frame = 0; frame < 8 && !battle.QueuedActions.Any(command => command.NativeQueuedCommand == 245); frame++)
        {
            battle.Tick();
        }

        Assert.False(battle.Combatants[1].IsActive);
        Assert.NotEqual(0, battle.Capture().Combatants[1].NativeStatus!.Saved & 1);
        Assert.Null(battle.ActiveAction);
        Assert.Equal((byte)245, Assert.Single(battle.QueuedActions).NativeQueuedCommand);
        Assert.Contains(battle.Events, item => item.Kind == "Queued" && item.ActorId == "party-0" && item.Amount == 0);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Windup" && item.ActorId == "enemy");
    }

    [Fact]
    public void PeriodicAngeloCallbackReadsTheSavedSharedSelectorAndMaskOnlyWhenDispatched()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets).Capture();
        BattleSession battle = BattleSession.Restore(saved with
        {
            Definition = saved.Definition with { LimitProgress = game.ReadNewGameLimitProgress() with { AngeloFlags = 2 } },
            NativeSupport = saved.NativeSupport! with { SelectedAction = 12, SelectedTargets = 1 },
            NativeCommandBanks = Callbacks(new BattleAttackCommand("party-0", "party-0", BattleAbility.EnemyAbility) { NativeCallback = new(0, 8) })
        });
        NativeBattleRandomState random = battle.Capture().Random!;
        string json = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);

        battle.Tick();
        restored.Tick();

        Assert.Null(battle.ActiveAction);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)240, queued.NativeQueuedCommand);
        Assert.Equal(12, queued.ActionId);
        Assert.Equal((ushort)1, queued.NativeTargetMask);
        Assert.Equal(BattleNativePlayerReaction.AngeloPeriodic, queued.NativePlayerReaction);
        BattleNativeAction installedAction = Assert.Single(assets.NativeActions, action => action.Command == 240 && action.ActionId == 12);
        Assert.Equal((byte)32, installedAction.AttackType);
        // The saved selector is reused; preparing the native healing formula consumes hit and status draws.
        NativeBattleRandomState preparedRandom = random.Draw(out _).Draw(out _);
        Assert.Equal(JsonSerializer.Serialize(preparedRandom), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void CallbackRequiresItsScratchOwnersAndCannotAlsoContainAnEffect()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSnapshot saved = CreateBattle(assets).Capture();
        BattleAttackCommand callback = saved.NativeCommandBanks!.BoundCommands.Last();

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeSupport = saved.NativeSupport! with { SelectedAction = null } }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeCommandBanks = Callbacks(callback with { NativeCallback = new(0, 8) }) }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeCommandBanks = Callbacks(callback with { NativeTargetMask = 1 }) }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeCommandBanks = Callbacks(callback with { NativeDuel = new(1) }) }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeCommandBanks = Callbacks(callback with { NativeSlots = new(1, 2) }) }));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleScriptState>("{\"PendingInitializations\":[3]}"));
    }

    private static BattleSession CreateBattle(BattleAssets assets, bool additionalEnemy = false)
    {
        NativeBattleAiDefinition inert = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 100, AttackType = 35, Power = 0, Hit = 0, HitCount = 1, AttackFlags = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        BattleDefinition definition = new()
        {
            NativeRetainedMemory = NativeBattleTestInputs.ColdMemory,
            NativeCommandSequences = NativeBattleTestInputs.CommandSequences,
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
        if (additionalEnemy)
        {
            definition = definition with { Combatants = [.. definition.Combatants, new() { Id = "remaining-enemy", Name = "Remaining enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeStatusResistances = new byte[40] }] };
        }
        BattleSnapshot initial = new BattleSession(definition, BattleAtbMode.TurnBased).Capture();
        NativeBattleAiDefinition initializer = inert with { Scripts = [[4, 200, 7, 100, 6, 0], [0], [0], [0], [0]] };
        return BattleSession.Restore(initial with
        {
            Definition = initial.Definition with { Combatants = initial.Definition.Combatants.Select((actor, index) => index == 1 ? actor with { NativeAi = initializer } : actor).ToArray() },
            NativeSupport = new(true, assets.ReadNativeSupportRules().Cooldown, 0) { SelectedAction = 0 },
            NativeCommandBanks = Callbacks(
                new("enemy", "enemy", BattleAbility.EnemyAbility) { NativeCallback = new(3, 0) },
                new("party-0", "party-0", BattleAbility.EnemyAbility) { NativeCallback = new(0, 7) }
            )
        });
    }

    private static NativeBattleCommandBanks Callbacks(params BattleAttackCommand[] callbacks)
    {
        NativeBattleCommandBanks state = NativeBattleCommandBanks.Initialize();
        foreach (BattleAttackCommand packet in callbacks)
        {
            BattleNativeCallback callback = packet.NativeCallback!;
            (state, NativeBattleCommandLocation location) = state.EnqueueCallback(0, callback.ActorIndex, callback.Stage);
            state = state.BindCommand(location, 0, packet);
        }
        return state;
    }
}
