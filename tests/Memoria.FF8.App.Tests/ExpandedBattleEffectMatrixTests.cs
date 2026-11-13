using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests(ITestOutputHelper output)
{
    private const string CasterId = "party-10";
    private const byte InitialStock = 2;

    [Fact]
    public void Installed_effect_catalog_reports_one_cast_on_an_eleven_by_eleven_roster()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeBattleEffectBinding[] catalog = game.ReadNativeEffectCatalog().Where(binding => binding.Entry != 0 || binding.Initializer != 0).ToArray();
        Assert.NotEmpty(catalog);
        const ushort oilboyleAction = 182;
        BattleNativeAction sonicWave = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == oilboyleAction);
        BattleNativeAction oilboyleCounter = Assert.Single(assets.NativeActions, action => action.Command == 8 && action.ActionId == 181);
        GameState field = PrepareField(directory);
        using GameWorld admissionWorld = StartBattle(directory, field, 0);
        BattleSession admission = Assert.IsType<BattleSession>(admissionWorld.Battle);
        BattleActionDefinition[] admitted = admission.ActionDefinitions.Where(action => action.Ability is BattleAbility.Magic or BattleAbility.Item && admission.SupportsExpandedAction(action)).ToArray();
        ILookup<int, BattleActionDefinition> guardianMappings = assets.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce && action.NativeAction is not null).ToLookup(action => (int)action.NativeAction!.MagicId);
        ILookup<int, BattleNativeAction> limitMappings = assets.NativeActions.Where(action => action.KernelSection == 19 && action.CrisisLevel == 4 || action.KernelSection == 18 && action.CharacterId == action.KernelIndex + 6).ToLookup(action => (int)action.MagicId);
        bool requireFull = Environment.GetEnvironmentVariable("MEMORIA_FF8_REQUIRE_FULL_EFFECT_MATRIX") == "1";
        bool probeEnemies = requireFull || Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_MATRIX_PROBE_ENEMIES") == "1";
        EnemyScenarioDiscoveryResult enemies = ScenarioEnemyDiscovery.Discover(assets);
        ILookup<int, EnemyScenarioScriptedAction> enemyMappings = enemies.ScriptedActions.Where(action => action.NativeEffectId.HasValue).ToLookup(action => action.NativeEffectId!.Value);
        Assert.Contains(catalog, binding => binding.Effect == oilboyleCounter.MagicId);
        Assert.Contains(enemyMappings[oilboyleCounter.MagicId], IsOilboyleCounter);
        ILookup<ushort, EnemyScenarioActionOwner> enemyOwners = enemies.Owners.Where(owner => owner.NativeAction is not null).ToLookup(owner => owner.NativeAction!.MagicId);
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        List<EffectResult> results = [];
        List<string> failures = [];

        foreach (NativeBattleEffectBinding effect in catalog)
        {
            BattleNativeAction[] mappings = assets.NativeActions.Where(action => ScenarioDiscovery.ResolveCatalogEffect(action, assets.NativeActions) == effect.Effect).ToArray();
            BattleActionDefinition? player = admitted.Where(action => action.NativeAction is { } native && ScenarioDiscovery.ResolveCatalogEffect(native, assets.NativeActions) == effect.Effect)
                .OrderBy(action => action.Ability == BattleAbility.Magic ? 0 : 1).ThenBy(action => action.Id).FirstOrDefault();
            EnemyScenarioScriptedAction[] scripts = enemyMappings[effect.Effect].ToArray();
            EnemyScenarioActionOwner[] owners = enemyOwners[effect.Effect].ToArray();
            string status;
            string detail;
            string? firstBlockerStage = null;
            EnemyRouteResult? enemyReaction = null;
            EnemyRouteResult? enemyDeath = null;
            string[] admissionBlockers = [];
            try
            {
                EnemyScenarioScriptedAction[] reactions = scripts.Where(script => script.Callback == 2).ToArray();
                if (reactions.Length > 0)
                {
                    firstBlockerStage = "enemy-reaction-execution";
                    enemyReaction = RunDiscoveredEnemyReaction(directory, field, assets, effect, reactions, probeEnemies);
                    if (reactions.Any(IsOilboyleCounter))
                    {
                        Assert.True(enemyReaction.Status is "native" or "logical", enemyReaction.Detail);
                    }
                }
                EnemyScenarioScriptedAction[] deaths = scripts.Where(script => script.Callback == 3).ToArray();
                if (deaths.Length > 0)
                {
                    firstBlockerStage = "enemy-death-execution";
                    enemyDeath = RunDiscoveredEnemyDeath(directory, field, assets, effect, deaths, probeEnemies);
                }
                if (player is not null)
                {
                    firstBlockerStage = "player-execution";
                    (status, detail) = RunPlayerCast(directory, field, effect, player);
                }
                else if (effect.Effect == sonicWave.MagicId)
                {
                    firstBlockerStage = "enemy-turn-execution";
                    (status, detail) = RunSonicWave(directory, field, effect, sonicWave);
                }
                else if (guardianMappings[effect.Effect].FirstOrDefault() is { } guardian && ExpandedBattleGuardianForceTests.RunGuardianForce(assets, guardian) is int guardianContacts)
                {
                    status = "logical";
                    detail = $"Original GF charge and impact used {guardianContacts} roster recipients through the HUD contact path.";
                }
                else if (limitMappings[effect.Effect].FirstOrDefault() is { } limit)
                {
                    firstBlockerStage = "limit-execution";
                    (status, detail) = RunExpandedLimit(directory, field, limit);
                    if (status == "blocked")
                    {
                        firstBlockerStage = "limit-admission";
                    }
                }
                else if (mappings.FirstOrDefault(action => action.KernelSection == 10 && action.Command is 23 or 24 or 25 or 26 or 27 or 32) is { } equipped)
                {
                    firstBlockerStage = "command-execution";
                    (status, detail) = RunEquippedCommand(directory, field, effect, equipped);
                }
                else if (enemyReaction is { } reaction && (reaction.Status is "native" || scripts.All(script => script.Callback == 2)))
                {
                    (status, detail, firstBlockerStage) = reaction;
                }
                else if (scripts.Length > 0)
                {
                    firstBlockerStage = "enemy-turn-execution";
                    EnemyRouteResult route = RunDiscoveredEnemyCast(directory, field, assets, effect, scripts, probeEnemies);
                    (status, detail, firstBlockerStage) = route;
                    admissionBlockers = route.AdmissionBlockers;
                }
                else
                {
                    status = "blocked";
                    firstBlockerStage = owners.Length > 0 ? "enemy-callback-discovery" : mappings.Length > 0 ? "command-admission" : "effect-owner-discovery";
                    detail = owners.Length > 0 ? "Installed enemy ability banks reference this binding. No scanned AI callback establishes its dispatch."
                        : mappings.Length > 0 ? "Installed kernel rows reference this binding. No mapped command has a supported expanded invocation and a prepared recipient route."
                        : "No scanned installed kernel row or enemy AI callback maps to this binding. Its original trigger remains unresolved.";
                }

                if (enemyReaction is { Status: "native" } && status != "native" || enemyReaction is { Status: "logical" } && status == "blocked")
                {
                    (status, detail, firstBlockerStage) = enemyReaction;
                }
                if (enemyDeath is { Status: "native" } && status != "native" || enemyDeath is { Status: "logical" } && status == "blocked" || enemyDeath is not null && status == "blocked" && scripts.All(script => script.Callback == 3))
                {
                    (status, detail, firstBlockerStage) = enemyDeath;
                }
                firstBlockerStage = status switch
                {
                    "native" => null,
                    "logical" => "native-presentation",
                    _ => firstBlockerStage
                };
            }
            catch (Exception failure)
            {
                status = "failed";
                firstBlockerStage ??= "guardian-execution";
                detail = $"{failure.GetType().Name}: {failure.Message}";
                failures.Add($"Effect {effect.Effect}: {failure}");
            }

            counts[status] = counts.GetValueOrDefault(status) + 1;
            string actions = string.Join(",", mappings.Select(action => $"{action.Command}/{action.ActionId}@{action.KernelSection}/{action.KernelIndex}"));
            string[] scripted = scripts.Select(action => $"c0m{action.ModelId:D3}/callback{action.Callback}+{action.Offset}/band{action.LevelBand}/c{action.Command}-a{action.ActionId}-seq{action.Sequence}").ToArray();
            string[] banks = owners.Select(owner => $"c0m{owner.ModelId:D3}/band{owner.LevelBand}/ability{owner.AbilitySlot}/c{owner.Command}-a{owner.ActionId}-seq{owner.Sequence}").ToArray();
            results.Add(new(effect.Effect, effect.Entry, effect.Initializer, status, firstBlockerStage, actions, scripted, banks, detail, enemyReaction, enemyDeath, admissionBlockers));
            output.WriteLine($"{effect.Effect:D3} {status} [{actions}] {detail} (first blocker: {firstBlockerStage ?? "none"})");
        }

        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_MATRIX_REPORT") is { Length: > 0 } reportPath)
        {
            using FileStream report = new(reportPath, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(report, new { Allies = 11, Enemies = 11, CatalogCount = catalog.Length, ProbedEnemyRoutes = probeEnemies, Counts = counts, Results = results, EnemyDiscoveryIssues = enemies.Issues }, new JsonSerializerOptions { WriteIndented = true });
        }
        output.WriteLine($"11x11 installed effects: {catalog.Length} catalog, {counts.GetValueOrDefault("native")} native, {counts.GetValueOrDefault("logical")} logical, {counts.GetValueOrDefault("blocked")} blocked, {counts.GetValueOrDefault("failed")} failed.");
        Assert.True(counts.GetValueOrDefault("native") > 0, "The installed expanded host did not present any catalog effect.");
        Assert.Empty(failures);
        if (requireFull)
        {
            Assert.True(counts.GetValueOrDefault("blocked") == 0 && counts.GetValueOrDefault("logical") == 0, $"The installed catalog still has {counts.GetValueOrDefault("blocked")} unexecuted and {counts.GetValueOrDefault("logical")} logic-only effects in 11x11.");
        }
    }

    private sealed record EffectResult(ushort Effect, uint Entry, uint Initializer, string Status, string? FirstBlockerStage, string Actions, string[] EnemyCallbacks, string[] EnemyAbilityRows, string Detail, EnemyRouteResult? EnemyReaction, EnemyRouteResult? EnemyDeath, string[] AdmissionBlockers);

    private static (string Status, string Detail) RunPlayerCast(string directory, GameState field, NativeBattleEffectBinding effect, BattleActionDefinition action)
    {
        GameWorld world = StartBattle(directory, field, 0, action);
        (string Status, string Detail) result;
        try
        {
            result = RunPlayerCast(world, effect, action);
        }
        catch (Exception failure)
        {
            try
            {
                world.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(failure, cleanup);
            }
            throw;
        }
        world.Dispose();
        return result;
    }

    private static (string Status, string Detail) RunPlayerCast(GameWorld world, NativeBattleEffectBinding effect, BattleActionDefinition action)
    {
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == CasterId);
        for (int turn = 0; turn < 24 && (!caster.IsReady || battle.ActiveAction is not null); turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            world.ApplyBattleInput(new(BattleInputKind.Tick, Frames: 30));
        }
        Assert.True(caster.IsReady);
        Assert.Null(battle.ActiveAction);
        Assert.Equal(InitialStock, Stock());

        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: input => world.ApplyBattleInput(input));
        hud.SelectParty(caster.Id);
        int commandIndex = hud.Commands.ToList().IndexOf(action.Ability);
        Assert.InRange(commandIndex, 0, hud.Commands.Count - 1);
        hud.Choose(commandIndex);
        int actionIndex = hud.ActionChoices.ToList().FindIndex(candidate => candidate.Id == action.Id);
        Assert.InRange(actionIndex, 0, hud.ActionChoices.Count - 1);
        hud.Choose(actionIndex);
        Assert.True(hud.SelectingTarget);
        BattleCombatant target = Assert.IsType<BattleCombatant>(effect.Effect is 1 or 25 ? hud.Targets.Single(candidate => candidate.Id == "party-9") : hud.Targets.LastOrDefault());
        bool revival = action.NativeAction?.AttackType is 5 or 6;
        if (revival)
        {
            Assert.Equal("party-9", target.Id);
            Assert.False(target.IsAlive);
            Assert.Equal(0, target.CurrentHp);
        }
        hud.SelectTarget(target.Id, false);
        bool random = (action.NativeAction?.TargetFlags.GetValueOrDefault() & 2) != 0;
        string[] expected = hud.SelectingTargetGroup ? hud.Targets.Select(actor => actor.Id).Order().ToArray() : [target.Id];
        if (random)
        {
            BattleSide randomSide = (action.NativeAction!.TargetFlags.GetValueOrDefault() & 0x30) == 0x20 ? BattleSide.Enemies : target.Side;
            expected = battle.Combatants.Where(actor => actor.Side == randomSide && BattleSession.IsValidSelectedActionTarget(caster, action, actor)).Select(actor => actor.Id).ToArray();
        }
        int casterHpBefore = caster.CurrentHp;
        int targetHpBefore = target.CurrentHp;
        int before = battle.Events.Count;
        hud.Attack();
        Assert.True(caster.IsBusy);
        NativeBattleEffectPresentation? presentation = null;
        NativeBattleQuake38Resources? quakeResources = null;
        bool observedBinding = false;
        bool hasOverlay = false;
        bool hasCameraShake = false;
        for (int frame = 0; frame < 2048 && caster.IsBusy; frame++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            if (battle.ExpandedEffect is { } live)
            {
                presentation ??= live;
                Assert.Same(presentation, live);
                if (live.Graphics.OriginalEffect is { } identity)
                {
                    Assert.Equal(effect.Effect, identity.RequestedEffect);
                    Assert.Equal(effect.Entry, identity.Binding.Entry);
                    Assert.Equal(effect.Initializer, identity.Binding.Initializer);
                    quakeResources ??= identity.Binding.Quake38Resources;
                    observedBinding = true;
                }
                hasOverlay |= live.Overlay is { LastCommands.IsEmpty: false };
                hasCameraShake |= live.ViewTranslationY != 0;
            }
        }

        Assert.False(caster.IsBusy);
        Assert.Null(battle.ExpandedEffect);
        Assert.Equal(InitialStock - 1, Stock());
        BattleEvent[] castEvents = battle.Events.Skip(before).Where(entry => entry.ActorId == caster.Id).ToArray();
        Assert.Single(castEvents, entry => entry.Kind == "Windup" && entry.Ability == action.Ability);
        Assert.Single(castEvents, entry => entry.Kind == "ActionComplete");
        BattleEvent[] contacts = castEvents.Where(entry => entry.Kind == "EffectContact").ToArray();
        if (random)
        {
            Assert.InRange(contacts.Length, 1, action.NativeAction!.HitCount);
            Assert.All(contacts, contact => Assert.Contains(contact.TargetId, expected));
        }
        else if (action.NativeAction is { HitCount: > 1 } repeated)
        {
            Assert.NotEmpty(contacts);
            string[] targetOrder = battle.Combatants.Where(actor => expected.Contains(actor.Id)).Select(actor => actor.Id).ToArray();
            string[] passes = Enumerable.Range(0, repeated.HitCount).SelectMany(_ => targetOrder).ToArray();
            Assert.Equal(passes.Take(contacts.Length), contacts.Select(entry => entry.TargetId));
            if (contacts.Length < passes.Length)
            {
                Assert.Contains(battle.Combatants, actor => actor.Id == passes[contacts.Length] && (!actor.IsAlive || !actor.IsActive));
            }
        }
        else
        {
            Assert.Equal(expected, contacts.Select(entry => entry.TargetId).Order());
        }
        if (revival)
        {
            Assert.True(target.IsAlive);
            int restored = action.NativeAction!.AttackType == 6 ? target.MaximumHp : Math.Max(1, target.MaximumHp / 8);
            Assert.Equal(restored, target.CurrentHp);
        }
        uint[] nativeSounds = castEvents.Where(entry => entry.NativeModuleSound is not null).Select(entry => entry.NativeModuleSound!.Sound).ToArray();
        string detail = $"{action.Ability}/{action.Id} {action.Name}, {caster.Id}, {contacts.Length} recipient(s), stock {InitialStock}->{Stock()}, overlay={hasOverlay}, camera_shake={hasCameraShake}, native_sounds={nativeSounds.Length}";
        if (effect.Effect == 39)
        {
            BattleEvent primary = Assert.Single(contacts);
            BattleEvent secondary = Assert.Single(castEvents, entry => entry.Kind == "SecondaryEffectContact");
            Assert.Equal(caster.Id, secondary.TargetId);
            Assert.Equal(BattleAbility.Magic, secondary.Ability);
            Assert.Single(castEvents, entry => entry.Kind == "EffectDamage" && entry.TargetId == target.Id && entry.Amount == primary.Amount);
            Assert.Single(castEvents, entry => entry.Kind == "EffectHealing" && entry.TargetId == caster.Id && entry.Amount == secondary.Amount);
            Assert.True(primary.Amount > 0 && secondary.Amount > 0);
            Assert.True(casterHpBefore < caster.MaximumHp);
            Assert.Equal(Math.Max(0, targetHpBefore - primary.Amount), target.CurrentHp);
            Assert.Equal(Math.Min(caster.MaximumHp, casterHpBefore + secondary.Amount), caster.CurrentHp);
            detail += $", target_hp={targetHpBefore}->{target.CurrentHp}, caster_hp={casterHpBefore}->{caster.CurrentHp}, secondary_contact={secondary.Amount}";
        }
        if (presentation is null)
        {
            return ("logical", detail);
        }
        Assert.True(observedBinding);
        Assert.True(presentation.Complete);
        if (effect.Effect == 38 && !hasOverlay)
        {
            Assert.True(hasCameraShake, "Quake's original no-ground callback must publish camera shake.");
            Assert.NotNull(quakeResources);
            Assert.Single(nativeSounds, sound => sound == quakeResources.RumbleSound);
            Assert.Single(nativeSounds, sound => sound == quakeResources.QuakeSound);
        }
        return ("native", detail);

        int Stock() => action.Ability == BattleAbility.Magic ? caster.MagicStocks.Single(stock => stock.Id == action.Id).Count : battle.Items.Single(stock => stock.Id == action.Id).Count;
    }

    private static (string Status, string Detail) RunSonicWave(string directory, GameState field, NativeBattleEffectBinding effect, BattleNativeAction action)
    {
        using GameWorld world = StartBattle(directory, field, 79);
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        string[] allies = battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.Id).Order().ToArray();
        NativeBattleEffectPresentation? presentation = null;
        for (int turn = 0; turn < 24; turn++)
        {
            world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn));
            for (int frame = 0; frame < 30; frame++)
            {
                world.ApplyBattleInput(new(BattleInputKind.Tick));
                if (battle.ExpandedEffect is { Graphics.OriginalEffect: { } identity } live && identity.RequestedEffect == effect.Effect)
                {
                    presentation ??= live;
                    Assert.Same(presentation, live);
                    Assert.Equal(effect.Entry, identity.Binding.Entry);
                    Assert.Equal(effect.Initializer, identity.Binding.Initializer);
                }
                BattleEvent[] contacts = battle.Events.Where(entry => entry.Kind == "EffectContact" && entry.Ability == BattleAbility.EnemyAbility && entry.Message == action.Name).ToArray();
                if (contacts.Length == 0 || battle.ActiveAction is not null)
                {
                    continue;
                }
                string? caster = Assert.Single(contacts.Select(entry => entry.ActorId).Distinct());
                Assert.Equal(allies, contacts.Select(entry => entry.TargetId).Order());
                Assert.All(contacts, contact => Assert.True(contact.Amount > 0));
                Assert.Single(battle.Events, entry => entry.ActorId == caster && entry.Kind == "Windup" && entry.Ability == BattleAbility.EnemyAbility && entry.Message.EndsWith(action.Name + ".", StringComparison.Ordinal));
                Assert.True(presentation is null || presentation.Complete);
                return (presentation is null ? "logical" : "native", $"Original Oilboyle AI {action.Command}/{action.ActionId} {action.Name}, {caster}, {contacts.Length} recipient(s)");
            }
        }
        throw new InvalidDataException("The installed Oilboyle AI did not complete its first Sonic Wave in the expanded host.");
    }

    internal static GameState PrepareField(string directory)
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        using GameWorld bootstrap = new(initial, directory);
        bootstrap.EnsureField();
        initial.SetEventState(bootstrap.Events.Capture());
        return initial.State with
        {
            FieldId = 133,
            FieldName = "cavern-component",
            EventState = null,
            DepartingFieldState = initial.State.EventState,
            FieldLoadContext = null,
            FieldRequestPacket = null
        };
    }

    private static GameWorld StartBattle(string directory, GameState field, ushort formation, BattleActionDefinition? action = null, bool requireExpanded = true, BattleNativeAction? limit = null, BattleNativeAction? equipped = null, byte nativeSeed = 0)
    {
        GameSession session = new(field);
        session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
        GameWorld world = new(session, directory, expandedBattleAllyCount: 11, expandedBattleEnemyCount: 11);
        try
        {
            world.EnsureField();
            world.Apply(new(PlayerActionKind.Wait, Frames: 4));
            NativeCharacterVitals vitals = (session.State.CharacterVitals! with { CombatParty = [0, 1, 2] }).WithGuestBattleRecords().Rest();
            NativeCharacterVital[] records = vitals.Records.Select(record => record with
            {
                MemberFlags = (ushort)(record.MemberFlags | 1),
                HitPointBonus = 2400,
                HitPoints = new(1200, 1200),
                Status = 0
            }).ToArray();
            if (limit is { CharacterId: int characterId })
            {
                records[characterId] = records[characterId] with { HitPoints = new(1, 1) };
                foreach (int knockedOut in Enumerable.Range(0, 11).Where(id => id != characterId).Take(4))
                {
                    records[knockedOut] = records[knockedOut] with { HitPoints = new(0, 0), Status = 1 };
                }
                session.SetLimitProgress(session.State.LimitProgress! with { BlueMagicFlags = ushort.MaxValue });
            }
            if (equipped is not null)
            {
                uint commandAbility = checked((uint)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf(equipped.Command)));
                records[10] = records[10] with { Junction = records[10].Junction with { Commands = commandAbility } };
                if (equipped.AttackType is 5 or 6)
                {
                    records[9] = records[9] with { HitPoints = new(0, 0), Status = 1 };
                }
            }
            if (action is not null)
            {
                if (action.NativeAction?.AttackType is 5 or 6)
                {
                    records[9] = records[9] with { HitPoints = new(0, 0), Status = 1 };
                }
                bool magic = action.Ability == BattleAbility.Magic;
                byte command = magic ? (byte)2 : (byte)4;
                uint commandAbility = checked((uint)(20 + vitals.MenuDefinitions.CommandAbilityCommands.IndexOf(command)));
                records[10] = records[10] with { Junction = records[10].Junction with { Commands = commandAbility } };
                byte actionId = checked((byte)action.Id);
                if (magic)
                {
                    NativeMagicStock[] stock = records[10].MagicInventory.Select(_ => new NativeMagicStock(0, 0)).ToArray();
                    stock[0] = new(actionId, InitialStock);
                    records[10] = records[10] with { MagicInventory = stock };
                }
                else
                {
                    NativeEconomy economy = session.State.Economy!;
                    NativeItemStock[] items = economy.Items.ToArray();
                    int index = Array.FindIndex(items, stock => stock.ItemId == actionId);
                    items[index >= 0 ? index : Array.FindIndex(items, stock => stock.ItemId == 0)] = new(actionId, InitialStock);
                    session.SetEconomy(economy with { Items = items });
                }
            }
            session.SetCharacterVitals(vitals with { Records = records });
            string source = world.Events.Capture().GetProperty("ProgramHash").GetString()!;
            session.SetRandomEncounters(new(true, NativeRandomEncounterState.FromColdProcess(), new(SceneKind.Field, 133, world.Events.Frame, formation, source)));
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: nativeSeed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            if (requireExpanded || battle.UsesExpandedRoster)
            {
                Assert.True(battle.UsesExpandedRoster);
                Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
                Assert.Equal(11, battle.Combatants.Count(actor => !actor.IsPlayer && actor.IsActive));
            }
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }
}
