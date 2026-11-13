using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Fact]
    public void InstalledPoisonGasDeathCallbackCompletesAfterLethalAttackAndJsonRestore()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding effect = Assert.Single(game.ReadNativeEffectCatalog(), binding => binding.Effect == 171);
        EnemyScenarioScriptedAction[] scripts = ScenarioEnemyDiscovery.Discover(assets).ScriptedActions.Where(script => script is { ModelId: 36, Callback: 3, Command: 8, NativeEffectId: 171 }).ToArray();
        Assert.NotEmpty(scripts);
        using GameWorld world = StartBattle(directory, PrepareField(directory), 71);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant enemy = Assert.Single(battle.Combatants, actor => actor.Id == "enemy-10" && actor.ModelId == 36);
        Assert.Equal(assets.ReadEnemyAi(enemy.ModelId, enemy.Level).Scripts[3], enemy.NativeAi!.Scripts[3]);
        EnemyRouteResult result = ObserveEnemyDeath(world, battle, effect, enemy, scripts, verifyRestore: true);
        Assert.True(result.Status is "native" or "logical", result.Detail);
    }

    private static EnemyRouteResult RunDiscoveredEnemyDeath(string directory, GameState field, BattleAssets assets, NativeBattleEffectBinding effect, EnemyScenarioScriptedAction[] scripts, bool probeEnemies)
    {
        if (scripts.All(script => script.NativeAction is null))
        {
            return new("blocked", "Installed callback 3 uses a direct graphics invocation.", "enemy-death-direct-graphics");
        }
        HashSet<string> blockers = new(StringComparer.Ordinal);
        var candidates = scripts.Where(script => script.NativeAction is not null)
            .SelectMany(script => script.Formations.Where(scene => scene.Enabled && !scene.Hidden && !scene.Suspended && !scene.Untargetable).Select(scene => (Script: script, Scene: scene)))
            .Where(route => AdmitsEnemyScript(assets, route.Script, route.Scene, blockers))
            .GroupBy(route => route.Scene.FormationId)
            .OrderBy(routes => routes.Key)
            .Take(3).ToArray();
        if (candidates.Length == 0)
        {
            return new("blocked", "No enabled scene owner and level band for the installed callback 3 passed expanded AI admission.", "enemy-death-admission") { AdmissionBlockers = [.. blockers.Order()] };
        }
        if (!probeEnemies)
        {
            return new("blocked", $"Callback 3 candidate formations {string.Join(", ", candidates.Select(routes => routes.Key))}. Set MEMORIA_FF8_EFFECT_MATRIX_PROBE_ENEMIES=1 to submit lethal attacks against their original owners.", "enemy-death-probe-disabled");
        }

        EnemyRouteResult? firstBlocker = null;
        List<string> attempts = [];
        foreach (var routes in candidates)
        {
            ushort formation = checked((ushort)routes.Key);
            using GameWorld world = StartBattle(directory, field, formation, requireExpanded: false);
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            EnemyRouteResult result = new("blocked", "The scene retained its original roster.", "enemy-death-formation-entry");
            if (battle.UsesExpandedRoster)
            {
                var owners = battle.Combatants.Reverse().Where(actor => !actor.IsPlayer && actor.IsAlive && actor.IsActive && !actor.Untargetable && actor.NativeAi is not null)
                    .Select(actor => (Actor: actor, Scripts: routes.Select(route => route.Script).Where(script => script.ModelId == actor.ModelId && script.LevelBand == actor.NativeAi!.LevelBand).Distinct().ToArray()))
                    .Where(owner => owner.Scripts.Length > 0)
                    .GroupBy(owner => (owner.Actor.ModelId, owner.Actor.NativeAi!.LevelBand))
                    .Select(group => group.First()).ToArray();
                result = new("blocked", "The expanded roster has no matching living scene owner and level band.", "enemy-death-owner");
                foreach (var owner in owners)
                {
                    Assert.Equal(assets.ReadEnemyAi(owner.Actor.ModelId, owner.Actor.Level).Scripts[3], owner.Actor.NativeAi!.Scripts[3]);
                    result = ObserveEnemyDeath(world, battle, effect, owner.Actor, owner.Scripts);
                    if (result.Status is "native" or "logical")
                    {
                        return result with { Detail = $"Formation {formation}, {result.Detail}" };
                    }
                }
            }
            firstBlocker ??= result;
            attempts.Add($"formation {formation}: {result.Detail}");
        }
        return firstBlocker! with { Detail = string.Join("; ", attempts) };
    }

    private static EnemyRouteResult ObserveEnemyDeath(GameWorld world, BattleSession battle, NativeBattleEffectBinding effect, BattleCombatant enemy, EnemyScenarioScriptedAction[] scripts, bool verifyRestore = false)
    {
        BattleCombatant attacker = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        int deaths = 0;
        int dispatches = 0;
        string lastBlocker = "enemy-death-trigger";
        BattleSession? restored = null;
        for (int attempt = 0; attempt < 24 && battle.Outcome == BattleOutcome.Active && attacker.IsAlive && enemy.IsAlive; attempt++)
        {
            // Set up a lethal contact while leaving the callback's KO, reward, memory and RNG owners to the real attack.
            enemy.CurrentHp = 1;
            attacker.Atb = BattleSession.AtbMaximum;
            int beforeAttack = battle.Events.Count;
            world.ApplyBattleInput(new(BattleInputKind.Action) { ActorId = attacker.Id, TargetId = enemy.Id, Ability = BattleAbility.Attack });
            if (!attacker.IsBusy)
            {
                break;
            }
            Assert.Contains(battle.QueuedActions, command => command.ActorId == attacker.Id && command.TargetId == enemy.Id && command.Ability == BattleAbility.Attack);

            BattleActionPresentation? cast = null;
            NativeBattleEffectPresentation? presentation = null;
            HashSet<string>? recipientsAtContact = null;
            int castEventStart = 0;
            for (int frame = 0; frame < 320 && battle.Outcome == BattleOutcome.Active; frame++)
            {
                int previousEvents = battle.Events.Count;
                HashSet<string> eligible = battle.Combatants.Where(actor => actor.IsActive && actor.IsAlive && !actor.Untargetable && (actor.PersistentStatus & 5) == 0).Select(actor => actor.Id).ToHashSet(StringComparer.Ordinal);
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (restored is not null)
                {
                    restored.Tick();
                }
                deaths += battle.Events.Skip(previousEvents).Count(entry => entry.Kind == "Defeated" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id);
                if (cast is null && battle.ActiveAction is { NativeAiCallbackStage: 3, AiRecipients: not null } active && active.ActorId == enemy.Id && scripts.Any(script => MatchesEnemyDispatch(active, script, effect)))
                {
                    Assert.True(deaths > 0);
                    Assert.Contains(battle.Events.Skip(beforeAttack), entry => entry.Kind == "Damage" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id && entry.Amount > 0);
                    Assert.Contains(battle.Events.Skip(beforeAttack), entry => entry.Kind == "ActionComplete" && entry.ActorId == attacker.Id && entry.TargetId == enemy.Id);
                    Assert.Equal(attacker.Id, enemy.LastAttackerId);
                    Assert.Contains(battle.EnemyDefeats, defeat => defeat.AllocationId == enemy.Id);
                    cast = active;
                    castEventStart = previousEvents;
                    dispatches++;
                    lastBlocker = "enemy-death-contact";
                    if (verifyRestore)
                    {
                        restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
                    }
                }
                if (cast is not null)
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
                        if (cast.AiRecipients!.DeathSelfTargetId is string self)
                        {
                            eligible.Add(self);
                        }
                        recipientsAtContact ??= eligible;
                        lastBlocker = "enemy-death-completion";
                    }
                    BattleEvent[] events = battle.Events.Skip(castEventStart).Where(entry => entry.ActorId == enemy.Id).ToArray();
                    if (recipientsAtContact is not null && events.Any(entry => entry.Kind == "ActionComplete"))
                    {
                        Assert.Single(events, entry => entry.Kind == "Windup" && entry.Ability == BattleAbility.EnemyAbility);
                        Assert.Single(events, entry => entry.Kind == "ActionComplete");
                        Assert.Single(battle.EnemyDefeats, defeat => defeat.AllocationId == enemy.Id);
                        BattleEvent[] contacts = events.Where(entry => entry.Kind == "EffectContact" && entry.Ability == BattleAbility.EnemyAbility).ToArray();
                        AssertEnemyContacts(battle, cast, contacts, recipientsAtContact);
                        Assert.All(contacts, contact => Assert.Equal(cast.NativeSequence, contact.NativeSequence));
                        Assert.True(presentation is null || presentation.Complete);
                        if (restored is not null)
                        {
                            Assert.Null(presentation);
                            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                        }
                        return new(presentation is null ? "logical" : "native", $"c0m{enemy.ModelId:D3} {enemy.Id}, callback 3, command {cast.NativeQueuedCommand}/{cast.ActionId}, sequence {cast.NativeSequence}, after lethal Attack by {attacker.Id}, {contacts.Length} contact(s)", presentation is null ? "native-presentation" : null);
                    }
                }
                if (!attacker.IsBusy && !enemy.IsBusy && battle.ActiveAction is null)
                {
                    break;
                }
            }
        }
        if (dispatches == 0 && deaths > 0)
        {
            lastBlocker = "enemy-death-dispatch";
        }
        return new("blocked", $"c0m{enemy.ModelId:D3} {enemy.Id}, {deaths} lethal Attack contact(s), {dispatches} exact callback 3 dispatch(es) within the probe budget.", lastBlocker);
    }
}
