using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class ExpandedAiEnemyScanTests
{
    [Fact]
    public void InstalledTriPointScanPublishesItsClassOneContactAcrossJsonRestore()
    {
        const int formation = 377;
        const int model = 30;
        const int level = 40;
        const ushort scanAction = 135;
        const string enemyId = "tri-point";
        const string targetId = "squall";
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleEnemyPlacement placement = Assert.Single(assets.LoadEncounter(formation).Enemies);
        Assert.Equal(model, placement.ModelId);
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(model, level);
        List<string> blockers = [];
        Assert.True(NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, formation, blockers) == ExpandedAiAdmission.Supported, string.Join(", ", blockers));

        BattleEnemyNativeProjection stats = assets.ReadEnemyNativeStats(model).Project(level);
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition
        {
            Id = index == 0 ? targetId : $"ally-{index}", Name = $"Ally {index}", ModelId = index, IsPlayer = true, Level = level,
            CurrentHp = index == 0 ? 9999 : 0, MaximumHp = 9999, NativeStatus0 = index == 0 ? (ushort)0 : (ushort)1, Speed = 1
        }).ToArray();
        BattleCombatantDefinition enemy = new()
        {
            Id = enemyId, Name = assets.ReadEnemyName(model), ModelId = model, SceneSlot = placement.Slot, Level = level,
            CurrentHp = stats.MaximumHp, MaximumHp = stats.MaximumHp, Attack = stats.Strength, Defense = stats.Vitality,
            Magic = stats.Magic, Spirit = stats.Spirit, Speed = stats.Speed, ElementDefenses = stats.ElementDefenses.Select(value => (int)value).ToArray(),
            NativeAi = ai, NativeCombatantFlags = assets.ReadEnemyNativeCombatantFlags(model), InitialAtb = BattleSession.AtbMaximum
        };
        IEnumerable<BattleCombatantDefinition> inactiveEnemies = Enumerable.Range(1, 10).Select(index => new BattleCombatantDefinition
        {
            Id = $"enemy-{index}", Name = $"Enemy {index}", CurrentHp = 0, NativeStatus0 = 1
        });
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true, EncounterId = formation, Combatants = [.. party, enemy, .. inactiveEnemies],
            NativeActions = assets.NativeActions.ToArray(), NativeStatusRules = assets.ReadNativeStatusRules(0), InitialRandom = NativeBattleRandomState.ForecastPhase(3)
        }, BattleAtbMode.Normal);

        for (int tick = 0; tick < 3000 && battle.ActiveAction?.NativeAction?.ActionId != scanAction && !battle.QueuedActions.Any(command => command.NativeAction?.ActionId == scanAction); tick++)
        {
            battle.Tick();
        }

        if (battle.ActiveAction is { NativeAction.ActionId: scanAction } active)
        {
            Assert.Equal((byte)1, active.NativeAiCallbackStage);
            Assert.Equal((byte)11, active.NativeSequence);
            Assert.Equal(targetId, Assert.Single(Assert.IsType<BattleAiRecipients>(active.AiRecipients).ActorIds));
            Assert.Equal((ushort)68, active.NativeAction.MagicId);
            Assert.Equal((byte)1, active.NativeAction.AttackFlags);
        }
        else
        {
            BattleAttackCommand queued = Assert.Single(battle.QueuedActions, command => command.NativeAction?.ActionId == scanAction);
            Assert.Equal((byte)1, queued.NativeAiCallbackStage);
            Assert.Equal((byte)11, queued.NativeSequence);
            Assert.Equal(targetId, Assert.Single(Assert.IsType<BattleAiRecipients>(queued.AiRecipients).ActorIds));
            Assert.Equal((ushort)68, queued.NativeAction!.MagicId);
            Assert.Equal((byte)1, queued.NativeAction.AttackFlags);
        }

        BattleSnapshot before = battle.Capture();
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
        int beforeEvents = battle.Events.Count;
        for (int tick = 0; tick < 1000 && !battle.Events.Skip(beforeEvents).Any(entry => entry.Kind == "ActionComplete" && entry.ActorId == enemyId); tick++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Single(battle.Events.Skip(beforeEvents), entry => entry.Kind == "ActionComplete" && entry.ActorId == enemyId);
        BattleEvent contact = Assert.Single(battle.Events.Skip(beforeEvents), entry => entry.Kind == "EffectContact" && entry.ActorId == enemyId && entry.TargetId == targetId);
        Assert.Equal(0, contact.Amount);
        BattleSnapshot after = battle.Capture();
        BattleCombatantSnapshot targetBefore = Assert.Single(before.Combatants, actor => actor.Id == targetId);
        BattleCombatantSnapshot targetAfter = Assert.Single(after.Combatants, actor => actor.Id == targetId);
        Assert.Equal(targetBefore.CurrentHp, targetAfter.CurrentHp);
        Assert.Equal((byte)1, targetAfter.NativeParticipant!.LastAction!.AttackType);
        Assert.Equal((byte)8, targetAfter.NativeParticipant.LastAction.Command);
        Assert.Equal((byte)scanAction, targetAfter.NativeParticipant.LastAction.ActionId);
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(restored.Capture()));
    }
}
