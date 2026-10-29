using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSmoothWholeActionTests
{
    [Theory]
    [InlineData((ushort)2, 1)]
    [InlineData((ushort)3, 7)]
    [InlineData((ushort)144, 4)]
    [InlineData((ushort)142, 2)]
    [InlineData((ushort)143, 3)]
    [InlineData((ushort)103, 5)]
    [InlineData((ushort)104, 6)]
    [InlineData((ushort)102, 8)]
    [InlineData((ushort)105, 9)]
    [InlineData((ushort)222, 10)]
    [InlineData((ushort)118, 11)]
    [InlineData((ushort)22, 12)]
    [InlineData((ushort)175, 14)]
    [InlineData((ushort)124, 15)]
    [InlineData((ushort)38, 17)]
    [InlineData((ushort)146, 18)]
    [InlineData((ushort)149, 19)]
    [InlineData((ushort)107, 13)]
    [InlineData((ushort)39, 44)]
    [InlineData((ushort)148, 49)]
    [InlineData((ushort)223, 16)]
    [InlineData((ushort)221, 20)]
    public void Original_elemental_action_preserves_native_duration_events_and_whole_action_contracts(ushort effect, int action)
    {
        AssertWholeAction(effect, action, false);
    }

    [Theory]
    [InlineData((ushort)1, 21)]
    [InlineData((ushort)25, 22)]
    [InlineData((ushort)28, 23)]
    [InlineData((ushort)147, 26)]
    [InlineData((ushort)24, 27)]
    [InlineData((ushort)32, 29)]
    [InlineData((ushort)33, 30)]
    [InlineData((ushort)106, 31)]
    [InlineData((ushort)125, 35)]
    [InlineData((ushort)123, 36)]
    [InlineData((ushort)109, 28)]
    [InlineData((ushort)111, 32)]
    [InlineData((ushort)4, 33)]
    [InlineData((ushort)21, 34)]
    [InlineData((ushort)119, 37)]
    [InlineData((ushort)121, 38)]
    [InlineData((ushort)122, 41)]
    public void Original_status_action_preserves_native_duration_events_and_whole_action_contracts(ushort effect, int action)
    {
        AssertWholeAction(effect, action, false, partyTarget: effect is 1 or 25 or 28 or 147 or 24 or 32 or 33 or 106 or 125 or 111 or 4 or 21);
    }

    [Theory]
    [InlineData((ushort)116, 0)]
    [InlineData((ushort)185, 1)]
    [InlineData((ushort)201, 2)]
    [InlineData((ushort)95, 3)]
    [InlineData((ushort)205, 4)]
    [InlineData((ushort)325, 5)]
    [InlineData((ushort)278, 6)]
    [InlineData((ushort)6, 7)]
    [InlineData((ushort)291, 8)]
    [InlineData((ushort)203, 9)]
    [InlineData((ushort)204, 10)]
    [InlineData((ushort)191, 11)]
    [InlineData((ushort)202, 12)]
    [InlineData((ushort)199, 13)]
    [InlineData((ushort)90, 14)]
    [InlineData((ushort)206, 15)]
    public void Original_guardian_action_preserves_native_duration_events_and_whole_action_contracts(ushort effect, int guardian)
    {
        AssertWholeAction(effect, guardian, true);
    }

    private static void AssertWholeAction(ushort effect, int action, bool guardian, bool partyTarget = false)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original battle resources are required.");
        Assert.True(NativeBattleSmoothPresentation.Supports(effect));
        using BattleAssets assets = new(directory);
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        bool quakeGround = effect == 38;
        string target = partyTarget || guardian && action is 6 or 9 ? "party-0" : quakeGround ? "enemy-0" : "enemy-6";
        BattleAbility ability = guardian ? BattleAbility.GuardianForce : BattleAbility.Magic;
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "party-0", TargetId: target, Ability: ability, ActionId: action), effect)
        {
            ExpectedNativeAction = guardian ? 64 + action : action,
            ExpectedNativeCommand = guardian ? (byte)254 : (byte)2
        };
        DiagnosticCharacterPreparation character = guardian
            ? new(0, 0, [], [action]) { Commands = [new(0, 21)], HitPoints = 486 }
            : new(0, 0, [new(action, 2)], [0]) { MagicCommand = new(0, 20), HitPoints = partyTarget && effect is 1 or 25 or 28 ? (ushort)1 : (ushort)486 };
        DiagnosticBattleRequest request = new(quakeGround ? 94 : 982, 0, 128, 0, 0)
        {
            BattleMusic = 5,
            PrimaryMusicChannel = -1,
            Fixture = new([new(guardian ? action : 0, true)], [character], true),
            Target = quakeGround ? new("enemy-0", 31, 61, 0, 3, 0xC3, 1, null, null) : new("enemy-6", 142, 126, 6, 3, 195, 1, 355, 76),
            Driver = driver,
            PrepareEntryBeforePresentation = true
        };
        request.Validate();
        BattleSession battle = world.StartDiagnosticBattle(request);
        world.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(RecreateGame.DiagnosticCommandReady(battle, driver));
        GameSession controlSession = GameWorld.CreateColdProcess(directory);
        controlSession.StartNewGame();
        using GameWorld controlWorld = new(controlSession, directory);
        BattleSession control = controlWorld.StartDiagnosticBattle(request);
        controlWorld.PrepareDiagnosticEntryBeforePresentation(request);
        Assert.True(RecreateGame.DiagnosticCommandReady(control, driver));
        // Diagnostic scheduling is deliberately not persisted; both real hosts must prepare it independently.
        BattleSnapshot seed = battle.Capture();
        BattleSnapshot controlSeed = control.Capture();
        OpaqueMagicComparison comparison = new(seed, controlSeed);
        comparison.AssertEqual(seed, controlSeed);
        List<BattleEvent> sampledEvents = [];
        List<BattleEvent> controlEvents = [];
        battle.DiagnosticEventPublished += sampledEvents.Add;
        control.DiagnosticEventPublished += controlEvents.Add;
        world.ApplyBattleInput(driver.Input);
        controlWorld.ApplyBattleInput(driver.Input);
        BattleSnapshot queued = battle.Capture();
        BattleSnapshot controlQueued = control.Capture();
        comparison.RegisterReservations(queued, controlQueued);
        comparison.AssertEqual(queued, controlQueued);
        long? startCallback = battle.NativeHostCallbacks;
        Assert.Equal(startCallback, control.NativeHostCallbacks);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture, PilotDiagnosticsEnabled = true };
        NativeBattlePrimitiveCapabilities capabilities = seed.Definition.NativeGraphics!.PrimitiveCapabilities ?? throw new InvalidOperationException("The original fixture requires its native primitive device profile.");
        Func<int, int, bool, Albeoris.Games.FF8.Imaging.PixelImage> icons = assets.ReadNativePcUiIcons().Resolve;
        NativeBattlePreparedPresentation? previous = null;
        bool previousActive = false;
        Dictionary<string, Uncovered> uncovered = [];
        int effectFrames = 0;
        int activeEffectFrames = 0;
        int selectedLoadingOrCastingIntervals = 0;
        int consecutiveIntervals = 0;
        int renderedIntervals = 0;
        long submittedFxCorners = 0;
        long coveredContinuousCorners = 0;
        long discreteCorners = 0;
        int emptyOrStaticIntervals = 0;
        int missingCameraIntervals = 0;
        int uncoveredIntervals = 0;
        int totalTicks = 0;
        bool complete = false;
        bool preservationVerified = false;
        string? failure = null;
        try
        {
            for (int tick = 0; tick < 16384; tick++)
            {
                battle.Tick();
                control.Tick();
                totalTicks++;
                Assert.Equal(control.Frame, battle.Frame);
                Assert.Equal(control.NativeHostCallbacks, battle.NativeHostCallbacks);
                NativeBattlePreparedPresentation? current = battle.PreparedPresentation;
                if (current?.PilotEffect == effect)
                {
                    effectFrames++;
                    // A selected binding can precede native entry and remain during casting/loading.
                    // The returned task descriptor is the native effect service's execution owner.
                    bool currentActive = battle.NativeGraphics?.OriginalEffect?.ReturnedDescriptor is > 0;
                    if (currentActive)
                    {
                        activeEffectFrames++;
                    }
                    bool consecutive = previous is not null && previous.Callback + 1 == current.Callback && previous.PilotGeneration == current.PilotGeneration;
                    bool ownsPackets = previous is not null && (HasEffectGeometry(previous, effect) || HasEffectGeometry(current, effect));
                    if (consecutive && !previousActive && !currentActive && !ownsPackets)
                    {
                        selectedLoadingOrCastingIntervals++;
                    }
                    else if (consecutive && previous is not null)
                    {
                        consecutiveIntervals++;
                        bool compatible = renderer.CanSampleNativePilotInterval(previous, current);
                        if (!compatible)
                        {
                            uncoveredIntervals++;
                            RecordUncovered(previous);
                            RecordUncovered(current);
                        }
                        else if (previous.PilotCamera is null || current.PilotCamera is null || previous.PilotProjection is null || current.PilotProjection is null)
                        {
                            missingCameraIntervals++;
                        }
                        else if (!HasEffectGeometry(previous, effect) && !HasEffectGeometry(current, effect))
                        {
                            // Empty windows and discrete full-screen flashes have no moving effect primitives to sample.
                            emptyOrStaticIntervals++;
                        }
                        else
                        {
                            long? callbacksBefore = battle.NativeHostCallbacks;
                            long frameBefore = battle.Frame;
                            int eventsBefore = sampledEvents.Count;
                            renderer.RenderNativePilotPrimitiveFrame(previous, current, 0.25f, capabilities: capabilities, uiTexture: icons);
                            ObserveSubmission();
                            renderer.RenderNativePilotPrimitiveFrame(previous, current, 0.75f, capabilities: capabilities, uiTexture: icons);
                            ObserveSubmission();
                            Assert.Equal(callbacksBefore, battle.NativeHostCallbacks);
                            Assert.Equal(frameBefore, battle.Frame);
                            Assert.Equal(eventsBefore, sampledEvents.Count);
                            Assert.Equal(0, renderer.LastPilotFallbackCount);
                            renderedIntervals++;
                        }
                    }
                    previous = current;
                    previousActive = currentActive;
                }
                else
                {
                    previous = null;
                    previousActive = false;
                }
                bool battleDrained = Drained(battle);
                bool controlDrained = Drained(control);
                Assert.Equal(controlDrained, battleDrained);
                if (activeEffectFrames > 0 && battleDrained)
                {
                    complete = true;
                    break;
                }
            }
            Assert.True(complete, $"Effect {effect} did not complete its original action within {totalTicks} native host ticks; blockers: {string.Join(", ", battle.ObserveDiagnosticDrainBlockers())}.");
            BattleSnapshot actual = battle.Capture();
            BattleSnapshot expected = control.Capture();
            Assert.Equal(expected.Frame, actual.Frame);
            Assert.Equal(expected.Random?.Lane, actual.Random?.Lane);
            Assert.Equal(expected.Random?.Indexes.ToArray(), actual.Random?.Indexes.ToArray());
            Assert.Equal(expected.NativeGlobalRandom, actual.NativeGlobalRandom);
            Assert.Equal(expected.NativeCrtRandom, actual.NativeCrtRandom);
            Assert.Equal(control.Combatants.Select(actor => (actor.Id, actor.CurrentHp)), battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp)));
            Assert.Equal(control.NativeHostCallbacks - startCallback, battle.NativeHostCallbacks - startCallback);
            Assert.Equal(JsonSerializer.Serialize(controlEvents), JsonSerializer.Serialize(sampledEvents));
            comparison.AssertEqual(actual, expected);
            preservationVerified = true;
            Assert.True(renderedIntervals > 0, $"Effect {effect} supplied no nonempty compatible intervals across its whole action.");
            Assert.True(submittedFxCorners > 0, $"Effect {effect} supplied no actual submitted effect primitives in its compatible intervals.");
            Assert.True(uncoveredIntervals == 0, $"Effect {effect} has {uncoveredIntervals} uncovered intervals: {string.Join("; ", uncovered.Values.Select(value => $"{value.Bank}/0x{value.Opcode:X2}: {value.Count}, first callback {value.FirstCallback}"))}.");
            Assert.Equal(0, missingCameraIntervals);
        }
        catch (Exception error)
        {
            failure = $"{error.GetType().Name}: {error.Message}";
            throw;
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ARTIFACTS") is string artifacts)
            {
                string report = Path.Combine(artifacts, $"whole-action-{effect}-{Guid.NewGuid():N}.json");
                Directory.CreateDirectory(artifacts);
                File.WriteAllText(report, JsonSerializer.Serialize(new
                {
                    Effect = effect,
                    Action = action,
                    Encounter = request.Encounter,
                    Guardian = guardian,
                    Complete = complete,
                    NativePreservationVerified = preservationVerified,
                    NativeHostTicks = totalTicks,
                    NativeCallbacks = battle.NativeHostCallbacks - startCallback,
                    SelectedEffectFrames = effectFrames,
                    ActiveEffectFrames = activeEffectFrames,
                    SelectedLoadingOrCastingIntervals = selectedLoadingOrCastingIntervals,
                    ConsecutiveIntervals = consecutiveIntervals,
                    RenderedIntervals = renderedIntervals,
                    SubmittedFxCorners = submittedFxCorners,
                    CoveredContinuousCorners = coveredContinuousCorners,
                    DiscreteCorners = discreteCorners,
                    EmptyOrStaticIntervals = emptyOrStaticIntervals,
                    MissingCameraIntervals = missingCameraIntervals,
                    UncoveredIntervals = uncoveredIntervals,
                    Uncovered = uncovered.Values,
                    SampledEvents = sampledEvents.Count,
                    ControlEvents = controlEvents.Count,
                    Failure = failure,
                    Validation = "Whole-action preservation and interval coverage; no FPS measurement. Independent hosts preserve diagnostic scheduling; comparison copies normalize only role-matched opaque magic identifiers. Midpoint restore is covered separately by endpoint-purity tests."
                }));
            }
        }

        void ObserveSubmission()
        {
            submittedFxCorners += renderer.PilotSubmission.Corners;
            coveredContinuousCorners += renderer.PilotSubmission.CoveredContinuousCorners;
            discreteCorners += renderer.PilotSubmission.DiscreteCorners;
        }

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
                NativeBattleLogicalCommand command = resolved.Command;
                string key = $"{command.Packet.Bank}/0x{command.Opcode:X2}";
                uncovered[key] = uncovered.TryGetValue(key, out Uncovered? prior)
                    ? prior with { Count = prior.Count + 1 }
                    : new(command.Packet.Bank, command.Opcode, 1, source.Callback);
            }
        }
    }

    private static bool Drained(BattleSession battle)
    {
        return battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.GuardianForceCasts.Count == 0 && battle.ObserveDiagnosticDrainBlockers().Count == 0;
    }

    private static bool HasEffectGeometry(NativeBattlePreparedPresentation frame, ushort effect)
    {
        return frame.Primitives.LastCommands.Any(resolved => resolved.Command.PilotGeometry?.Identity.Effect == effect);
    }

    // These are host nonces, not native random values: InitializeBattleMagic creates EncounterId;
    // EnsureMagicReservation creates ActionId. BeginEncounter uses the former only as a reset identity,
    // and Reserve/Commit/Release use the latter only to link the same quoted cast through its lifecycle.
    // Normalize named reference fields on comparison copies only; every other serialized value stays exact.
    internal sealed class OpaqueMagicComparison
    {
        private readonly Dictionary<string, string> sampled = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> control = new(StringComparer.Ordinal);

        internal OpaqueMagicComparison(BattleSnapshot first, BattleSnapshot second)
        {
            MagicSystemState a = Assert.IsType<MagicSystemState>(first.MagicVitals?.MagicSystem);
            MagicSystemState b = Assert.IsType<MagicSystemState>(second.MagicVitals?.MagicSystem);
            Assert.NotEqual(a.EncounterId, b.EncounterId);
            Add(sampled, a.EncounterId, Canonical(1));
            Add(control, b.EncounterId, Canonical(1));
            Assert.Empty(a.Reservations);
            Assert.Empty(b.Reservations);
        }

        internal void RegisterReservations(BattleSnapshot first, BattleSnapshot second)
        {
            MagicCastReservation[] a = first.MagicVitals!.MagicSystem.Reservations.OrderBy(Role, StringComparer.Ordinal).ToArray();
            MagicCastReservation[] b = second.MagicVitals!.MagicSystem.Reservations.OrderBy(Role, StringComparer.Ordinal).ToArray();
            Assert.Equal(a.Length, b.Length);
            Assert.Equal(a.Length, a.Select(Role).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(b.Length, b.Select(Role).Distinct(StringComparer.Ordinal).Count());
            for (int index = 0; index < a.Length; index++)
            {
                Assert.NotEqual(a[index].ActionId, b[index].ActionId);
                Assert.Equal(Role(a[index]), Role(b[index]));
                Assert.Equal(a[index] with { ActionId = string.Empty }, b[index] with { ActionId = string.Empty });
                Add(sampled, a[index].ActionId, Canonical(index + 2));
                Add(control, b[index].ActionId, Canonical(index + 2));
                ValidateLinks(first, a[index]);
                ValidateLinks(second, b[index]);
            }
        }

        internal void AssertEqual(BattleSnapshot first, BattleSnapshot second)
        {
            Assert.Equal(JsonSerializer.Serialize(Normalize(first, sampled)), JsonSerializer.Serialize(Normalize(second, control)));
        }

        private static string Role(MagicCastReservation reservation) => $"{reservation.ActorRow}/{reservation.SpellId}/{reservation.Context}/{reservation.BankId}/{reservation.Count}";
        private static string Canonical(int role) => $"000000000000000000000000{role:X8}";

        private static void Add(Dictionary<string, string> map, string? original, string canonical)
        {
            Assert.True(Guid.TryParseExact(original, "N", out Guid value) && value != Guid.Empty, "The native host must supply a nonempty GUID nonce.");
            Assert.DoesNotContain(canonical, map.Values);
            Assert.True(map.TryAdd(original!, canonical), "Independent magic roles must not share an opaque identity.");
        }

        private static void ValidateLinks(BattleSnapshot snapshot, MagicCastReservation reservation)
        {
            List<(string? Id, string Actor, int Spell)> links = snapshot.QueuedActions.Select(command => (command.MagicReservationId, command.ActorId, (int)(command.NativeAction?.ActionId ?? command.ActionId))).ToList();
            if (snapshot.NativeCommandBanks is { } banks)
            {
                links.AddRange(banks.BoundCommands.Select(command => (command.MagicReservationId, command.ActorId, (int)(command.NativeAction?.ActionId ?? command.ActionId))));
            }
            if (snapshot.ActiveAction is { } active)
            {
                links.Add((active.MagicReservationId, active.ActorId, active.NativeAction?.ActionId ?? active.ActionId));
            }
            if (snapshot.NativeTimedAction is { Command: { } timed })
            {
                links.Add((timed.MagicReservationId, timed.ActorId, timed.NativeAction?.ActionId ?? timed.ActionId));
            }
            (string? Id, string Actor, int Spell)[] owners = links.Where(link => link.Id == reservation.ActionId).ToArray();
            Assert.NotEmpty(owners);
            Assert.All(owners, owner =>
            {
                Assert.Equal(reservation.SpellId, owner.Spell);
                Assert.Equal(reservation.ActorRow, snapshot.Definition.Combatants.Single(actor => actor.Id == owner.Actor).MagicRecordId);
            });
            Assert.Equal(MagicCastContext.Battle, reservation.Context);
            Assert.True(reservation.Count > 0);
        }

        private static BattleSnapshot Normalize(BattleSnapshot source, Dictionary<string, string> ids)
        {
            string? Reference(string? value)
            {
                if (value is null)
                {
                    return null;
                }
                Assert.True(ids.TryGetValue(value, out string? canonical), "An unregistered magic reservation appeared during the isolated action.");
                return canonical;
            }
            NativeCharacterVitals? Vitals(NativeCharacterVitals? original)
            {
                if (original is null)
                {
                    return null;
                }
                MagicSystemState magic = original.MagicSystem;
                return original with { MagicSystem = magic with
                {
                    EncounterId = magic.EncounterId is string encounter && ids.TryGetValue(encounter, out string? canonical) ? canonical : magic.EncounterId,
                    Reservations = magic.Reservations.Select(reservation => reservation with { ActionId = Reference(reservation.ActionId)! }).ToArray(),
                    CommittedActionIds = magic.CommittedActionIds.Select(value => ids.TryGetValue(value, out string? known) ? known : value).ToArray()
                } };
            }
            BattleAttackCommand Command(BattleAttackCommand command) => command with { MagicReservationId = Reference(command.MagicReservationId) };
            BattleActionPresentation Action(BattleActionPresentation action) => action with { MagicReservationId = Reference(action.MagicReservationId) };
            return source with
            {
                MagicVitals = Vitals(source.MagicVitals),
                Definition = source.Definition with { MagicVitals = Vitals(source.Definition.MagicVitals) },
                QueuedActions = source.QueuedActions.Select(Command).ToArray(),
                ActiveAction = source.ActiveAction is { } active ? Action(active) : null,
                NativeTimedAction = source.NativeTimedAction is { } timed ? timed with { Command = Action(timed.Command) } : null,
                NativeCommandBanks = source.NativeCommandBanks is { } banks ? banks with { Banks = banks.Banks.Select(bank => bank with { LogicalCommands = bank.LogicalCommands.Select(command => command is null ? null : Command(command)).ToImmutableArray() }).ToImmutableArray() } : null,
                GuardianForceCasts = source.GuardianForceCasts.Select(cast => cast with { Command = Command(cast.Command) }).ToArray()
            };
        }
    }

    private sealed record Uncovered(string Bank, byte Opcode, int Count, long FirstCallback);

    // This submission/state fixture uses a synthetic capture surface; GPU pixel fidelity is validated separately.
    private sealed class Capture : ITriangleRenderer, ILineRenderer, IFramebufferCaptureRenderer
    {
        private uint[] syntheticSurface = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            int length = checked(width * height);
            if (syntheticSurface.Length != length)
            {
                syntheticSurface = new uint[length];
                for (int index = 0; index < length; index++)
                {
                    syntheticSurface[index] = 0xFF000000u | unchecked((uint)index & 0x00FFFFFFu);
                }
            }
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
        }

        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
        }

        public bool TryCaptureArgb(out uint[] pixels)
        {
            pixels = syntheticSurface.ToArray();
            return syntheticSurface.Length != 0;
        }

        public void End()
        {
        }
    }
}
