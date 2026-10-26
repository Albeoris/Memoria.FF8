using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DiagnosticTerminalHoldNativeTests
{
    [Theory]
    [InlineData(109, false)]
    [InlineData(109, true)]
    [InlineData(120, false)]
    public void External_original_terminal_recipe_preserves_natural_defeat_or_repeats_with_explicit_hold(int expectedEffect, bool hold)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_HOLD_RECIPE");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath), "Select installed resources and an external original terminal recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest original = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipePath!), json)!;
        DiagnosticBattleDriverRecipe source = Assert.IsType<DiagnosticBattleDriverRecipe>(original.Driver);
        Assert.SkipUnless(source.ExpectedEffect == expectedEffect, "The selected external recipe belongs to the other terminal effect.");
        int probeLimit = 30000;
        string? configuredLimit = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_PROBE_LIMIT");
        if (configuredLimit is not null)
        {
            Assert.True(int.TryParse(configuredLimit, out probeLimit) && probeLimit is >= 128 and <= 30000, "MEMORIA_FF8_TERMINAL_PROBE_LIMIT must be an integer from128 through30000.");
        }
        if (expectedEffect == 109)
        {
            Assert.Equal(28, source.Input.ActionId);
        }
        else
        {
            Assert.Equal(6, Assert.IsType<BattleDiagnosticEnemyAbility>(source.EnemyAbility).Model);
        }
        BattleDiagnosticCombatRepair repair = expectedEffect == 109 ? new("party-0", 1, Revive: true) : new("party-0", 1213, RemoveStatuses0: 4);
        DiagnosticBattleDriverRecipe driver = source with
        {
            TerminalHold = hold ? new("Explicit terminal diagnostic repetition", [repair.ActorId], 18000) : null,
            Repairs = hold ? [repair] : []
        };
        DiagnosticBattleRequest request = original with { Driver = driver };
        request.Validate();
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> focal = [];
        List<BattleEvent> completions = [];
        List<object> boundaries = [];
        List<object> holdProbes = [];
        List<BattleDiagnosticRepairReceipt> repairs = [];
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == driver.Input.ActorId && dispatch.RequestedEffect == expectedEffect)
            {
                focal.Add(dispatch);
            }
        };
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "ActionComplete" && entry.ActorId == driver.Input.ActorId)
            {
                completions.Add(entry);
            }
        };
        int callbacks = 0;
        int ticks = 0;
        bool firstHeldObserved = false;
        bool blocked512Observed = false;
        long initialCallback = battle.NativeHostCallbacks ?? battle.Frame;
        Guid lifecycle = battle.DiagnosticLifecycleId;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            for (int cast = 1; cast <= (hold ? 2 : 1); cast++)
            {
                WaitUntil(() => battle.NativeEntryReady && RecreateGame.DiagnosticCommandReady(battle, driver) && battle.ObserveDiagnosticDrainBlockers().Count == 0, "command readiness");
                Record("before-cast-" + cast);
                if (driver.EnemyAbility is { } enemy)
                {
                    battle.SubmitDiagnosticEnemyAbility(driver.Input.ActorId!, enemy, driver.ExpectedEffect);
                }
                else
                {
                    world.ApplyBattleInput(driver.Input);
                }
                WaitUntil(() => focal.Count >= cast, "focal dispatch");
                Assert.Equal((ushort)expectedEffect, focal[^1].ExecutedEffect);
                if (!hold)
                {
                    WaitUntil(() => battle.Outcome == BattleOutcome.Defeat && battle.NativeGraphics?.NativeOutcome?.FadeDurationB0C == 0, "natural defeat and completed fade");
                    Assert.Null(battle.DiagnosticTerminalHold);
                    Assert.Equal((byte?)1, battle.NativeGraphics!.NativeOutcome!.ResultByte6E7);
                    Assert.Empty(battle.DiagnosticTerminalHoldReceipts);
                    Assert.Single(completions);
                    Record("natural-defeat");
                    break;
                }
                WaitUntil(() => battle.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held && completions.Count >= cast && battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0, "held terminal full native drain");
                Assert.Equal(BattleOutcome.Active, battle.Outcome);
                Assert.True(battle.NativeGraphics!.NativeOutcome?.ResultByte6E7 is null or 0);
                BattleDiagnosticCombatant party = Assert.Single(battle.ObserveDiagnosticCombatants(), actor => actor.Id == repair.ActorId);
                if (expectedEffect == 109)
                {
                    Assert.Equal(0, party.CurrentHp);
                    Assert.NotEqual(0, party.Statuses0 & 1);
                }
                else
                {
                    Assert.NotEqual(0, party.Statuses0 & 4);
                }
                Assert.False(RecreateGame.DiagnosticCommandReady(battle, driver));
                Record("held-cast-" + cast);
                if (cast == 1)
                {
                    long callback = battle.NativeHostCallbacks!.Value;
                    repairs.Add(battle.ApplyDiagnosticCombatRepairs(driver.Repairs, callback));
                    battle.RearmDiagnosticTerminalHold(callback);
                    Assert.Equal(BattleDiagnosticTerminalHoldPhase.Armed, battle.DiagnosticTerminalHold!.Phase);
                    Assert.Equal(callback, battle.NativeHostCallbacks);
                    Record("rearmed");
                }
                else
                {
                    battle.ReleaseDiagnosticTerminalHold(battle.NativeHostCallbacks!.Value);
                    Assert.Equal(new[] { "held", "rearmed", "held", "released" }, battle.DiagnosticTerminalHoldReceipts.Select(receipt => receipt.Kind).ToArray());
                    WaitUntil(() => battle.Outcome == BattleOutcome.Defeat && battle.NativeGraphics?.NativeOutcome?.FadeDurationB0C == 0, "released natural defeat and completed fade");
                    Assert.Equal((byte?)1, battle.NativeGraphics!.NativeOutcome!.ResultByte6E7);
                    Record("released-defeat");
                }
                Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
                Assert.Equal(0, battle.DiagnosticRestoreCount);
                Assert.Equal(0, battle.DiagnosticResetCount);
            }
            Assert.Equal(hold ? 2 : 1, focal.Count);
            Assert.Equal(lifecycle, battle.DiagnosticLifecycleId);
            Assert.Equal(0, battle.DiagnosticRestoreCount);
            Assert.Equal(0, battle.DiagnosticResetCount);
            if (hold)
            {
                Assert.True(focal[1].Callback > focal[0].Callback);
                Assert.True(focal[1].Serial > focal[0].Serial);
                Assert.Single(repairs);
            }
        }
        finally
        {
            string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_HOLD_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, $"terminal-{expectedEffect}-hold-{hold}.json"), JsonSerializer.Serialize(new { expectedEffect, hold, callbacks, probeLimit, focal, completions, repairs, boundaries, holdProbes, terminalHolds = battle.DiagnosticTerminalHoldReceipts }, json));
            }
        }

        void WaitUntil(Func<bool> condition, string boundary)
        {
            while (!condition() && callbacks < probeLimit && ticks < probeLimit)
            {
                battle.Tick();
                audio.Update(battle);
                ticks++;
                callbacks = checked((int)((battle.NativeHostCallbacks ?? battle.Frame) - initialCallback));
                if (hold && !firstHeldObserved && battle.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held)
                {
                    firstHeldObserved = true;
                    RecordHoldProbe("first-held", boundary);
                }
                if (hold && !blocked512Observed && callbacks >= 512 && !condition())
                {
                    blocked512Observed = true;
                    RecordHoldProbe("blocked-at-512", boundary);
                }
            }
            Assert.True(condition(), $"Did not reach {boundary} within {probeLimit} native callbacks. Owners: {JsonSerializer.Serialize(battle.ObserveDiagnosticInvocationOwners(), json)}");
        }

        void RecordHoldProbe(string kind, string boundary)
        {
            NativeBattleScriptState script = (NativeBattleScriptState)typeof(BattleSession).GetField("nativeScriptState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
            holdProbes.Add(new
            {
                kind,
                boundary,
                battle.Frame,
                battle.NativeHostCallbacks,
                holdPhase = battle.DiagnosticTerminalHold?.Phase,
                allocationControl = script.AllocationControl,
                reactionOrder = typeof(BattleSession).GetField("nativeReactionOrder", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle),
                pendingReactions = typeof(BattleSession).GetProperty("HasNativePendingReactions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle),
                blockers = battle.NativeEntryReady ? battle.ObserveDiagnosticDrainBlockers().ToArray() : ["native-entry"],
                actors = battle.Combatants.Where(actor => Actor("NativeIndex", actor) is int index && index is >= 0 and < 7).Select(actor => new
                {
                    actor.Id,
                    actor.IsPlayer,
                    actor.IsActive,
                    actor.CurrentHp,
                    index = Actor("NativeIndex", actor),
                    pending = Actor("NativePendingReaction", actor),
                    saved = Actor("NativeStatus0", actor),
                    statuses = Actor("NativeStatus1", actor)
                }).ToArray()
            });
        }

        object? Actor(string name, BattleCombatant actor) => typeof(BattleCombatant).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(actor);

        void Record(string boundary)
        {
            boundaries.Add(new { boundary, owners = battle.ObserveDiagnosticInvocationOwners(), party = battle.ObserveDiagnosticCombatants(), battle.Outcome, battle.NativeResult });
        }
    }

    [Fact]
    public void Original_all_party_Petrify_retains_unadmitted_pending_bytes_under_bounded_hold()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_HOLD_RECIPE");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && recipePath is not null && File.Exists(recipePath), "Select installed resources and the original Petrify Stare recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest original = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipePath!), json)!;
        DiagnosticBattleDriverRecipe source = Assert.IsType<DiagnosticBattleDriverRecipe>(original.Driver);
        Assert.SkipUnless(source.ExpectedEffect == 120, "The selected recipe must be the original Petrify Stare case.");
        BattleDiagnosticEnemyAbility enemy = Assert.IsType<BattleDiagnosticEnemyAbility>(source.EnemyAbility);
        Assert.Equal(6, enemy.Model);
        BattleDiagnosticCombatRepair repair = new("party-0", 1213, RemoveStatuses0: 4);
        DiagnosticBattleRequest request = original with { Driver = source with { TerminalHold = new("Observe original gated all-party Petrify", ["party-0"], 18000), Repairs = [repair] } };
        request.Validate();
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        List<BattleDiagnosticEffectDispatch> focal = [];
        List<BattleEvent> completions = [];
        List<object> observations = [];
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            if (dispatch.ActorId == source.Input.ActorId && dispatch.RequestedEffect == 120)
            {
                focal.Add(dispatch);
            }
        };
        battle.DiagnosticEventPublished += entry =>
        {
            if (entry.Kind == "ActionComplete" && entry.ActorId == source.Input.ActorId)
            {
                completions.Add(entry);
            }
        };
        long initialCallback = battle.NativeHostCallbacks!.Value;
        int ticks = 0;
        bool submitted = false;
        bool observedHeld = false;
        try
        {
            if (request.PrepareEntryBeforePresentation)
            {
                world.PrepareDiagnosticEntryBeforePresentation(request);
            }
            while (battle.NativeHostCallbacks - initialCallback < 512 && ticks < 512)
            {
                if (!submitted && battle.NativeEntryReady && battle.CanSubmitDiagnosticEnemyAbility(source.Input.ActorId!, enemy) && battle.ObserveDiagnosticDrainBlockers().Count == 0)
                {
                    battle.SubmitDiagnosticEnemyAbility(source.Input.ActorId!, enemy, 120);
                    submitted = true;
                }
                battle.Tick();
                audio.Update(battle);
                ticks++;
                if (!observedHeld && battle.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held)
                {
                    observations.Add(ObserveGated("first-held"));
                    observedHeld = true;
                }
            }
            Assert.True(submitted);
            Assert.True(observedHeld);
            Assert.Equal(512L, battle.NativeHostCallbacks - initialCallback);
            observations.Add(ObserveGated("callback-512"));
            Assert.Single(focal);
            Assert.Equal((ushort)120, focal[0].ExecutedEffect);
            Assert.Single(completions);
            Assert.Null(battle.ActiveAction);
            Assert.Empty(battle.QueuedActions);
            Assert.Equal(new[] { "timed-input-or-mandatory-reaction" }, battle.ObserveDiagnosticDrainBlockers());
            BattleDiagnosticTerminalHoldReceipt receipt = Assert.Single(battle.DiagnosticTerminalHoldReceipts);
            Assert.Equal("held", receipt.Kind);
            Assert.Equal(1, receipt.Sequence);
            Assert.Equal(0, battle.DiagnosticTerminalHold!.Rearms);
            Assert.False(RecreateGame.DiagnosticCommandReady(battle, request.Driver!));
            Assert.Equal(0, battle.DiagnosticRestoreCount);
            Assert.Equal(0, battle.DiagnosticResetCount);
        }
        finally
        {
            string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_HOLD_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, "terminal-120-gated-negative.json"), JsonSerializer.Serialize(new { request, ticks, focal, completions, observations, terminalHolds = battle.DiagnosticTerminalHoldReceipts }, json));
            }
        }

        object ObserveGated(string boundary)
        {
            NativeBattleScriptState script = (NativeBattleScriptState)typeof(BattleSession).GetField("nativeScriptState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle)!;
            NativeBattleAllocationControl control = Assert.IsType<NativeBattleAllocationControl>(script.AllocationControl);
            Assert.False(control.ActionGate);
            Assert.False(control.PauseGate);
            byte? order = (byte?)typeof(BattleSession).GetField("nativeReactionOrder", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battle);
            Assert.Equal((byte?)1, order);
            BattleCombatant party = Assert.Single(battle.Combatants, actor => actor.IsPlayer);
            Assert.Equal("party-0", party.Id);
            NativeBattlePendingReaction pending = (NativeBattlePendingReaction)typeof(BattleCombatant).GetProperty("NativePendingReaction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(party)!;
            Assert.Equal(new NativeBattlePendingReaction(2, 0), pending);
            BattleDiagnosticCombatant observed = Assert.Single(battle.ObserveDiagnosticCombatants(), actor => actor.Id == party.Id);
            Assert.Equal((ushort)4, observed.Statuses0);
            Assert.Equal(1213, observed.CurrentHp);
            Assert.Equal(0, observed.NativeIndex);
            Assert.Equal(BattleOutcome.Active, battle.Outcome);
            Assert.Equal(BattleDiagnosticTerminalHoldPhase.Held, battle.DiagnosticTerminalHold!.Phase);
            Assert.True(battle.NativeGraphics!.NativeOutcome?.ResultByte6E7 is null or 0);
            Assert.Contains("timed-input-or-mandatory-reaction", battle.ObserveDiagnosticDrainBlockers());
            return new { boundary, battle.Frame, battle.NativeHostCallbacks, control, order, pending, party = observed, blockers = battle.ObserveDiagnosticDrainBlockers().ToArray() };
        }
    }
}
