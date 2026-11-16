using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticBattleDriverTests
{
    private static DiagnosticBattleDriverRecipe Recipe => new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "enemy-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, ActionTimeoutSeconds = 5, ReadinessTimeoutSeconds = 3, OverallTimeoutSeconds = 20 };
    private static DiagnosticDriverObservation Observation(double seconds, long frame, bool drained = true, bool terminal = false) => new(seconds, frame, frame, true, drained, terminal, drained ? [] : ["pending-contact"]);
    private static BattleDiagnosticEffectDispatch Dispatch(int serial, ushort executed = 2) => new(serial, serial, "party-0", BattleAbility.Magic, 1, 2, executed);

    [Fact]
    public void Native_identity_can_differ_from_the_semantic_guardian_force_id()
    {
        DiagnosticBattleDriverRecipe recipe = Recipe with { Input = Recipe.Input with { Ability = BattleAbility.GuardianForce, ActionId = 0 }, ExpectedNativeAction = 64, ExpectedNativeCommand = 254 };
        recipe.Validate();
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.GuardianForce, 64, 2, 2, 254));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Null(driver.Blocker);
    }

    [Fact]
    public void Declared_supporting_dispatch_does_not_replace_the_focal_dispatch()
    {
        DiagnosticBattleDriver driver = new(Recipe with { SupportingEffects = [3] }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(new(1, 1, "party-0", BattleAbility.Magic, 99, 3, 3));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        Assert.Empty(driver.Dispatches);
        driver.ObserveDispatch(Dispatch(2));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Single(driver.Dispatches);
    }

    [Fact]
    public void Two_dispatches_require_two_independent_drains_and_presented_tails()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        Assert.False(driver.Advance(Observation(0, 0)));
        Assert.True(driver.Advance(Observation(0.1, 1)));
        driver.ObserveDispatch(Dispatch(1));
        Assert.False(driver.Advance(Observation(0.2, 2)));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        Assert.False(driver.Advance(Observation(0.3, 3)));
        Assert.False(driver.Advance(Observation(0.4, 4)));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
        Assert.True(driver.Advance(Observation(0.5, 5)));
        driver.ObserveDispatch(Dispatch(2));
        driver.Advance(Observation(0.6, 6));
        driver.Advance(Observation(0.7, 7));
        driver.Advance(Observation(0.8, 8));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(2, driver.Dispatches.Count);
        Assert.False(driver.Advance(Observation(1, 9)));
    }

    [Fact]
    public void Late_work_restarts_the_complete_presented_tail()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(Dispatch(1));
        driver.Advance(Observation(0.2, 2));
        driver.Advance(Observation(0.3, 3, false));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(0.4, 4));
        driver.Advance(Observation(0.5, 5));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(0.6, 6));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
    }

    [Fact]
    public void Authored_Adel_death_can_complete_with_a_certified_resident_screen_blend()
    {
        string source = new('a', 64);
        DiagnosticBattleDriverRecipe recipe = Recipe with
        {
            Input = Recipe.Input with { Ability = BattleAbility.Attack, ActionId = 0 },
            ExpectedEffect = 211,
            ExpectedNativeCommand = 8,
            ExpectedNativeAction = 317,
            EnemyTrigger = new("enemy-0", 112, 0, null, 3, source)
        };
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(Observation(0, 0));
        Assert.True(driver.Advance(Observation(0.1, 1)));
        BattleDiagnosticEffectDispatch dispatch = new(1, 1, "enemy-0", BattleAbility.EnemyAbility, 317, 211, 211, 8)
        {
            NativeActorIndex = 3,
            NativeAiCallbackStage = 3,
            NativeAiScriptSha256 = source
        };
        driver.ObserveDispatch(dispatch);
        Assert.Equal(dispatch, driver.HeldScreenBlendDispatch);
        BattleDiagnosticInvocationOwners owners = EffectComponentTerminalTextureTrackerTests.Observation(1).Owners;
        BattleComponentHeldScreenBlendTask task = new(0, 1, 0, null, NativeBattleAuxiliaryKind.ScreenBlend501E80, 0, false,
            [2, 0, 2, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        string[] blockers = ["effect-or-auxiliary-task"];
        BattleComponentHeldScreenBlendObservation first = new(211, 2, 0, 0, owners, true, 1, 0, 0, task, task.Fingerprint, [..blockers]);
        EffectComponentHeldScreenBlendTracker tracker = new();
        Assert.False(tracker.Observe(first, 2, 20));
        Assert.False(tracker.Observe(first with { GraphicsCallback = 3 }, 3, 30));
        DiagnosticDriverObservation observation = new(0.3, 3, 3, true, false, false, blockers);
        Assert.False(driver.Advance(observation));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.True(tracker.Observe(first with { GraphicsCallback = 4 }, 4, 40));
        BattleComponentHeldScreenBlendCertificate certificate = tracker.Certificate(211, source, dispatch, null);
        DiagnosticDriverObservation tail = observation with { Seconds = 0.4, Callback = 4, PresentedFrame = 4, HeldScreenBlend = certificate };
        Assert.Throws<InvalidDataException>(() => driver.Advance(tail with { DrainBlockers = ["effect-or-auxiliary-task", "action-or-command"] }));
        Assert.Throws<InvalidDataException>(() => driver.Advance(tail with { HeldScreenBlend = certificate with { NativeDispatch = dispatch with { Serial = 2 } } }));
        Assert.False(driver.Advance(tail));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(blockers, driver.LastDrainBlockers);
        Assert.False(certificate.Drained);
        Assert.True(certificate.ResidentTasksRemain);
    }

    [Fact]
    public void Installed_Adel_death_leaves_the_authored_screen_blend_with_hosted_owners_released()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_ADEL_RECIPE");
        Assert.SkipUnless(directory is not null && path is not null && File.Exists(path), "Select installed game resources and the authored Adel recipe.");
        JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(path!), options)!;
        request.Validate();
        DiagnosticBattleDriverRecipe recipe = Assert.IsType<DiagnosticBattleDriverRecipe>(request.Driver);
        Assert.Equal((ushort)211, recipe.ExpectedEffect);
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        ExpandedBattlePresentationProcess hosted = Assert.IsType<ExpandedBattlePresentationProcess>(battle.ExpandedPresentationProcess);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        world.PrepareDiagnosticEntryBeforePresentation(request);
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        battle.DiagnosticEffectDispatched += dispatches.Add;
        recipe.EnemyTrigger!.Prepare(battle);
        world.ApplyBattleInput(recipe.Input);
        for (int frame = 0; frame < 900; frame++)
        {
            battle.Tick();
            audio.Update(battle);
            if (battle.ObserveComponentHeldScreenBlend(211) is { } observation)
            {
                BattleDiagnosticEffectDispatch dispatch = Assert.Single(dispatches, value => value.ExecutedEffect == 211);
                Assert.Equal(317, dispatch.ActionId);
                Assert.Equal<byte?>(3, dispatch.NativeAiCallbackStage);
                Assert.Equal(796, battle.EncounterId);
                BattleCombatant rinoa = Assert.Single(battle.Combatants, actor => !actor.IsPlayer && actor.ModelId == 113);
                Assert.Equal(1, rinoa.SceneSlot);
                Assert.Equal(4, rinoa.NativeActorIndex);
                Assert.True(observation.IsEligible);
                NativeBattleAuxiliaryTask slot = hosted.Graphics.Auxiliary.Slots[observation.ScreenBlend.Slot];
                Assert.NotNull(slot.Program);
                Assert.False(observation.ScreenBlend.HasProgram);
                Assert.Equal(slot.Payload, observation.ScreenBlend.Payload);
                Assert.Equal(new[] { "effect-or-auxiliary-task" }, battle.ObserveDiagnosticDrainBlockers());
                return;
            }
        }
        object facts = new
        {
            battle.EncounterId, battle.Outcome, Dispatches = dispatches, Blockers = battle.ObserveDiagnosticDrainBlockers(),
            hosted.Memory.Active, Selected = hosted.Graphics.OriginalEffect?.RequestedEffect,
            ReturnedTask = hosted.Memory.ReadReturnedTask5044B0(), hosted.Graphics.Auxiliary.Head, hosted.Graphics.Auxiliary.Tail,
            Tasks = hosted.Graphics.Auxiliary.Slots.Select((task, index) => new { Index = index, task.Flags, task.State, task.Kind, task.Next, task.UnknownByteMask, HasRetainedProgram = task.Program is not null, Payload = Convert.ToHexString(task.Payload.AsSpan()) }).Where(task => (task.Flags & 1) != 0).ToArray(),
            Rinoa = battle.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId == 113).Select(actor => new { actor.SceneSlot, actor.NativeActorIndex, actor.IsActive }).ToArray()
        };
        Assert.Fail(JsonSerializer.Serialize(facts, options));
    }

    [Fact]
    public void Fallback_dispatch_is_an_explicit_blocker()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.ObserveDispatch(Dispatch(1, 3));
        Assert.Equal("REQUESTED_EXECUTED_EFFECT_MISMATCH", driver.Blocker);
        Assert.Empty(driver.Dispatches);
    }

    [Fact]
    public void An_action_that_never_dispatches_cannot_complete()
    {
        DiagnosticBattleDriver driver = new(Recipe, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0.1, 1));
        driver.Advance(Observation(6, 2));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(60, 10)]
    public void Dispatch_wait_expires_at_its_independent_deadline(double actionTimeout, double dispatchTimeout)
    {
        DiagnosticBattleDriver driver = new(Recipe with { ActionTimeoutSeconds = actionTimeout }, 0);
        driver.Advance(Observation(0, 0));
        Assert.True(driver.Advance(Observation(0, 1)));
        driver.Advance(Observation(dispatchTimeout - 0.01, 2));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Observation(dispatchTimeout, 3));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
        Assert.Equal("action_one", driver.CampaignStage);
        Assert.Empty(driver.Dispatches);
    }

    [Fact]
    public void Callback_and_supporting_dispatch_progress_cannot_extend_the_focal_dispatch_deadline()
    {
        DiagnosticBattleDriver driver = new(Recipe with { ActionTimeoutSeconds = 60, SupportingEffects = [3] }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0, 1));
        for (int second = 1; second <= 10; second++)
        {
            driver.ObserveDispatch(new(second, second, "party-0", BattleAbility.Magic, 99, 3, 3));
            driver.Advance(Observation(second, second * 1000));
        }
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
        Assert.Empty(driver.Dispatches);
    }

    [Fact]
    public void The_second_submission_gets_its_own_dispatch_deadline()
    {
        DiagnosticBattleDriver driver = new(Recipe with { ActionTimeoutSeconds = 60 }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0, 1));
        driver.ObserveDispatch(Dispatch(1));
        driver.Advance(Observation(1, 2));
        driver.Advance(Observation(2, 3));
        driver.Advance(Observation(3, 4));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
        Assert.True(driver.Advance(Observation(4, 5)));
        driver.Advance(Observation(13.99, 6));
        Assert.Equal(DiagnosticDriverStage.AwaitDispatch, driver.Stage);
        driver.Advance(Observation(14, 7));
        Assert.Equal("EXPECTED_EFFECT_DISPATCH_TIMEOUT", driver.Blocker);
        Assert.Equal("action_two", driver.CampaignStage);
        Assert.Single(driver.Dispatches);
    }

    [Fact]
    public void Dispatched_effects_keep_the_original_long_drain_and_tail_deadline()
    {
        DiagnosticBattleDriver driver = new(Recipe with { ActionTimeoutSeconds = 60, OverallTimeoutSeconds = 200 }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0, 1));
        driver.ObserveDispatch(Dispatch(1));
        driver.Advance(Observation(11, 2, drained: false));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(12, 3));
        driver.Advance(Observation(23, 4));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(24, 5));
        Assert.Equal(DiagnosticDriverStage.Readiness, driver.Stage);
        Assert.True(driver.Advance(Observation(25, 6)));
        driver.ObserveDispatch(Dispatch(2));
        driver.Advance(Observation(36, 7, drained: false));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.Advance(Observation(37, 8));
        driver.Advance(Observation(48, 9));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(49, 10));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Null(driver.Blocker);
        Assert.Equal(2, driver.Dispatches.Count);
    }

    [Fact]
    public void Dispatched_effects_still_fail_at_the_original_action_deadline()
    {
        DiagnosticBattleDriver driver = new(Recipe with { ActionTimeoutSeconds = 15 }, 0);
        driver.Advance(Observation(0, 0));
        driver.Advance(Observation(0, 1));
        driver.ObserveDispatch(Dispatch(1));
        driver.Advance(Observation(16, 2, drained: false));
        Assert.Equal("ACTION_OR_DRAIN_TIMEOUT", driver.Blocker);
    }

    [Fact]
    public void Terminal_or_rewound_owner_history_blocks_repetition()
    {
        DiagnosticBattleDriver terminal = new(Recipe, 0);
        terminal.Advance(Observation(0, 0, terminal: true));
        Assert.Equal("REPEAT_PRECONDITION_UNSATISFIED", terminal.Blocker);
        DiagnosticBattleDriver rewound = new(Recipe, 0);
        rewound.Advance(Observation(0, 5));
        rewound.Advance(Observation(0.1, 4));
        Assert.Equal("CALLBACK_LINEAGE_REGRESSED", rewound.Blocker);
    }

    [Fact]
    public void Unsupported_repairs_and_unbounded_deadlines_fail_before_launch()
    {
        Assert.Throws<NotSupportedException>(() => (Recipe with { RequiresCombatRepairs = true }).Validate());
        Assert.Throws<NotSupportedException>(() => (Recipe with { RequiresCapabilityAdapter = true }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { ActionTimeoutSeconds = double.PositiveInfinity }).Validate());
        Assert.Throws<InvalidDataException>(() => (Recipe with { TailFrames = 1 }).Validate());
    }
}
