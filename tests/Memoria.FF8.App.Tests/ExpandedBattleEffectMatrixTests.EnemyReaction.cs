using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    private const int OilboyleFormation = 79;

    private static bool IsOilboyleCounter(EnemyScenarioScriptedAction script)
    {
        return script is { ModelId: 99, Callback: 2, Command: 8, ActionId: 181, TargetSelector: 203, NativeAction: not null } &&
            script.Formations.Any(scene => scene.FormationId == OilboyleFormation && scene.Enabled);
    }

    private static EnemyRouteResult RunDiscoveredEnemyReaction(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts, bool probeEnemies)
    {
        if (scripts.All(script => script.NativeAction is null))
        {
            return new("blocked", "The installed callback 2 references this binding through direct graphics.", "enemy-reaction-direct-graphics");
        }

        var sceneOwners = scripts.Where(script => script.NativeAction is not null)
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled && !scene.Hidden && !scene.Suspended && !scene.Untargetable).Select(scene => (Script: script, Scene: scene)))
            .ToArray();
        if (sceneOwners.Length == 0)
        {
            return new("blocked", "The installed callback 2 has no enabled, targetable scene owner.", "enemy-reaction-scene-owner");
        }

        HashSet<string> admissionBlockers = new(StringComparer.Ordinal);
        var candidates = sceneOwners.Where(route => AdmitsEnemyScript(assets, route.Script, route.Scene, admissionBlockers))
            .GroupBy(route => route.Scene.FormationId)
            .OrderByDescending(routes => routes.Key == OilboyleFormation && routes.Any(route => IsOilboyleCounter(route.Script)))
            .ThenBy(routes => routes.Key)
            .Take(3).ToArray();
        if (candidates.Length == 0)
        {
            return new("blocked", "No enabled scene owner and level band for the installed callback 2 passed expanded AI admission.", "enemy-reaction-admission") { AdmissionBlockers = [.. admissionBlockers.Order()] };
        }

        EnemyRouteResult? blocker = null;
        List<string> attempts = [];
        foreach (var routes in candidates)
        {
            ushort formation = checked((ushort)routes.Key);
            bool oilboyle = formation == OilboyleFormation && routes.Any(route => IsOilboyleCounter(route.Script));
            if (!probeEnemies && !oilboyle)
            {
                continue;
            }

            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            EnemyRouteResult result = new("blocked", "The scene retained its original roster.", "enemy-reaction-formation-entry");
            if (battle.UsesExpandedRoster)
            {
                var owners = battle.Combatants.Reverse().Where(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable && actor.NativeAi is not null)
                    .Select(actor => (Actor: actor, Scripts: routes.Select(route => route.Script).Where(script => actor.ModelId == script.ModelId && actor.NativeAi!.LevelBand == script.LevelBand).Distinct().ToArray()))
                    .Where(owner => owner.Scripts.Length > 0)
                    .GroupBy(owner => (owner.Actor.ModelId, owner.Actor.NativeAi!.LevelBand))
                    .Select(group => group.First()).ToArray();
                result = new("blocked", "The expanded roster has no matching living scene owner and level band.", "enemy-reaction-owner");
                foreach (var owner in owners)
                {
                    bool oilboyleOwner = oilboyle && owner.Scripts.Any(IsOilboyleCounter);
                    if (oilboyle && !oilboyleOwner)
                    {
                        continue;
                    }

                    NativeBattleAiDefinition original = assets.ReadEnemyAi(owner.Actor.ModelId, owner.Actor.Level);
                    Assert.Equal(original.Scripts[2], owner.Actor.NativeAi!.Scripts[2]);
                    EnemyScenarioScriptedAction[] ownerScripts = oilboyleOwner ? owner.Scripts.Where(IsOilboyleCounter).ToArray() : owner.Scripts;
                    result = ObserveEnemyReaction(world, battle, effect, owner.Actor, ownerScripts, oilboyleOwner);
                    if (result.Status is "native" or "logical")
                    {
                        return result with { Detail = $"Formation {formation}, {result.Detail}" };
                    }
                }
            }

            blocker ??= result;
            attempts.Add($"formation {formation}: {result.Detail}");
            if (oilboyle)
            {
                return result with { Detail = string.Join("; ", attempts) };
            }
        }

        return blocker is null
            ? new("blocked", $"Callback 2 candidate formations {string.Join(", ", candidates.Select(routes => routes.Key))}. Set MEMORIA_FF8_EFFECT_MATRIX_PROBE_ENEMIES=1 to submit attacks against their original owners.", "enemy-reaction-probe-disabled")
            : blocker with { Detail = string.Join("; ", attempts) };
    }

    private static EnemyRouteResult ObserveEnemyReaction(GameWorld world, BattleSession battle, NativeBattleEffectBinding effect, BattleCombatant enemy, EnemyScenarioScriptedAction[] scripts, bool oilboyle)
    {
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        if (oilboyle)
        {
            Assert.Equal(10, attacker.Slot!.Value.Position);
            Assert.Equal(10, enemy.Slot!.Value.Position);
        }

        int hits = 0;
        int dispatches = 0;
        string lastBlocker = "enemy-reaction-trigger";
        for (int attempt = 0; attempt < 24 && battle.Outcome == BattleOutcome.Active && attacker.IsAlive && enemy.IsAlive; attempt++)
        {
            if (oilboyle)
            {
                // Oilboyle's installed callback 2 tests HP below half before its random branch.
                enemy.CurrentHp = enemy.MaximumHp / 3;
            }
            attacker.Atb = BattleSession.AtbMaximum;
            int beforeAttack = battle.Events.Count;
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = attacker.Id, TargetId = enemy.Id, Ability = BattleAbility.Attack });
            if (!attacker.IsBusy)
            {
                break;
            }
            Assert.Contains(battle.QueuedActions, command => command.ActorId == attacker.Id && command.TargetId == enemy.Id && command.Ability == BattleAbility.Attack);

            BattleActionPresentation? reaction = null;
            NativeBattleEffectPresentation? presentation = null;
            HashSet<string>? recipientsAtContact = null;
            int reactionEventStart = 0;
            for (int frame = 0; frame < 320 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                int previousEvents = battle.Events.Count;
                int enemyHp = enemy.CurrentHp;
                HashSet<string> eligible = battle.Combatants.Where(actor => actor.IsActive && actor.IsAlive && !actor.Untargetable && (actor.PersistentStatus & 5) == 0).Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (enemy.CurrentHp < enemyHp && battle.Events.Skip(previousEvents).Any(entry => entry.Kind == "Damage" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id && entry.Ability == BattleAbility.Attack))
                {
                    hits++;
                }

                if (reaction is null && battle.ActiveAction is { Ability: BattleAbility.EnemyAbility, AiRecipients: not null } active && active.ActorId == enemy.Id)
                {
                    EnemyScenarioScriptedAction[] matched = scripts.Where(script => MatchesEnemyDispatch(active, script, effect)).ToArray();
                    if (matched.Length > 0)
                    {
                        Assert.Contains(battle.Events.Skip(beforeAttack), entry => entry.Kind == "ActionComplete" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id);
                        if (oilboyle)
                        {
                            Assert.Equal(attacker.Id, enemy.LastAttackerId);
                            Assert.Equal(BattleAbility.Attack, enemy.LastAttackerAbility);
                            Assert.Contains(battle.Events.Skip(beforeAttack), entry => entry.Kind == "Damage" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id && entry.Ability == BattleAbility.Attack && entry.Amount > 0);
                        }
                        if (matched.All(script => script.TargetSelector == 203))
                        {
                            Assert.Equal(attacker.Id, active.TargetId);
                            Assert.Equal(new[] { attacker.Id }, active.AiRecipients.ActorIds);
                        }
                        reaction = active;
                        reactionEventStart = previousEvents;
                        dispatches++;
                        lastBlocker = "enemy-reaction-contact";
                    }
                }

                if (reaction is not null)
                {
                    if (battle.ExpandedEffect is { Graphics.OriginalEffect: { } identity } live)
                    {
                        presentation ??= live;
                        Assert.Same(presentation, live);
                        Assert.Equal(effect.Effect, identity.RequestedEffect);
                        Assert.Equal(effect.Entry, identity.Binding.Entry);
                        Assert.Equal(effect.Initializer, identity.Binding.Initializer);
                    }
                    if (battle.Events.Skip(previousEvents).Any(entry => entry.Kind == "EffectContact" && entry.ActorId == enemy.Id && entry.Ability == BattleAbility.EnemyAbility))
                    {
                        recipientsAtContact ??= eligible;
                        lastBlocker = "enemy-reaction-completion";
                    }
                    BattleEvent[] events = battle.Events.Skip(reactionEventStart).Where(entry => entry.ActorId == enemy.Id).ToArray();
                    if (recipientsAtContact is not null && events.Any(entry => entry.Kind == "ActionComplete"))
                    {
                        Assert.Single(events, entry => entry.Kind == "Windup" && entry.Ability == BattleAbility.EnemyAbility);
                        Assert.Single(events, entry => entry.Kind == "ActionComplete");
                        BattleEvent[] contacts = events.Where(entry => entry.Kind == "EffectContact" && entry.Ability == BattleAbility.EnemyAbility).ToArray();
                        AssertEnemyContacts(battle, reaction, contacts, recipientsAtContact);
                        Assert.All(contacts, contact => Assert.Equal(reaction.NativeSequence, contact.NativeSequence));
                        Assert.True(presentation is null || presentation.Complete);
                        return new(presentation is null ? "logical" : "native", $"c0m{enemy.ModelId:D3} {enemy.Id}, callback 2, command {reaction.NativeQueuedCommand}/{reaction.ActionId}, sequence {reaction.NativeSequence}, after Attack by {attacker.Id}, {contacts.Length} contact(s)", presentation is null ? "native-presentation" : null);
                    }
                }

                if (!attacker.IsBusy && !enemy.IsBusy && battle.ActiveAction is null)
                {
                    break;
                }
            }

            if (attacker.IsBusy || enemy.IsBusy || battle.ActiveAction is not null)
            {
                break;
            }
        }

        if (dispatches == 0 && hits > 0)
        {
            lastBlocker = "enemy-reaction-dispatch";
        }
        return new("blocked", $"c0m{enemy.ModelId:D3} {enemy.Id}, {hits} damaging Attack contact(s), {dispatches} exact callback 2 dispatch(es) within the probe budget.", lastBlocker);
    }
}
