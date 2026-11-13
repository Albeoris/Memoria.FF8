using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private static (string Status, string Detail) RunDiscoveredEnemyCast(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts, bool probeEnemies)
    {
        var candidates = scripts.Where(script => script.Callback is 0 or 1 && script.NativeAction is not null)
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled && !scene.Hidden && !scene.Suspended && !scene.Untargetable).Select(scene => (Script: script, Scene: scene)))
            .Where(route => AdmitsEnemyScript(assets, route.Script, route.Scene))
            .GroupBy(route => route.Scene.FormationId)
            .OrderBy(routes => routes.Key)
            .Take(3).ToArray();
        if (!probeEnemies && candidates.Length > 0)
        {
            return ("blocked", $"Original enemy candidate formations {string.Join(", ", candidates.Select(routes => routes.Key))}. Set MEMORIA_FF8_EFFECT_MATRIX_PROBE_ENEMIES=1 to attempt their original callbacks.");
        }
        List<string> attempted = [];
        foreach (var routes in candidates)
        {
            ushort formation = checked((ushort)routes.Key);
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            if (!battle.UsesExpandedRoster)
            {
                attempted.Add($"formation {formation} retained its original roster");
                continue;
            }

            Dictionary<string, EnemyScenarioScriptedAction[]> actors = [];
            foreach (BattleCombatantDefinition actor in battle.ReadDefinition().Combatants.Where(actor => !actor.IsPlayer && actor.NativeAi is not null))
            {
                EnemyScenarioScriptedAction[] matches = routes.Select(route => route.Script)
                    .Where(script => actor.ModelId == script.ModelId && actor.NativeAi!.LevelBand == script.LevelBand).Distinct().ToArray();
                if (matches.Length > 0)
                {
                    actors.Add(actor.Id, matches);
                }
            }
            if (actors.Count == 0)
            {
                attempted.Add($"formation {formation} did not resolve to an admitted owner and level band");
                continue;
            }

            if (ObserveEnemyCast(world, battle, effect, actors) is { } witnessed)
            {
                return (witnessed.Status, $"Formation {formation}, {witnessed.Detail}");
            }
            attempted.Add($"formation {formation} did not complete the exact dispatch within the bounded original turns");
        }

        if (attempted.Count > 0)
        {
            return ("blocked", string.Join("; ", attempted));
        }
        if (scripts.All(script => script.NativeAction is null))
        {
            return ("blocked", "Installed enemy AI references this effect through direct graphics. The expanded host requires its original graphics callback and trigger.");
        }
        if (scripts.All(script => script.Callback > 1 || script.NativeAction is null))
        {
            return ("blocked", "Installed enemy AI maps this effect to a reaction, death or other callback. Its original trigger and expanded execution route remain unproved.");
        }
        return ("blocked", "Installed enemy AI maps this effect to initialization or turn dispatch. No matching scene owner and level band pass expanded AI admission.");
    }

    private static bool AdmitsEnemyScript(BattleAssets assets, EnemyScenarioScriptedAction script, EnemyScenarioFormation scene)
    {
        // The decoded program contains all three bands. The actual band is checked again at entry.
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(script.ModelId, 0) with { LevelBand = script.LevelBand };
        if (NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, scene.FormationId) != ExpandedAiAdmission.Supported)
        {
            return false;
        }
        BattleEnemyPlacement[] enabled = assets.LoadEncounter(scene.FormationId).Slots.Where(slot => slot.Enabled).ToArray();
        return enabled.Length > 0 && enabled.All(slot => slot.IsEnemyModel && !slot.Hidden && !slot.Untargetable && !slot.InitiallySuspended);
    }

    private static (string Status, string Detail)? ObserveEnemyCast(GameWorld world, BattleSession battle, NativeBattleEffectBinding effect, Dictionary<string, EnemyScenarioScriptedAction[]> actors)
    {
        int before = battle.Events.Count;
        BattleActionPresentation? cast = null;
        NativeBattleEffectPresentation? presentation = null;
        for (int turn = 0; turn < 24 && battle.Outcome == BattleOutcome.Active; turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            for (int frame = 0; frame < 30 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                BattleActionPresentation? active = battle.ActiveAction;
                if (cast is null && active is { Ability: BattleAbility.EnemyAbility, NativeAction: { } native, AiRecipients: not null }
                    && actors.TryGetValue(active.ActorId, out EnemyScenarioScriptedAction[]? scripts)
                    && scripts.Any(script => script.Callback == active.NativeAiCallbackStage && script.Command == active.NativeQueuedCommand && script.ActionId == native.ActionId && script.Sequence == active.NativeSequence
                        && script.NativeAction is { } mapped && mapped.KernelSection == native.KernelSection && mapped.KernelIndex == native.KernelIndex && native.MagicId == effect.Effect))
                {
                    cast = active;
                }
                if (cast is null)
                {
                    before = battle.Events.Count;
                    continue;
                }
                if (battle.ExpandedEffect is { Graphics.OriginalEffect: { } identity } live)
                {
                    presentation ??= live;
                    Assert.Same(presentation, live);
                    Assert.Equal(effect.Effect, identity.RequestedEffect);
                    Assert.Equal(effect.Entry, identity.Binding.Entry);
                    Assert.Equal(effect.Initializer, identity.Binding.Initializer);
                }

                BattleEvent[] events = battle.Events.Skip(before).Where(entry => entry.ActorId == cast.ActorId).ToArray();
                if (!events.Any(entry => entry.Kind == "ActionComplete"))
                {
                    continue;
                }
                Assert.Single(events, entry => entry.Kind == "Windup" && entry.Ability == BattleAbility.EnemyAbility);
                Assert.Single(events, entry => entry.Kind == "ActionComplete");
                BattleEvent[] contacts = events.Where(entry => entry.Kind == "EffectContact" && entry.Ability == BattleAbility.EnemyAbility).ToArray();
                if (contacts.Length == 0)
                {
                    // The ordinary runtime can cancel if its original recipients become ineligible during windup.
                    cast = null;
                    presentation = null;
                    before = battle.Events.Count;
                    continue;
                }
                Assert.Equal(cast.AiRecipients!.ActorIds.Order(), contacts.Select(entry => entry.TargetId).Order());
                Assert.True(presentation is null || presentation.Complete);
                BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.Id == cast.ActorId);
                return (presentation is null ? "logical" : "native", $"c0m{actor.ModelId:D3} {actor.Id}, callback {cast.NativeAiCallbackStage}, command {cast.NativeQueuedCommand}/{cast.ActionId}, sequence {cast.NativeSequence}, {contacts.Length} recipient(s)");
            }
        }
        return null;
    }
}
