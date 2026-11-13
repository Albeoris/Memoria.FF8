using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private static EnemyRouteResult? ProbeOriginalSlotRoutes(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts)
    {
        var candidates = scripts.Where(script => script.Callback <= 3 && script.NativeAction is not null)
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled).Select(scene => (Script: script, Scene: scene)))
            .GroupBy(route => route.Scene.FormationId)
            .Where(routes => routes.Any(route =>
            {
                NativeBattleAiDefinition ai = assets.ReadEnemyAi(route.Script.ModelId, 0) with { LevelBand = route.Script.LevelBand };
                List<string> models = [];
                ExpandedAiAdmission ordinary = NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, routes.Key, explicitModelReferences: models);
                return (ordinary == ExpandedAiAdmission.RequiresOriginalSlots || models.Count != 0 || route.Scene.Hidden || route.Scene.Suspended || route.Scene.Untargetable) &&
                    NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, routes.Key, originalEnemySlots: true) == ExpandedAiAdmission.Supported;
            }))
            .SelectMany(routes => routes.GroupBy(route => route.Script.LevelBand).Select(band => (Formation: routes.Key, Band: band.Key, Scripts: band.Select(route => route.Script).Distinct().ToArray())))
            .Take(6).ToArray();
        foreach (var route in candidates)
        {
            ushort formation = checked((ushort)route.Formation);
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false, prepareHigherBand: route.Band == HigherEnemyBand, useAreaEnemyRoster: true);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            if (!battle.UsesExpandedRoster || !battle.ReadDefinition().UsesOriginalEnemySlots)
            {
                continue;
            }
            BattleEncounter encounter = assets.LoadEncounter(formation);
            Assert.All(battle.ReadDefinition().Combatants.Where(actor => !actor.IsPlayer && actor.InitialActive), actor =>
                Assert.Contains(encounter.Enemies, original => original.Slot == actor.SceneSlot && original.ModelId == actor.ModelId));
            Dictionary<string, EnemyScenarioScriptedAction[]> owners = [];
            foreach (BattleCombatant actor in battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.NativeAi is not null))
            {
                EnemyScenarioScriptedAction[] matched = route.Scripts.Where(script => script.ModelId == actor.ModelId && script.LevelBand == actor.NativeAi!.LevelBand).ToArray();
                if (matched.Length > 0)
                {
                    owners.Add(actor.Id, matched);
                }
            }
            if (owners.Count == 0)
            {
                continue;
            }
            if (owners.Values.Any(matched => matched.Any(script => script.Callback <= 1)))
            {
                world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.Normal));
                EnemyRouteResult observed = ObserveEnemyCast(world, battle, effect, owners);
                if (observed.Status is "logical" or "native")
                {
                    return observed with { Detail = $"Formation {formation}, original slots, {observed.Detail}" };
                }
                if (battle.Outcome != BattleOutcome.Active || world.Battle is null)
                {
                    continue;
                }
                world.ApplyBattleInput(new(BattleInputKind.SetMode, Mode: BattleAtbMode.TurnBased));
            }
            foreach (BattleCombatant owner in battle.Combatants.Where(actor => owners.ContainsKey(actor.Id) && actor.IsAlive && !actor.Untargetable).ToArray())
            {
                EnemyScenarioScriptedAction[] authored = owners[owner.Id];
                EnemyRouteResult? observed = authored.Any(script => script.Callback == 3)
                    ? ObserveEnemyDeath(world, battle, effect, owner, authored)
                    : authored.Any(script => script.Callback == 2) ? ObserveEnemyReaction(world, battle, effect, owner, authored, oilboyle: false) : null;
                if (observed is { Status: "logical" or "native" })
                {
                    return observed with { Detail = $"Formation {formation}, original slots, {observed.Detail}" };
                }
            }
        }
        return null;
    }
}
