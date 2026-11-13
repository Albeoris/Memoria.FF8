using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private sealed record EnemyRouteResult(string Status, string Detail, string? FirstBlockerStage)
    {
        public string[] AdmissionBlockers { get; init; } = [];
    }

    private static EnemyRouteResult RunDiscoveredEnemyCast(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts, bool probeEnemies)
    {
        HashSet<string> blockers = new(StringComparer.Ordinal);
        var candidates = scripts.Where(script => script.Callback is 0 or 1 && script.NativeAction is not null && (effect.Effect != 226 || script.LevelBand == 0))
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled && !scene.Hidden && !scene.Suspended && !scene.Untargetable).Select(scene => (Script: script, Scene: scene)))
            .Where(route => AdmitsEnemyScript(assets, route.Script, route.Scene, blockers))
            .GroupBy(route => route.Scene.FormationId)
            .OrderBy(routes => routes.Key)
            .Take(3).ToArray();
        if (!probeEnemies && candidates.Length > 0)
        {
            return new("blocked", $"Original enemy candidate formations {string.Join(", ", candidates.Select(routes => routes.Key))}. Set MEMORIA_FF8_EFFECT_MATRIX_PROBE_ENEMIES=1 to attempt their original callbacks.", "enemy-turn-probe-disabled");
        }
        List<string> attempted = [];
        string firstBlocker = "enemy-formation-entry";
        // Adamantoise's White Wind requires two successful random conditions in the same low-band turn.
        // Retry with independent entry seeds so unrelated AI admissions cannot hide this route by changing the RNG stream.
        IEnumerable<byte> seeds = effect.Effect == 226 ? [0, 5, 9] : [0];
        foreach (var (routes, seed) in seeds.SelectMany(seed => candidates.Select(routes => (routes, seed))))
        {
            ushort formation = checked((ushort)routes.Key);
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false, nativeSeed: seed);
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

            EnemyRouteResult witnessed = ObserveEnemyCast(world, battle, effect, actors);
            if (witnessed.Status is "native" or "logical")
            {
                return witnessed with { Detail = $"Formation {formation}, seed {seed}, {witnessed.Detail}" };
            }
            firstBlocker = witnessed.FirstBlockerStage!;
            attempted.Add($"formation {formation}, seed {seed}: {witnessed.Detail}");
        }

        if (attempted.Count > 0)
        {
            return new("blocked", string.Join("; ", attempted), firstBlocker);
        }
        if (scripts.All(script => script.NativeAction is null))
        {
            return new("blocked", "Installed enemy AI references this effect through direct graphics. The expanded host requires its original graphics callback and trigger.", "enemy-direct-graphics");
        }
        if (scripts.All(script => script.Callback > 1 || script.NativeAction is null))
        {
            return new("blocked", "Installed enemy AI maps this effect to a reaction, death or other callback. Its original trigger and expanded execution route remain unproved.", "enemy-callback-trigger");
        }
        return new("blocked", "Installed enemy AI maps this effect to initialization or turn dispatch. No matching scene owner and level band pass expanded AI admission.", "enemy-ai-admission") { AdmissionBlockers = [.. blockers.Order()] };
    }

    private static bool AdmitsEnemyScript(BattleAssets assets, EnemyScenarioScriptedAction script, EnemyScenarioFormation scene, ICollection<string>? blockers = null)
    {
        // The decoded program contains all three bands. The actual band is checked again at entry.
        NativeBattleAiDefinition ai = assets.ReadEnemyAi(script.ModelId, 0) with { LevelBand = script.LevelBand };
        if (NativeBattleExpandedAi.Evaluate(ai, assets.NativeActions, scene.FormationId, blockers) != ExpandedAiAdmission.Supported)
        {
            return false;
        }
        BattleEnemyPlacement[] enabled = assets.LoadEncounter(scene.FormationId).Slots.Where(slot => slot.Enabled).ToArray();
        bool supported = enabled.Length > 0 && enabled.All(slot => slot.IsEnemyModel && !slot.Hidden && !slot.Untargetable && !slot.InitiallySuspended);
        if (!supported)
        {
            blockers?.Add("formation-allocation");
        }
        return supported;
    }

    private static EnemyRouteResult ObserveEnemyCast(GameWorld world, BattleSession battle, NativeBattleEffectBinding effect, Dictionary<string, EnemyScenarioScriptedAction[]> actors)
    {
        int before = battle.Events.Count;
        bool dispatched = false;
        BattleActionPresentation? cast = null;
        NativeBattleEffectPresentation? presentation = null;
        HashSet<string>? recipientsAtFirstContact = null;
        for (int turn = 0; turn < 24 && battle.Outcome == BattleOutcome.Active; turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            for (int frame = 0; frame < 30 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                HashSet<string> eligibleBeforeContact = battle.Combatants.Where(actor => actor.IsActive && actor.IsAlive && !actor.Untargetable && (actor.PersistentStatus & 5) == 0).Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                BattleActionPresentation? active = battle.ActiveAction;
                if (cast is null && active is { Ability: BattleAbility.EnemyAbility, AiRecipients: not null }
                    && actors.TryGetValue(active.ActorId, out EnemyScenarioScriptedAction[]? scripts)
                    && scripts.Any(script => MatchesEnemyDispatch(active, script, effect)))
                {
                    cast = active;
                    dispatched = true;
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
                if (events.Any(entry => entry.Kind == "EffectContact" && entry.Ability == BattleAbility.EnemyAbility))
                {
                    recipientsAtFirstContact ??= eligibleBeforeContact;
                }
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
                    recipientsAtFirstContact = null;
                    before = battle.Events.Count;
                    continue;
                }
                Assert.NotNull(recipientsAtFirstContact);
                AssertEnemyContacts(battle, cast, contacts, recipientsAtFirstContact);
                Assert.True(presentation is null || presentation.Complete);
                BattleCombatant actor = Assert.Single(battle.Combatants, candidate => candidate.Id == cast.ActorId);
                return new(presentation is null ? "logical" : "native", $"c0m{actor.ModelId:D3} {actor.Id}, callback {cast.NativeAiCallbackStage}, command {cast.NativeQueuedCommand}/{cast.ActionId}, sequence {cast.NativeSequence}, {contacts.Length} contact(s)", presentation is null ? "native-presentation" : null);
            }
        }
        if (recipientsAtFirstContact is not null)
        {
            return new("blocked", "The exact dispatch reached contact but did not complete within the bounded original turns.", "enemy-turn-completion");
        }
        return dispatched
            ? new("blocked", "The exact dispatch did not reach contact within the bounded original turns.", "enemy-turn-contact")
            : new("blocked", "The exact dispatch was not observed within the bounded original turns.", "enemy-turn-dispatch");
    }

    private static bool MatchesEnemyDispatch(BattleActionPresentation active, EnemyScenarioScriptedAction script, NativeBattleEffectBinding effect)
    {
        return active.NativeAction is { } native && script.NativeAction is { } mapped && script.Callback == active.NativeAiCallbackStage &&
            script.Command == active.NativeQueuedCommand && script.ActionId == active.ActionId && script.ActionId == native.ActionId && script.Sequence == active.NativeSequence &&
            mapped.KernelSection == native.KernelSection && mapped.KernelIndex == native.KernelIndex && native.MagicId == effect.Effect;
    }

    private static void AssertEnemyContacts(BattleSession battle, BattleActionPresentation cast, BattleEvent[] contacts, HashSet<string> eligible)
    {
        string[] recipients = cast.AiRecipients!.ActorIds.Where(eligible.Contains).ToArray();
        int hitCount = cast.NativeAction!.HitCount;
        if (cast.AiRecipients.RandomSide is BattleSide side || (cast.NativeAction.TargetFlags.GetValueOrDefault() & 2) != 0)
        {
            BattleSide targetSide = cast.AiRecipients.RandomSide ?? battle.Combatants.Single(actor => actor.Id == cast.TargetId).Side;
            Assert.InRange(contacts.Length, 1, hitCount);
            Assert.All(contacts, contact => Assert.Contains(battle.Combatants, actor => actor.Id == contact.TargetId && actor.Side == targetSide && eligible.Contains(actor.Id)));
            return;
        }
        if (hitCount == 1)
        {
            Assert.Equal(recipients.Order(), contacts.Select(entry => entry.TargetId).Order());
            return;
        }

        string[] expected = Enumerable.Range(0, hitCount).SelectMany(_ => recipients).ToArray();
        Assert.InRange(contacts.Length, 1, expected.Length);
        Assert.Equal(expected.Take(contacts.Length), contacts.Select(entry => entry.TargetId));
        if (contacts.Length < expected.Length)
        {
            string interrupted = expected[contacts.Length];
            Assert.Contains(battle.Combatants, actor => actor.Id == interrupted && (!actor.IsActive || !actor.IsAlive));
        }
    }
}
