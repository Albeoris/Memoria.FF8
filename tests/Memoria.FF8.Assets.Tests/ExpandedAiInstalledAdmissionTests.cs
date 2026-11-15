using System.Text.Json;

namespace Memoria.FF8.Assets.Tests;

using Memoria.FF8.Runtime;

public sealed class ExpandedAiInstalledAdmissionTests
{
    [Theory]
    [InlineData(89, 18, ExpandedAiAdmission.Supported)]
    [InlineData(236, 56, ExpandedAiAdmission.RequiresOriginalSlots)]
    [InlineData(237, 56, ExpandedAiAdmission.RequiresOriginalSlots)]
    [InlineData(344, 95, ExpandedAiAdmission.Supported)]
    [InlineData(371, 95, ExpandedAiAdmission.Supported)]
    [InlineData(448, 95, ExpandedAiAdmission.Supported)]
    [InlineData(478, 95, ExpandedAiAdmission.Supported)]
    [InlineData(0, 71, ExpandedAiAdmission.RequiresOriginalSlots)]
    public void InstalledScriptsUseFormationConditionsAndPreserveAllocationBoundaries(int formation, int model, ExpandedAiAdmission expected)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        Assert.Contains(assets.LoadEncounter(formation).Enemies, enemy => enemy.ModelId == model);
        NativeBattleAiDefinition program = assets.ReadEnemyAi(model, 8);
        List<string> blockers = [];
        Assert.Equal(expected, NativeBattleExpandedAi.Evaluate(program, assets.NativeActions, formation, blockers));
        if (model == 71)
        {
            Assert.Contains("physical-variable:14/96", blockers);
            Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, assets.NativeActions, formation, originalEnemySlots: true));
        }
    }

    [Theory]
    [InlineData(194, 102, 8, 119, 14, 298)]
    [InlineData(247, 57, 32, 132, 12, 0)]
    public void OriginalZeroDamageCommandsPublishTheirAuthoredSelfContactAcrossPrivateRestore(int formation, int model, int level, ushort action, byte sequence, ushort effect)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEnemyPlacement placement = Assert.Single(assets.LoadEncounter(formation).Enemies);
        Assert.Equal(model, placement.ModelId);
        byte partyLevel = model == 102 ? (byte)10 : (byte)40;
        Assert.Equal(level, NativeBattleEnemyLevelRules.ResolveExpanded((byte)placement.Level, [partyLevel], 0));
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(model, level);
        List<string> blockers = [];
        Assert.True(NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, formation, blockers) == ExpandedAiAdmission.Supported, string.Join(", ", blockers));

        bool deathCallback = action == 119;
        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(model).Project(level);
        int startingHp = deathCallback ? 1 : stats.MaximumHp / 4;
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = $"ally-{index}", Name = $"Ally {index}", ModelId = index, IsPlayer = true, Level = partyLevel,
            CurrentHp = 1000, MaximumHp = 1000, Attack = 100, Speed = 1, Hit = 255,
            InitialAtb = deathCallback && index == 10 ? BattleSession.AtbMaximum : 0
        }).ToArray();
        BattleCombatantDefinition enemy = new()
        {
            Id = "enemy", Name = assets.ReadEnemyName(model), ModelId = model, SceneSlot = placement.Slot, Level = level,
            CurrentHp = startingHp, MaximumHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality, Magic = stats.Magic, Spirit = stats.Spirit, Speed = 1,
            NativeAi = ai, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(model), NativeStatus1 = deathCallback ? 0u : 0x800000u,
            InitialAtb = deathCallback ? 0 : BattleSession.AtbMaximum
        };
        IEnumerable<BattleCombatantDefinition> remainingEnemies = Enumerable.Range(1, 10).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", CurrentHp = 0, NativeStatus0 = 1
        });
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true, EncounterId = formation, Combatants = [.. party, enemy, .. remainingEnemies],
            NativeActions = assets.NativeActions.ToArray(), NativeStatusRules = assets.ReadNativeStatusRules(0), InitialRandom = NativeBattleRandomState.ForecastPhase(3)
        }, BattleAtbMode.TurnBased);
        Assert.Equal(new BattleExpandedAiCallbackRequest(new BattleEnemyActorReference(enemy.Id), 0), Assert.Single(battle.QueuedActions).ExpandedAiCallback);
        if (deathCallback)
        {
            battle.QueueAttack("ally-10", enemy.Id);
        }

        Until(battle, () => battle.ActiveAction?.NativeAction?.ActionId == action || battle.QueuedActions.Any(command => command.NativeAction?.ActionId == action));
        byte expectedStage = deathCallback ? (byte)3 : (byte)1;
        BattleTargetRequest request;
        BattleNativeAction native;
        if (battle.ActiveAction is { NativeAction: { ActionId: var activeId } } active && activeId == action)
        {
            Assert.Equal(sequence, active.NativeSequence);
            Assert.Equal(expectedStage, active.NativeAiCallbackStage);
            request = active.TargetRequest;
            native = active.NativeAction!;
        }
        else
        {
            BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
            Assert.Equal(sequence, queued.NativeSequence);
            Assert.Equal(expectedStage, queued.NativeAiCallbackStage);
            request = queued.TargetRequest;
            native = Assert.IsType<BattleNativeAction>(queued.NativeAction);
        }
        Assert.Equal(enemy.Id, Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(request.Selection).Actors).ActorId);
        Assert.Equal(deathCallback ? enemy.Id : null, request.DeathCallbackSelf?.ActorId);
        Assert.Equal((byte)8, native.Command);
        Assert.Equal((byte)0, native.AttackType);
        Assert.Equal((byte)0, native.AttackFlags);
        Assert.Equal(effect, native.MagicId);

        BattleSnapshot before = battle.Capture();
        if (!deathCallback)
        {
            BattleCombatantSnapshot charged = Assert.Single(before.Combatants, actor => actor.Id == enemy.Id);
            Assert.Equal((byte)1, charged.NativeParticipant!.TurnCount);
            Assert.Equal(1u, charged.NativeParticipant.Variables[0]);
            Assert.Equal(0x800000u, charged.NativeStatus!.Battle & 0x800000u);
        }
        BattleSession restored = BattleSession.Restore(NativeBattleStorageSnapshots.Capture(before));
        for (int frame = 0; frame < 100 && !battle.Events.Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == enemy.Id); frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete" && entry.ActorId == enemy.Id);
        BattleEvent contact = Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == enemy.Id && entry.TargetId == enemy.Id);
        Assert.Equal(0, contact.Amount);
        Assert.Equal(deathCallback ? BattleOutcome.Victory : BattleOutcome.Active, battle.Outcome);
        BattleSnapshot after = battle.Capture();
        BattleCombatantSnapshot finished = Assert.Single(after.Combatants, actor => actor.Id == enemy.Id);
        Assert.Equal(deathCallback ? 0 : startingHp, finished.CurrentHp);
        Assert.Equal(0u, finished.NativeStatus!.Battle & 0x800000u);
        Assert.Equal((byte)0, finished.NativeParticipant!.LastAction!.AttackType);
        Assert.Equal((byte)8, finished.NativeParticipant.LastAction.Command);
        Assert.Equal((byte)action, finished.NativeParticipant.LastAction.ActionId);
        Assert.Equal(before.Random!.Lane, after.Random!.Lane);
        Assert.Equal(before.Random.Indexes.AsSpan().ToArray(), after.Random.Indexes.AsSpan().ToArray());
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
    }

    private static void Until(BattleSession battle, Func<bool> reached)
    {
        for (int frame = 0; frame < 100 && !reached(); frame++)
        {
            battle.Tick();
        }
        Assert.True(reached());
    }
}
