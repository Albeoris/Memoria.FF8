using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedBattleEffectMatrixTests(ITestOutputHelper output)
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
        GameState field = PrepareField(directory);
        using GameWorld admissionWorld = StartBattle(directory, field, 0);
        BattleSession admission = Assert.IsType<BattleSession>(admissionWorld.Battle);
        BattleActionDefinition[] admitted = admission.ActionDefinitions.Where(action => action.Ability is BattleAbility.Magic or BattleAbility.Item && admission.SupportsExpandedAction(action)).ToArray();
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        List<EffectResult> results = [];
        List<string> failures = [];

        foreach (NativeBattleEffectBinding effect in catalog)
        {
            BattleNativeAction[] mappings = assets.NativeActions.Where(action => MappedEffect(action, assets.NativeActions) == effect.Effect).ToArray();
            BattleActionDefinition? player = admitted.Where(action => action.NativeAction is { } native && MappedEffect(native, assets.NativeActions) == effect.Effect)
                .OrderBy(action => action.Ability == BattleAbility.Magic ? 0 : 1).ThenBy(action => action.Id).FirstOrDefault();
            string status;
            string detail;
            try
            {
                if (player is not null)
                {
                    (status, detail) = RunPlayerCast(directory, field, effect, player);
                }
                else if (effect.Effect == sonicWave.MagicId)
                {
                    (status, detail) = RunSonicWave(directory, field, effect, sonicWave);
                }
                else
                {
                    status = "blocked";
                    detail = mappings.Length == 0 ? "No installed kernel action maps to this binding" : "No mapped action has a supported ordinary expanded command and a prepared recipient route";
                }
            }
            catch (Exception failure)
            {
                status = "failed";
                detail = $"{failure.GetType().Name}: {failure.Message}";
                failures.Add($"Effect {effect.Effect}: {failure}");
            }

            counts[status] = counts.GetValueOrDefault(status) + 1;
            string actions = string.Join(",", mappings.Select(action => $"{action.Command}/{action.ActionId}@{action.KernelSection}/{action.KernelIndex}"));
            results.Add(new(effect.Effect, status, actions, detail));
            output.WriteLine($"{effect.Effect:D3} {status} [{actions}] {detail}");
        }

        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_MATRIX_REPORT") is { Length: > 0 } reportPath)
        {
            using FileStream report = new(reportPath, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(report, new { Allies = 11, Enemies = 11, CatalogCount = catalog.Length, Counts = counts, Results = results }, new JsonSerializerOptions { WriteIndented = true });
        }
        output.WriteLine($"11x11 installed effects: {catalog.Length} catalog, {counts.GetValueOrDefault("native")} native, {counts.GetValueOrDefault("logical")} logical, {counts.GetValueOrDefault("blocked")} blocked, {counts.GetValueOrDefault("failed")} failed.");
        Assert.True(counts.GetValueOrDefault("native") > 0, "The installed expanded host did not present any catalog effect.");
        Assert.Empty(failures);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_REQUIRE_FULL_EFFECT_MATRIX") == "1")
        {
            Assert.True(counts.GetValueOrDefault("blocked") == 0 && counts.GetValueOrDefault("logical") == 0, $"The installed catalog still has {counts.GetValueOrDefault("blocked")} unexecuted and {counts.GetValueOrDefault("logical")} logic-only effects in 11x11.");
        }
    }

    private sealed record EffectResult(ushort Effect, string Status, string Actions, string Detail);

    private static ushort? MappedEffect(BattleNativeAction action, IReadOnlyList<BattleNativeAction> actions)
    {
        if (action.KernelSection != 7 || action.AttackType != 14)
        {
            return action.MagicId;
        }
        if (action.ActionId == 30)
        {
            // The PocketStation item selects its summon dynamically.
            return null;
        }
        BattleNativeAction[] children = actions.Where(child => child.Command == 244 && child.ActionId == action.MagicId).ToArray();
        return children.Length == 1 ? children[0].MagicId : null;
    }

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
        BattleCombatant target = Assert.IsType<BattleCombatant>(hud.Targets.LastOrDefault());
        bool revival = action.NativeAction?.AttackType is 5 or 6;
        if (revival)
        {
            Assert.Equal("party-9", target.Id);
            Assert.False(target.IsAlive);
            Assert.Equal(0, target.CurrentHp);
        }
        hud.SelectTarget(target.Id, false);
        string[] expected = hud.SelectingTargetGroup ? hud.Targets.Select(actor => actor.Id).Order().ToArray() : [target.Id];
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
        Assert.Equal(expected, contacts.Select(entry => entry.TargetId).Order());
        if (revival)
        {
            Assert.True(target.IsAlive);
            int restored = action.NativeAction!.AttackType == 6 ? target.MaximumHp : Math.Max(1, target.MaximumHp / 8);
            Assert.Equal(restored, target.CurrentHp);
        }
        uint[] nativeSounds = castEvents.Where(entry => entry.NativeModuleSound is not null).Select(entry => entry.NativeModuleSound!.Sound).ToArray();
        string detail = $"{action.Ability}/{action.Id} {action.Name}, {caster.Id}, {contacts.Length} recipient(s), stock {InitialStock}->{Stock()}, overlay={hasOverlay}, camera_shake={hasCameraShake}, native_sounds={nativeSounds.Length}";
        if (presentation is null)
        {
            Assert.Equal(BattleAbility.Item, action.Ability);
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

    private static GameState PrepareField(string directory)
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

    private static GameWorld StartBattle(string directory, GameState field, ushort formation, BattleActionDefinition? action = null)
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
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 0));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            Assert.True(battle.UsesExpandedRoster);
            Assert.Equal(11, battle.Combatants.Count(actor => actor.IsPlayer));
            Assert.Equal(11, battle.Combatants.Count(actor => !actor.IsPlayer && actor.IsActive));
            return world;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }
}
