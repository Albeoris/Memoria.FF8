using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSmoothShotActionTests
{
    [Theory]
    [InlineData((ushort)188, 0)]
    [InlineData((ushort)192, 1)]
    [InlineData((ushort)193, 2)]
    [InlineData((ushort)194, 3)]
    [InlineData((ushort)195, 4)]
    [InlineData((ushort)196, 5)]
    [InlineData((ushort)197, 6)]
    [InlineData((ushort)198, 7)]
    public void Real_Irvine_opening_trigger_and_cancel_preserve_native_duration_events_and_all_fractional_draws(ushort effect, int ammo)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        Assert.True(NativeBattleSmoothPresentation.Supports(effect));
        const string actor = "party-2";
        const string target = "enemy-6";
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: actor, TargetId: target, Ability: BattleAbility.LimitBreak, ActionId: (14 << 16) | ammo), effect)
        {
            ExpectedNativeCommand = 14,
            ExpectedNativeAction = ammo,
            LimitSelection = new(1, 4, 256)
        };
        DiagnosticBattleRequest request = new(982, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(0, true)], [new(2, 2, [], [0]) { HitPoints = 1 }], true) { CombatParty = [2, 255, 255], Items = [new(101 + ammo, 2)] },
            Target = new(target, 142, 126, 6, 3, 195, 1, 355, 76),
            Driver = driver,
            PrepareEntryBeforePresentation = true
        };
        request.Validate();
        using BattleAssets assets = new(directory);
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession battle = world.StartDiagnosticBattle(request);
        world.PrepareDiagnosticEntryBeforePresentation(request);
        GameSession controlSession = GameWorld.CreateColdProcess(directory);
        controlSession.StartNewGame();
        using GameWorld controlWorld = new(controlSession, directory);
        BattleSession control = controlWorld.StartDiagnosticBattle(request);
        controlWorld.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(RecreateGame.DiagnosticCommandReady(battle, driver), "The installed Irvine row must reach its actual ready command boundary.");
        Assert.True(RecreateGame.DiagnosticCommandReady(control, driver));
        BattleSnapshot initial = battle.Capture();
        NativeBattleSmoothWholeActionTests.OpaqueMagicComparison comparison = new(initial, control.Capture());
        comparison.AssertEqual(initial, control.Capture());
        List<BattleEvent> renderedEvents = [];
        List<BattleEvent> controlEvents = [];
        battle.DiagnosticEventPublished += renderedEvents.Add;
        control.DiagnosticEventPublished += controlEvents.Add;
        int verifiedEvents = 0;
        long? startCallbacks = battle.NativeHostCallbacks;
        Assert.NotNull(startCallbacks);
        int totalTicks = 0;
        int compatibleIntervals = 0;
        int uncoveredIntervals = 0;
        int loadingIntervals = 0;
        int emptyIntervals = 0;
        long effectCorners = 0;
        long movingCorners = 0;
        bool fired = false;
        bool ended = false;
        bool pureDraws = false;
        bool verifyNextCallback = false;
        bool nextCallbackVerified = false;
        long? pureDrawCallback = null;
        bool complete = false;
        bool preservation = false;
        string? failure = null;
        Dictionary<string, (int Count, long FirstCallback)> uncovered = [];
        NativeBattlePreparedPresentation? previous = null;
        bool previousActive = false;
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        NativeBattlePrimitiveCapabilities capabilities = initial.Definition.NativeGraphics!.PrimitiveCapabilities ?? throw new InvalidOperationException("The installed Shot fixture requires its native renderer capabilities.");
        Func<int, int, bool, Albeoris.Games.FF8.Imaging.PixelImage> icons = assets.ReadNativePcUiIcons().Resolve;
        try
        {
            BattleDiagnosticLimitSelectionReceipt selected = battle.SelectDiagnosticLimit(actor, driver.LimitSelection!);
            BattleDiagnosticLimitSelectionReceipt controlSelected = control.SelectDiagnosticLimit(actor, driver.LimitSelection!);
            Assert.True(selected.Accepted, "The real ready-menu selections did not admit Irvine's declared crisis range.");
            Assert.True(controlSelected.Accepted);
            Assert.Equal((byte)14, selected.Command);
            Assert.Equal(selected.CrisisSelections.ToArray(), controlSelected.CrisisSelections.ToArray());
            Checkpoint();
            world.ApplyBattleInput(driver.Input);
            controlWorld.ApplyBattleInput(driver.Input);
            Checkpoint();
            for (int tick = 0; tick < 16384; tick++)
            {
                AdvanceBoth();
                if (battle.GetNativeShot(actor) is { Phase: BattleNativeShotPhase.Ready } ready)
                {
                    Assert.Equal(ready, control.GetNativeShot(actor));
                    if (!fired)
                    {
                        Assert.Equal(2, battle.Items.Single(stock => stock.Id == 101 + ammo).Count);
                        world.ApplyBattleInput(new(BattleInputKind.ShotTrigger, ActorId: actor));
                        controlWorld.ApplyBattleInput(new(BattleInputKind.ShotTrigger, ActorId: actor));
                        Assert.Equal(BattleNativeShotPhase.AwaitingEffect, battle.GetNativeShot(actor)!.Phase);
                        Assert.Equal(1, battle.Items.Single(stock => stock.Id == 101 + ammo).Count);
                        fired = true;
                        Checkpoint();
                    }
                    else if (!ended)
                    {
                        // This is the ordinary public cancellation operation used by the Shot widget.
                        battle.EndNativeShot(actor);
                        control.EndNativeShot(actor);
                        Assert.Equal(BattleNativeShotPhase.Ending, battle.GetNativeShot(actor)!.Phase);
                        ended = true;
                        Checkpoint();
                    }
                }
                if (ended && battle.GetNativeShot(actor) is null && Drained(battle))
                {
                    Assert.Null(control.GetNativeShot(actor));
                    Assert.True(Drained(control));
                    complete = true;
                    break;
                }
            }
            Assert.True(complete, $"Shot {effect} did not drain its real opening/trigger/cancel cycle in {totalTicks} ticks: {string.Join(", ", battle.ObserveDiagnosticDrainBlockers())}.");
            Assert.True(fired);
            Assert.True(ended);
            Checkpoint();
            BattleSnapshot actual = battle.Capture();
            BattleSnapshot expected = control.Capture();
            Assert.Equal(expected.Random!.Lane, actual.Random!.Lane);
            Assert.Equal(expected.Random.Indexes.ToArray(), actual.Random.Indexes.ToArray());
            Assert.Equal(expected.NativeCrtRandom, actual.NativeCrtRandom);
            Assert.Equal(expected.NativeGlobalRandom, actual.NativeGlobalRandom);
            Assert.Equal(control.NativeHostCallbacks - startCallbacks, battle.NativeHostCallbacks - startCallbacks);
            Assert.Equal(JsonSerializer.Serialize(controlEvents), JsonSerializer.Serialize(renderedEvents));
            preservation = true;
            Assert.True(compatibleIntervals > 0);
            Assert.True(effectCorners > 0, "The actual fired Shot must submit its own authored effect geometry.");
            Assert.True(movingCorners > 0, "The actual fired Shot must submit compatible continuous source geometry.");
            Assert.True(pureDraws, "No visible moving completed Shot interval reached the endpoint/120-draw/next-callback purity check.");
            Assert.True(nextCallbackVerified, "The callback immediately following the pure draws must match its independent native control.");
            Assert.True(uncoveredIntervals == 0, $"Shot {effect} has uncovered intervals: {JsonSerializer.Serialize(uncovered.Select(entry => new { BankOpcode = entry.Key, entry.Value.Count, entry.Value.FirstCallback }))}");
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            throw;
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ARTIFACTS") is string artifacts)
            {
                Directory.CreateDirectory(artifacts);
                File.WriteAllText(Path.Combine(artifacts, $"shot-action-{effect}-{Guid.NewGuid():N}.json"), JsonSerializer.Serialize(new { Effect = effect, AmmoAction = ammo, AmmoItem = 101 + ammo, Complete = complete, Fired = fired, Cancelled = ended, NativePreservationVerified = preservation, PureDrawsVerified = pureDraws, NextCallbackVerified = nextCallbackVerified, Ticks = totalTicks, NativeCallbacks = battle.NativeHostCallbacks - startCallbacks, CompatibleIntervals = compatibleIntervals, UncoveredIntervals = uncoveredIntervals, LoadingIntervals = loadingIntervals, EmptyIntervals = emptyIntervals, EffectCorners = effectCorners, MovingCorners = movingCorners, Uncovered = uncovered.Select(entry => new { BankOpcode = entry.Key, entry.Value.Count, entry.Value.FirstCallback }).ToArray(), Events = renderedEvents.Count, Failure = failure, Scope = "Native Shot cycle preservation and source coverage; no FPS measurement. Real installed Irvine saved row, finite stock and bounded ordinary Limit selection; two independently prepared diagnostic hosts." }));
            }
        }

        void AdvanceBoth()
        {
            battle.Tick();
            control.Tick();
            totalTicks++;
            Assert.Equal(control.Frame, battle.Frame);
            Assert.Equal(control.NativeHostCallbacks, battle.NativeHostCallbacks);
            Assert.Equal(control.GetNativeShot(actor), battle.GetNativeShot(actor));
            Assert.Equal(control.Combatants.Select(combatant => (combatant.Id, combatant.CurrentHp)), battle.Combatants.Select(combatant => (combatant.Id, combatant.CurrentHp)));
            Assert.Equal(controlEvents.Count, renderedEvents.Count);
            while (verifiedEvents < renderedEvents.Count)
            {
                Assert.Equal(JsonSerializer.Serialize(controlEvents[verifiedEvents]), JsonSerializer.Serialize(renderedEvents[verifiedEvents]));
                verifiedEvents++;
            }
            if (verifyNextCallback)
            {
                Assert.Equal(pureDrawCallback + 1, battle.NativeHostCallbacks);
                Checkpoint();
                verifyNextCallback = false;
                nextCallbackVerified = true;
            }
            ObserveCompletedInterval();
        }

        void Checkpoint()
        {
            BattleSnapshot rendered = battle.Capture();
            BattleSnapshot native = control.Capture();
            comparison.RegisterReservations(rendered, native);
            comparison.AssertEqual(rendered, native);
        }

        void ObserveCompletedInterval()
        {
            NativeBattlePreparedPresentation? current = battle.PreparedPresentation;
            if (current?.PilotEffect != effect)
            {
                previous = null;
                previousActive = false;
                return;
            }
            bool active = battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } original && original.Binding.Effect == effect;
            bool consecutive = previous is not null && previous.Callback + 1 == current.Callback && previous.PilotGeneration == current.PilotGeneration;
            if (consecutive && previous is not null)
            {
                bool own = HasEffect(previous) || HasEffect(current);
                if (!previousActive && !active && !own)
                {
                    loadingIntervals++;
                }
                else if (!renderer.CanSampleNativePilotInterval(previous, current))
                {
                    uncoveredIntervals++;
                    RecordUncovered(previous);
                    RecordUncovered(current);
                }
                else if (!own)
                {
                    emptyIntervals++;
                }
                else
                {
                    Assert.NotNull(previous.PilotProjection);
                    Assert.NotNull(current.PilotProjection);
                    Assert.NotNull(previous.PilotCamera);
                    Assert.NotNull(current.PilotCamera);
                    compatibleIntervals++;
                    long? callbacks = battle.NativeHostCallbacks;
                    long frame = battle.Frame;
                    int events = renderedEvents.Count;
                    Render(previous, current, 0.25f);
                    NativeBattlePilotSubmission quarter = renderer.PilotSubmission;
                    Render(previous, current, 0.75f);
                    NativeBattlePilotSubmission threeQuarter = renderer.PilotSubmission;
                    Assert.Equal(callbacks, battle.NativeHostCallbacks);
                    Assert.Equal(frame, battle.Frame);
                    Assert.Equal(events, renderedEvents.Count);
                    // A moving triangle can become degenerate between fractions, changing the submitted material hash.
                    // Replaying the same fraction must preserve all submitted geometry and materials exactly.
                    Render(previous, current, 0.25f);
                    NativeBattlePilotSubmission replayedQuarter = renderer.PilotSubmission;
                    Assert.Equal(quarter, replayedQuarter);
                    if (!pureDraws && fired && quarter.CoveredContinuousCorners > 0 && quarter.WorldSignature != threeQuarter.WorldSignature)
                    {
                        VerifyPureDraws(previous, current);
                        pureDraws = true;
                    }
                }
            }
            previous = current;
            previousActive = active;
        }

        void VerifyPureDraws(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later)
        {
            BattleSnapshot frozen = battle.Capture();
            string before = JsonSerializer.Serialize(frozen);
            long? callbacks = battle.NativeHostCallbacks;
            int events = renderedEvents.Count;
            renderer.RenderNativePrimitiveFrame(earlier.Primitives, earlier.Textures, capabilities: capabilities, textureWindowCache: earlier.TextureWindows, uiFrame: earlier.UiFrame, uiTexture: icons);
            TriangleVertex[] first = capture.Vertices.ToArray();
            renderer.RenderNativePrimitiveFrame(later.Primitives, later.Textures, capabilities: capabilities, textureWindowCache: later.TextureWindows, uiFrame: later.UiFrame, uiTexture: icons);
            TriangleVertex[] next = capture.Vertices.ToArray();
            Assert.NotEmpty(first);
            for (int draw = 0; draw < 120; draw++)
            {
                float alpha = draw % 5 / 4f;
                Render(earlier, later, alpha);
                if (alpha == 0)
                {
                    Assert.Equal(first, capture.Vertices.ToArray());
                }
                else if (alpha == 1)
                {
                    Assert.Equal(next, capture.Vertices.ToArray());
                }
            }
            Assert.Equal(callbacks, battle.NativeHostCallbacks);
            Assert.Equal(events, renderedEvents.Count);
            Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
            Checkpoint();
            // The caller's next normal tick advances both independent owners once;
            // verify its entire state immediately, without issuing any callback from Render.
            pureDrawCallback = callbacks;
            verifyNextCallback = true;
        }

        void Render(NativeBattlePreparedPresentation earlier, NativeBattlePreparedPresentation later, float alpha)
        {
            renderer.RenderNativePilotPrimitiveFrame(earlier, later, alpha, capabilities: capabilities, uiTexture: icons);
            Assert.Equal(0, renderer.LastPilotFallbackCount);
            effectCorners += renderer.PilotSubmission.Corners;
            movingCorners += renderer.PilotSubmission.CoveredContinuousCorners;
        }

        bool HasEffect(NativeBattlePreparedPresentation frame) => frame.Primitives.LastCommands.Any(resolved => resolved.Command.PilotGeometry?.Identity.Effect == effect);

        void RecordUncovered(NativeBattlePreparedPresentation source)
        {
            NativeBattlePreparedPresentation empty = source with { Primitives = source.Primitives with { LastCommands = [] } };
            foreach (NativeBattleLogicalResolvedCommand resolved in source.Primitives.LastCommands)
            {
                NativeBattlePreparedPresentation single = source with { Primitives = source.Primitives with { LastCommands = [resolved] } };
                if (renderer.CanSampleNativePilotInterval(single, empty))
                {
                    continue;
                }
                string key = $"{resolved.Command.Packet.Bank}/0x{resolved.Command.Opcode:X2}";
                uncovered[key] = uncovered.TryGetValue(key, out var prior) ? (prior.Count + 1, prior.FirstCallback) : (1, source.Callback);
            }
        }
    }

    private static bool Drained(BattleSession battle) => battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GuardianForceCasts.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0;

    private sealed class Capture : ITriangleRenderer, ILineRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
        }

        public void End()
        {
        }
    }
}
