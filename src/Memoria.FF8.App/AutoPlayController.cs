using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class AutoPlayRunState
{
    public int Version { get; set; } = 1;
    public int Seed { get; set; } = 1;
    public long Steps { get; set; }
    public AutoPlayPlannerState Coverage { get; set; } = new();
    public string? ActiveVisit { get; set; }
    public string? ActiveEvent { get; set; }
    public int ActiveField { get; set; }
    public int? ActiveStory { get; set; }
    public int ActiveObjectId { get; set; } = -1;
    public string? ActiveConditionSignature { get; set; }
    public List<AutoPlayChoiceSelection>? ActiveSelections { get; set; } = [];
    public AutoPlayEventRoute? ActiveTravelRoute { get; set; }
    public string? ActiveBeforeCheckpoint { get; set; }
    public string? ActiveTargetId { get; set; }
    public string? ActiveAbsenceGuardKey { get; set; }
    public string? ActiveAvailabilityFingerprint { get; set; }
    public ScriptedInputMetadata? ActiveObservedInput { get; set; }
    public bool ObserveContactInput { get; set; }
    public bool ObservedControlLoss { get; set; }
    public int SettleFrames { get; set; }
    public long SceneFrames { get; set; }
    public List<string> SceneChoices { get; set; } = [];
    public string? PrimaryContinuation { get; set; }
    public int PrimaryStory { get; set; }
    public int PrimaryFrontier { get; set; }
    public bool PrimaryConditionsChanged { get; set; }
    public List<AutoPlayChoiceSelection>? PrimarySelections { get; set; }
    public string? Snapshot { get; set; }
    public string? Error { get; set; }
    public bool ReachedEnding { get; set; }
    public Dictionary<string, int> ScriptedInputs { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ScriptedInputMetadata> ScriptedChoiceInputs { get; set; } = new(StringComparer.Ordinal);
    public int ScriptedInputSteps { get; set; }
    public Dictionary<string, int> DialogueSelections { get; set; } = new(StringComparer.Ordinal);
    public long LastProgressStep { get; set; }
    public int LastProgressScore { get; set; }
    public HashSet<string> ObservedScenes { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> ObservedMessages { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, NativePlayerAbsenceReceipt> UnavailabilityGuards { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AutoPlayVisitGuard> VisitGuards { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> InspectedVisitsSinceProgress { get; set; } = new(StringComparer.Ordinal);
    public string? RefreshTargetVisit { get; set; }
    public AutoPlaySurveyBaseline? SurveyBaseline { get; set; }
    public bool SurveyInvalidationUnknown { get; set; }
    public string? SurveyProgressBeforeInitialization { get; set; }
    public bool ParticipantPolicyDiagnosticOnly { get; set; }
    public string? ParticipantPolicyResumeChoice { get; set; }
}

/// <summary>The graphics host and command line drive this same autonomous, deterministic controller.</summary>
internal sealed partial class AutoPlayController
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private static readonly JsonSerializerOptions BeforeSnapshotJsonOptions = new(JsonOptions) { WriteIndented = false };
    private readonly GameSession session;
    private readonly Func<GameWorld> getWorld;
    private readonly Action<GameState> restore;
    private readonly string outputDirectory;
    private readonly TimeProvider time;
    private readonly TimeProvider deadlineTime;
    private long lastSave;
    private AutoPlayRunState run = new();
    private AutoPlayPlanner planner = new();
    private GameState? beforeStep;
    private string? beforeController;
    private string? runDirectory;
    private long lastProgressTimestamp;
    private readonly HashSet<GameWorld> watchedWorlds = [];
    private bool advancing;
    internal static readonly TimeSpan MaximumIdleTime = TimeSpan.FromSeconds(10);

    public AutoPlayController(GameSession session, Func<GameWorld> getWorld, Action<GameState> restore, string outputDirectory, TimeProvider? time = null, TimeProvider? deadlineTime = null)
    {
        this.session = session;
        this.getWorld = () =>
        {
            GameWorld world = getWorld();
            if (advancing && watchedWorlds.Add(world))
            {
                world.Events.OperationCompleted += CheckIdleDeadline;
                world.Events.OperationCompleted += ObserveSurveyOperation;
                world.Events.BeforeNativePartyActionRequest += ObserveParticipantAction;
            }
            if (advancing)
            {
                ObserveSurveyInitialization(world);
            }
            return world;
        };
        this.restore = state =>
        {
            if (advancing && run.SurveyBaseline is not null)
            {
                ObserveSurveySegmentChanges(this.getWorld());
            }
            ResetSurveySegment();
            restore(state);
        };
        this.outputDirectory = Path.GetFullPath(outputDirectory);
        this.time = time ?? TimeProvider.System;
        this.deadlineTime = deadlineTime ?? TimeProvider.System;
        ResetIdleDeadline();
    }

    public bool Running { get; private set; }
    public bool Paused { get; private set; }
    public string Status { get; private set; } = "Ready; fixed native cold-process seed 1.";
    public string? Error { get; private set; }
    public string? ReproductionPath { get; private set; }
    public string? ResumeCommand => ReproductionPath is null ? null : $"--auto-play --resume \"{ReproductionPath}\"";
    internal AutoPlayRunState State => run;

    public void Start()
    {
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        restore(cold.State);
        run = new();
        planner = new(run.Coverage, (visit, item) => AutoPlayTraversalPolicy.PhaseOrder(visit.Region, item.Order), AutoPlayTraversalPolicy.ProgressionEntryOrder);
        runDirectory = Path.Combine(outputDirectory, time.GetUtcNow().ToString("yyyyMMdd-HHmmss-fffffff"));
        Directory.CreateDirectory(runDirectory);
        Running = true;
        Paused = false;
        Error = null;
        ReproductionPath = null;
        lastSave = time.GetTimestamp();
        Status = "Starting New Game; seed 1.";
        ResetIdleDeadline();
        // This initial standalone also reproduces field allocation failures.
        SaveBundle("start", cold.State);
    }

    public void Pause()
    {
        if (Paused)
        {
            return;
        }
        Paused = true;
        Status = "Paused. " + Status;
    }

    public void Resume()
    {
        if (Error is null)
        {
            Running = true;
            Paused = false;
            ResetIdleDeadline();
        }
    }

    public void Stop()
    {
        Running = false;
        Paused = false;
        Status = "Stopped. " + Status;
    }

    public void SaveAndStop()
    {
        ReproductionPath = SaveBundle($"stopped-{run.Steps:D10}", CaptureState());
        Stop();
    }

    public void ReplayFailure()
    {
        ResumeFrom(ReproductionPath ?? throw new InvalidOperationException("No failure reproduction has been saved."));
    }

    public void ReportHostFailure(Exception exception)
    {
        if (!Running)
        {
            return;
        }
        beforeController = JsonSerializer.Serialize(run, BeforeSnapshotJsonOptions);
        beforeStep = session.State;
        try
        {
            beforeStep = CaptureState();
        }
        catch
        {
            // Retain the last serializable session if presentation also prevents capture.
        }
        Fail(exception);
    }

    public void ResumeFrom(string manifest)
    {
        manifest = Path.GetFullPath(manifest);
        string json = File.ReadAllText(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(json, JsonOptions) ?? throw new InvalidDataException("Empty automatic play manifest.");
        if (saved.ParticipantPolicyDiagnosticOnly)
        {
            throw new NotSupportedException("This participant-policy bundle preserves a diagnostic callback boundary. Resume the separately saved authentic pre-choice or pre-event return bundle.");
        }
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(nameof(AutoPlayRunState.ActiveSelections), out _) && saved.ActiveEvent is not null)
        {
            saved.ActiveSelections = null;
        }
        if (!document.RootElement.TryGetProperty(nameof(AutoPlayRunState.ActiveConditionSignature), out _) && saved.ActiveVisit is string activeVisit && saved.ActiveEvent is string activeEvent)
        {
            saved.ActiveConditionSignature = saved.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == activeVisit)?.Events.SingleOrDefault(item => item.Event.Id == activeEvent)?.Conditions?.ActiveEntry;
        }
        if (saved.Version != 1 || saved.Seed != 1 || saved.Snapshot is null)
        {
            throw new InvalidDataException("Unsupported automatic play manifest or seed.");
        }
        string directory = Path.GetDirectoryName(manifest)!;
        restore(GameSession.Load(Path.Combine(directory, saved.Snapshot)));
        runDirectory = Path.Combine(outputDirectory, time.GetUtcNow().ToString("yyyyMMdd-HHmmss-fffffff") + "-resume");
        Directory.CreateDirectory(runDirectory);
        Dictionary<string, string> imported = new(StringComparer.OrdinalIgnoreCase);
        string? ImportCheckpoint(string? checkpoint)
        {
            if (checkpoint is null)
            {
                return null;
            }
            string source = Path.GetFullPath(checkpoint, directory);
            if (imported.TryGetValue(source, out string? existing))
            {
                return existing;
            }
            byte[] bytes = File.ReadAllBytes(source);
            string filename = "import-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".json";
            string destination = Path.Combine(runDirectory!, filename);
            if (!File.Exists(destination))
            {
                File.WriteAllBytes(destination, bytes);
            }
            imported.Add(source, filename);
            return filename;
        }
        foreach (AutoPlayChoiceCoverage choice in saved.Coverage.Choices)
        {
            foreach (string scope in choice.PolicyCheckpoints.Keys.ToArray())
            {
                AutoPlayPolicyChoiceCheckpoint checkpoint = choice.PolicyCheckpoints[scope];
                choice.PolicyCheckpoints[scope] = checkpoint with { BeforeCheckpoint = ImportCheckpoint(checkpoint.BeforeCheckpoint), ContinuationCheckpoint = ImportCheckpoint(checkpoint.ContinuationCheckpoint) };
            }
            if (choice.Finished)
            {
                choice.BeforeCheckpoint = null;
                choice.ContinuationCheckpoint = null;
            }
            else
            {
                choice.BeforeCheckpoint = ImportCheckpoint(choice.BeforeCheckpoint);
                choice.ContinuationCheckpoint = ImportCheckpoint(choice.ContinuationCheckpoint);
            }
        }
        saved.PrimaryContinuation = ImportCheckpoint(saved.PrimaryContinuation);
        saved.ActiveBeforeCheckpoint = ImportCheckpoint(saved.ActiveBeforeCheckpoint);
        run = saved;
        if (!document.RootElement.TryGetProperty(nameof(AutoPlayRunState.SurveyBaseline), out _) && run.ActiveEvent is not null)
        {
            run.SurveyInvalidationUnknown = true;
        }
        run.Error = null;
        run.LastProgressStep = run.Steps;
        planner = new(run.Coverage, (visit, item) => AutoPlayTraversalPolicy.PhaseOrder(visit.Region, item.Order), AutoPlayTraversalPolicy.ProgressionEntryOrder);
        Running = true;
        Paused = false;
        Error = null;
        ReproductionPath = manifest;
        lastSave = time.GetTimestamp();
        Status = $"Resumed at step {run.Steps}.";
        ResetIdleDeadline();
        SaveBundle("resume-start", session.State);
    }

    /// <summary>Runs immediately, bounded by wall time only for keeping the graphical host responsive.</summary>
    public void Advance(int stepBudget = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stepBudget, 1);
        if (!Running || Paused)
        {
            return;
        }
        long started = Stopwatch.GetTimestamp();
        for (int step = 0; step < stepBudget && Running && !Paused; step++)
        {
            try
            {
                advancing = true;
                beforeStep = session.State;
                beforeController = JsonSerializer.Serialize(run, BeforeSnapshotJsonOptions);
                beforeStep = CaptureState();
                CheckIdleDeadline();
                Tick();
                run.Steps++;
                int score = run.ObservedScenes.Count + run.ObservedMessages.Count + run.Coverage.Visits.Count + run.Coverage.Choices.Count + run.Coverage.Visits.Sum(visit => visit.Events.Count(item => item.Completed) + visit.Events.Sum(item => item.UnavailableAttempts.Count + item.ParticipantPolicyDeferrals.Count)) + run.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
                if (score != run.LastProgressScore)
                {
                    run.LastProgressScore = score;
                    run.LastProgressStep = run.Steps;
                    ResetIdleDeadline();
                }
                else if (Running && run.Steps - run.LastProgressStep > 10000)
                {
                    throw new TimeoutException("No new location, event or choice coverage in 10000 controller steps. A reproducible loop was preserved.");
                }
                if (time.GetElapsedTime(lastSave) >= TimeSpan.FromMinutes(1))
                {
                    SaveBundle($"minute-{run.Steps:D10}", CaptureState());
                    lastSave = time.GetTimestamp();
                }
            }
            catch (AutoPlayParticipantPolicyException exception)
            {
                try
                {
                    DeferParticipantAction(exception.Request);
                    run.Steps++;
                }
                catch (Exception failure)
                {
                    Fail(failure);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
            finally
            {
                advancing = false;
                foreach (GameWorld watched in watchedWorlds)
                {
                    watched.Events.OperationCompleted -= CheckIdleDeadline;
                    watched.Events.OperationCompleted -= ObserveSurveyOperation;
                    watched.Events.BeforeNativePartyActionRequest -= ObserveParticipantAction;
                }
                watchedWorlds.Clear();
            }
            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(12))
            {
                break;
            }
        }
    }

    internal void ResetIdleDeadline()
    {
        lastProgressTimestamp = deadlineTime.GetTimestamp();
    }

    internal void CheckIdleDeadline()
    {
        if (deadlineTime.GetElapsedTime(lastProgressTimestamp) >= MaximumIdleTime)
        {
            throw new TimeoutException("Automatic play made no new scene, dialogue, event or branch progress for 10 seconds. The pre-failure state is preserved.");
        }
    }

    private GameState CaptureState()
    {
        GameWorld world = getWorld();
        if (world.Field is not null)
        {
            if (session.State.Scene == SceneKind.WorldMap || world.HasCurrentFieldAllocation)
            {
                session.SetEventState(world.Events.Capture());
            }
            else
            {
                session.SetDepartingFieldState(world.Events.Capture());
            }
        }
        return session.State;
    }

    private void Tick()
    {
        GameWorld world = getWorld();
        if (world.Events.FieldId != session.State.FieldId || session.State.Scene == SceneKind.WorldMap)
        {
            ObserveSurveySegmentChanges(world);
        }
        world.EnsureField();
        ObserveSurveyInitialization(world);
        if (run.ParticipantPolicyResumeChoice is string policyChoice)
        {
            ResumeParticipantChoice(policyChoice);
            return;
        }
        if (!world.Events.IsUserControlEnabled && run.ActiveEvent is not null)
        {
            run.ObservedControlLoss = true;
        }
        bool observedProgress = run.ObservedScenes.Add($"{session.State.Scene}:{VisitKey()}:{(session.State.Scene == SceneKind.Movie ? session.State.MovieIndex : -1)}");
        if (world.CurrentDialogue is { } observed)
        {
            observedProgress |= run.ObservedMessages.Add($"{VisitKey()}:{observed.ObjectId}:{observed.ScriptId}:{observed.Operation}:{observed.MessageId}");
        }
        if (observedProgress)
        {
            ResetIdleDeadline();
        }
        if (world.Events.LastFault is { } fault)
        {
            throw new EventScriptException(fault.Message);
        }
        if (session.State.FieldId == 76 && IsEnding(session.State, world.Events.Capture()))
        {
            run.ReachedEnding = true;
            ReproductionPath = SaveBundle($"ending-{run.Steps:D10}", CaptureState());
            Running = false;
            Status = "Ending reached. Inspect coverage for conditional or unreachable events.";
            return;
        }
        int deferred = run.Coverage.Visits.Sum(visit => visit.Events.Sum(item => item.ParticipantPolicyDeferrals.Count));
        Status = $"{session.State.FieldId}:{session.State.FieldName} · frame {world.Events.Frame} · {run.Coverage.Visits.Count} visits · {run.Steps} steps" + (deferred == 0 ? string.Empty : $" · {deferred} deferred");
        if (world.PendingEntityName is not null)
        {
            Apply(new(PlayerActionKind.EnterName));
            return;
        }
        if (session.State.Scene == SceneKind.Movie)
        {
            run.SettleFrames = 0;
            Apply(new(PlayerActionKind.SkipMovie));
            return;
        }
        if (world.Events.Scene.NativeTutorial is not null || world.Events.Scene.NativeMenuTutorial is not null)
        {
            run.SettleFrames = 0;
            Apply(new(world.Tutorial is null ? PlayerActionKind.Cancel : PlayerActionKind.Confirm, Frames: 300));
            return;
        }
        if (world.CurrentDialogue is { CanConfirm: true } dialogue)
        {
            run.ActiveObservedInput = null;
            run.ObserveContactInput = false;
            HandleDialogue(dialogue);
            run.SettleFrames = 0;
            return;
        }
        if (world.Events.PartyMenu is not null)
        {
            run.SettleFrames = 0;
            Apply(GameWorld.PlanAutoPlayPartyInput(world.Events.PartyMenu));
            return;
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            ExploreWorld(world);
            return;
        }
        if (world.Events.PlayerActor is null && run.ActiveField != session.State.FieldId && TryBacktrackNativeUnavailable(world))
        {
            return;
        }
        if (!world.Events.IsUserControlEnabled && session.State.FieldButtonInput is { PhysicalHeld: 0 } buttons && (buttons.PendingPulse != 0 || buttons.RawHeld != 0))
        {
            world.AdvanceAutoPlayInputFrame();
            return;
        }
        if (world.Events.IsUserControlEnabled && world.Events.PlayerActor is { NativeLadder: { Active: true, AutomaticCompletion: false } ladder, NativeMovementCompletion542: 1 } ladderPlayer && TryDriveOwnedLadderInput(world, ladderPlayer, ladder))
        {
            return;
        }
        if (run.ActiveObservedInput is { Masks.Count: > 0 } observedInput && run.ActiveField == session.State.FieldId)
        {
            run.ActiveObservedInput = null;
            run.ObserveContactInput = false;
            DriveScriptedInput(observedInput, world.Events.IsUserControlEnabled);
            return;
        }
        if (world.Events.IsUserControlEnabled && run.ActiveField == session.State.FieldId && world.Events.HasPendingPlayerInteraction(run.ActiveObjectId) && world.Events.DiscoverScriptedInput(run.ActiveObjectId) is { Masks.Count: > 0 } controlledInput)
        {
            DriveWhenQueried(controlledInput, true, run.ActiveObjectId);
            return;
        }
        if (!world.Events.IsUserControlEnabled && world.Events.DiscoverScriptedInput() is { Masks.Count: > 0 } input)
        {
            DriveWhenQueried(input, false, null);
            return;
        }
        if (world.NextTrigger.Kind != PlayerTriggerKind.Playable || !world.Events.IsUserControlEnabled || !run.ObservedControlLoss && run.ActiveField == session.State.FieldId && world.Events.HasPendingPlayerInteraction(run.ActiveObjectId))
        {
            run.SettleFrames = 0;
            if (run.SceneFrames > 180000)
            {
                throw new TimeoutException("No control-return boundary within 180000 simulation frames. Inspect the saved wait/input contract.");
            }
            run.SceneFrames += Apply(new(PlayerActionKind.Wait, Frames: 300)).FramesAdvanced;
            return;
        }
        if (run.SettleFrames < 4)
        {
            Apply(new(PlayerActionKind.Wait));
            run.SettleFrames++;
            return;
        }
        run.SceneFrames = 0;
        run.ScriptedInputSteps = 0;
        string visit = VisitKey();
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        planner.Observe(new(visit, world.AutoPlayRegion), targets.GroupBy(target => target.CoverageId).Select(group =>
        {
            AutoPlayTarget target = group.First();
            return new AutoPlayEvent(target.CoverageId, target.Kind == AutoPlayTargetKind.Gateway || target.DestinationField is not null ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ChangesStory: target.ChangesStory, Order: target.Order, ConditionSignature: world.ReadAutoPlayConditions(target.CoverageId, target.ObjectId).Fingerprint, Variants: group.Select(variant => variant.Id).Order(StringComparer.Ordinal).ToArray());
        }).ToArray());
        RefreshUnavailable(world);
        RefreshParticipantPolicies(world);
        InspectSurveyVisit(visit);
        if (FinishChoices())
        {
            return;
        }
        if (run.ActiveVisit is not null && run.ActiveEvent is not null)
        {
            RecordActiveControlReturn();
            run.ActiveVisit = null;
            run.ActiveEvent = null;
            run.ActiveStory = null;
            run.ActiveObservedInput = null;
            run.ObserveContactInput = false;
            run.ActiveTravelRoute = null;
            run.ActiveBeforeCheckpoint = null;
            run.ActiveTargetId = null;
            run.ActiveAbsenceGuardKey = null;
            run.ActiveAvailabilityFingerprint = null;
        }
        if (world.CaptureAutoPlayVisitGuard(targets) is { } guard)
        {
            run.VisitGuards[visit] = guard;
        }
        AutoPlayPlanStep? next = planner.Plan(visit, candidate => NeedsVisitRefresh(world, candidate), run.RefreshTargetVisit);
        run.RefreshTargetVisit = next?.RefreshVisitKey;
        if (next is null)
        {
            WaitForNativeFrontier();
            return;
        }
        AutoPlayEventCoverage currentEvent = run.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == next.Event.Id);
        AutoPlayTarget[] variants = targets.Where(item => item.CoverageId == next.Event.Id && !currentEvent.UnavailableVariants.Contains(item.Id) && !currentEvent.PolicyDeferredVariants.Contains(item.Id) && (next.Route?.TargetId is not string recordedTarget || item.Id == recordedTarget)).OrderBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        AutoPlayTarget target = variants.FirstOrDefault() ?? throw new InvalidDataException("Planned event is no longer available in the current field.");
        string beforeCheckpoint = $"event-{run.Steps:D10}.json";
        SaveBundle(Path.GetFileNameWithoutExtension(beforeCheckpoint), CaptureState());
        int variantAttempt = 0;
        string BeforeVariant()
        {
            BeginSurveySegment(world);
            if (variantAttempt++ == 0)
            {
                return beforeCheckpoint;
            }
            string name = $"event-{run.Steps:D10}-attempt-{variantAttempt}";
            SaveBundle(name, CaptureState());
            return name + ".json";
        }
        void ActivateTarget(AutoPlayTarget selected, string checkpoint)
        {
            RefreshUnavailable(world);
            planner.BeginEvent(visit, selected.CoverageId);
            run.ActiveVisit = visit;
            run.ActiveEvent = selected.CoverageId;
            run.ActiveField = world.Events.FieldId;
            run.ActiveStory = world.Events.Variables.GetValueOrDefault(256);
            run.ActiveObjectId = selected.ObjectId;
            run.ActiveConditionSignature = world.ReadAutoPlayConditions(selected.CoverageId, selected.ObjectId).Fingerprint;
            run.ActiveSelections = [];
            run.ActiveTravelRoute = next.Route;
            run.ActiveBeforeCheckpoint = checkpoint;
            run.ActiveTargetId = selected.Id;
            run.ActiveAbsenceGuardKey = null;
            run.ActiveAvailabilityFingerprint = run.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == selected.CoverageId).AvailabilityFingerprint;
            run.ActiveObservedInput = null;
            run.ObserveContactInput = selected.Kind != AutoPlayTargetKind.Talk;
            run.ObservedControlLoss = false;
            run.SettleFrames = 0;
        }
        if (target.Kind == AutoPlayTargetKind.Talk)
        {
            bool accepted = false;
            foreach (AutoPlayTarget variant in variants)
            {
                ActivateTarget(variant, BeforeVariant());
                world.Events.TeleportPlayer(variant.X, variant.Y, variant.Z, variant.Triangle, variant.Direction);
                PlayerActionResult result = world.Apply(new(PlayerActionKind.Interact, variant.ObjectId));
                if (result.Accepted)
                {
                    accepted = true;
                    break;
                }
            }
            if (!accepted)
            {
                planner.Block(visit, target.CoverageId, "No discovered approach selects this actor with the ordinary talk query.");
                run.ActiveVisit = null;
                run.ActiveEvent = null;
                run.ActiveStory = null;
                run.ActiveBeforeCheckpoint = null;
                run.ActiveTargetId = null;
            }
        }
        else
        {
            bool triggered = false;
            foreach (AutoPlayTarget variant in variants)
            {
                string checkpoint = BeforeVariant();
                FieldTeleportPosition? approach = null;
                if (variant.ApproachX is float approachX && variant.ApproachY is float approachY)
                {
                    approach = new(approachX, approachY, variant.ApproachZ ?? variant.Z, variant.ApproachTriangle ?? variant.Triangle, variant.Direction);
                }
                FieldTeleportContactResult placement = world.Events.TeleportPlayerAndDispatchContacts(new(variant.X, variant.Y, variant.Z, variant.Triangle, variant.Direction), approach);
                if (placement.NativeInterrupted)
                {
                    run.ActiveVisit = null;
                    run.ActiveEvent = null;
                    run.ActiveStory = null;
                    run.ActiveBeforeCheckpoint = null;
                    run.ActiveTargetId = null;
                    run.ActiveObjectId = -1;
                    run.ActiveObservedInput = null;
                    run.ObserveContactInput = false;
                    Apply(new(PlayerActionKind.Wait));
                    return;
                }
                ActivateTarget(variant, checkpoint);
                bool contacted = placement.ContactTriggered;
                if (variant.RequiresConfirm)
                {
                    Apply(new(PlayerActionKind.Confirm));
                    contacted = world.Events.HasPendingPlayerInteraction(variant.ObjectId);
                }
                if (contacted)
                {
                    triggered = true;
                    break;
                }
            }
            if (!triggered)
            {
                planner.Block(visit, target.CoverageId, "None of the discovered coordinate approaches activated a native contact callback.");
                run.ActiveVisit = null;
                run.ActiveEvent = null;
                run.ActiveStory = null;
                run.ActiveBeforeCheckpoint = null;
                run.ActiveTargetId = null;
                run.ActiveObjectId = -1;
            }
            Apply(new(PlayerActionKind.Wait));
        }
    }

    private PlayerActionResult ApplyContactInput(PlayerAction action)
    {
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        void ObserveQuery(ScriptCommand command)
        {
            if (run.ObserveContactInput && run.ActiveEvent is not null && run.ActiveField == field && command.ObjectId == run.ActiveObjectId && command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON")
            {
                // Continuous contact callbacks can query and return in one frame, leaving no active fiber at the next controller tick.
                if (world.Events.DiscoverScriptedInput(command.ObjectId) is { Masks.Count: > 0 } input)
                {
                    run.ActiveObservedInput = input;
                    run.ObserveContactInput = false;
                }
            }
        }
        world.Events.CommandExecuted += ObserveQuery;
        try
        {
            return world.Apply(action);
        }
        finally
        {
            world.Events.CommandExecuted -= ObserveQuery;
        }
    }

    private void WaitForNativeFrontier()
    {
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        ScriptedInputMetadata? observedInput = null;
        int observedObject = -1;
        void ObserveQuery(ScriptCommand command)
        {
            if (observedInput is null && command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" && world.Events.DiscoverScriptedInput(command.ObjectId) is { Masks.Count: > 0 } input)
            {
                observedInput = input;
                observedObject = command.ObjectId;
            }
        }
        world.Events.CommandExecuted += ObserveQuery;
        PlayerActionResult result;
        try
        {
            result = Apply(new(PlayerActionKind.Wait, Frames: 300, WaitWhilePlayable: true));
        }
        finally
        {
            world.Events.CommandExecuted -= ObserveQuery;
        }
        if (result.NativeInputObserved && session.State.Scene == SceneKind.Field && session.State.FieldId == field && world.CurrentDialogue is not { CanConfirm: true })
        {
            run.ActiveObservedInput = observedInput ?? throw new NotSupportedException("The native frontier wait sampled a scripted input query without supported physical-input metadata.");
            run.ActiveField = field;
            run.ActiveObjectId = observedObject;
            run.ActiveSelections = [];
        }
    }

    private void DriveWhenQueried(ScriptedInputMetadata input, bool allowControlled, int? objectId)
    {
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        Apply(new(PlayerActionKind.FieldButtons, 0));
        if (!HasNeutralScriptedInput(session.State.FieldButtonInput, field, world.Events.Frame))
        {
            world.AdvanceAutoPlayInputFrame();
            return;
        }
        GameState checkpoint = CaptureState();
        bool queried = false;
        void ObserveQuery(ScriptCommand command)
        {
            queried |= command.FieldId == field && command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" && (objectId is null || command.ObjectId == objectId) && input.Queries.Any(query => query.ObjectId == command.ObjectId && query.ScriptId == command.ScriptId && query.Operation == command.Operation && query.Opcode == command.Name);
        }
        world.Events.CommandExecuted += ObserveQuery;
        try
        {
            world.AdvanceAutoPlayInputFrame();
        }
        finally
        {
            world.Events.CommandExecuted -= ObserveQuery;
        }
        if (queried && HasScriptedInputOwner(world, field, allowControlled) && world.CurrentDialogue is not { CanConfirm: true } && PrepareScriptedInput(allowControlled))
        {
            // A neutral callback proves the real query. Replay its genuine before-state so preparation cannot consume the input window.
            restore(checkpoint);
            getWorld().EnsureField();
            DriveScriptedInput(input, allowControlled, prepared: true);
        }
    }

    internal static bool HasNeutralScriptedInput(NativeFieldButtonInput? input, int field, long frame)
    {
        return input is { PhysicalHeld: 0, RawHeld: 0, RawPressed: 0, PendingPulse: 0, Held: 0, Pressed: 0 } && input.SampledFieldId == field && input.SampledFrame == frame;
    }

    private bool TryDriveOwnedLadderInput(GameWorld world, ActorState player, NativeLadderState owner)
    {
        int field = world.Events.FieldId;
        bool HasOwner()
        {
            return !owner.AutomaticCompletion && session.State.Scene == SceneKind.Field && session.State.FieldId == field && world.Events.FieldId == field && world.Events.IsUserControlEnabled && ReferenceEquals(world.Events.PlayerActor, player) && player.NativeMovementCompletion542 == 1 && player.NativeLadder is { Active: true, AutomaticCompletion: false } current && current.FiberSequence == owner.FiberSequence && current.ScriptId == owner.ScriptId && current.Operation == owner.Operation && current.Up == owner.Up && world.CurrentDialogue is null && world.PendingEntityName is null && world.Events.PartyMenu is null && !world.Events.HasPendingBattle && world.Events.PendingTripleTriad is null && world.Events.Scene.NativeTutorial is null && world.Events.Scene.NativeMenuTutorial is null;
        }
        if (!HasOwner())
        {
            return false;
        }
        ushort direction = owner.Up ? (ushort)0x1000 : (ushort)0x4000;
        ushort physical = (session.State.InputConfiguration ?? throw new NotSupportedException("Controlled native ladder input requires its actual saved input mapping.")).RawForMappedPulse(direction);
        bool driven = false;
        try
        {
            for (int callback = 0; callback < 16 && HasOwner(); callback++)
            {
                Apply(new(PlayerActionKind.FieldButtons, physical));
                world.AdvanceAutoPlayInputFrame();
                driven = true;
            }
        }
        finally
        {
            // Release the input device without inventing a sampled callback or button edge.
            if (session.State.FieldButtonInput is { } input)
            {
                session.SetFieldButtonInput(input with { PhysicalHeld = 0 });
            }
        }
        return driven;
    }

    private void DriveScriptedInput(ScriptedInputMetadata input, bool allowControlled, bool prepared = false)
    {
        if (!prepared && !PrepareScriptedInput(allowControlled))
        {
            return;
        }
        if (++run.ScriptedInputSteps > 10000)
        {
            throw new NotSupportedException("Scripted input exploration exceeded 10000 attempts without a control-return boundary.");
        }
        int attempt = run.ScriptedInputs.GetValueOrDefault(input.Fingerprint);
        run.ScriptedInputs[input.Fingerprint] = attempt + 1;
        string key = $"input:{session.State.FieldId}:{input.Fingerprint}";
        run.ScriptedChoiceInputs.TryAdd(key, input);
        planner.ObserveChoice(key, input.Masks.Count);
        AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(item => item.Key == key);
        RefreshParticipantChoice(choice);
        int selection = attempt % input.Masks.Count;
        if (run.ActiveTravelRoute is not null && TrySelectTravelChoice(key, 0, input.Masks.Count - 1, out int recorded))
        {
            selection = recorded;
        }
        else if (choice.ActiveOption is null && !choice.Finished)
        {
            AutoPlayChoiceStep next = planner.PlanChoice(key);
            if (next.Kind == AutoPlayChoiceStepKind.SaveBeforeChoice)
            {
                RegisterChoiceSnapshot(key);
                choice.InputCheckpointPrepared = true;
                next = planner.PlanChoice(key);
            }
            if (next.Selection is int planned)
            {
                selection = planned;
                planner.BeginChoiceOption(key, selection);
                run.SceneChoices.Add(key);
            }
        }
        run.ActiveSelections?.Add(new(key, selection));
        PulseScriptedInput(input.Masks[selection], input, allowControlled, prepared: true);
    }

    private bool PrepareScriptedInput(bool allowControlled)
    {
        Apply(new(PlayerActionKind.FieldButtons, 0));
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        for (int release = 0; release < 2 && HasScriptedInputOwner(world, field, allowControlled); release++)
        {
            world.AdvanceAutoPlayInputFrame();
        }
        return HasScriptedInputOwner(world, field, allowControlled);
    }

    private bool HasScriptedInputOwner(GameWorld world, int field, bool allowControlled)
    {
        return session.State.Scene == SceneKind.Field && session.State.FieldId == field && (allowControlled || !world.Events.IsUserControlEnabled) && world.Events.Scene.NativeTutorial is null && world.Events.Scene.NativeMenuTutorial is null && world.Events.PartyMenu is null;
    }

    private void PulseScriptedInput(int mask, ScriptedInputMetadata input, bool allowControlled, bool prepared = false)
    {
        if (!prepared && !PrepareScriptedInput(allowControlled))
        {
            throw new NotSupportedException($"Scripted input {input.Fingerprint} lost its field owner before physical mask {mask} was sampled.");
        }
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        bool HasOwner() => HasScriptedInputOwner(world, field, allowControlled);
        if (!HasOwner())
        {
            throw new NotSupportedException($"Scripted input {input.Fingerprint} lost its field owner before physical mask {mask} was sampled.");
        }
        bool sampled = false;
        void Observe(ScriptCommand command)
        {
            if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" && command.Arguments.Count == 1 && input.Queries.Any(query => query.ObjectId == command.ObjectId && query.ScriptId == command.ScriptId && query.Operation == command.Operation))
            {
                NativeFieldButtonInput buttons = session.State.FieldButtonInput!;
                int value = command.Name == "KEYSCAN2" ? buttons.RawHeld : command.Name == "KEYON" ? buttons.Pressed ?? 0 : buttons.Held ?? 0;
                sampled |= (buttons.PhysicalHeld & mask) == mask && (command.Arguments[0] & value) != 0;
            }
        }
        world.Events.CommandExecuted += Observe;
        try
        {
            for (int hold = 1, callbacks = 0; callbacks < 256 && HasOwner() && !sampled; hold++)
            {
                if (hold > 1 || !prepared)
                {
                    Apply(new(PlayerActionKind.FieldButtons, 0));
                    world.AdvanceAutoPlayInputFrame();
                    callbacks++;
                    if (!HasOwner())
                    {
                        break;
                    }
                }
                Apply(new(PlayerActionKind.FieldButtons, mask));
                for (int held = 0; held < hold && callbacks < 256 && HasOwner() && !sampled; held++)
                {
                    world.AdvanceAutoPlayInputFrame();
                    callbacks++;
                }
            }
        }
        finally
        {
            world.Events.CommandExecuted -= Observe;
            // Physical release belongs to the process input device even when a callback changes scene.
            if (session.State.FieldButtonInput is { } sampler)
            {
                session.SetFieldButtonInput(sampler with { PhysicalHeld = 0 });
            }
        }
        if (!sampled)
        {
            throw new NotSupportedException($"Scripted input {input.Fingerprint} did not sample physical mask {mask} within 256 native callbacks; the alternative is not completed.");
        }
        run.SettleFrames = 0;
        run.SceneFrames = 0;
    }

    private void RegisterChoiceSnapshot(string key)
    {
        AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(item => item.Key == key);
        string identity = choice.PolicyScope is null ? key : JsonSerializer.Serialize(new { key, choice.PolicyScope });
        string name = "choice-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16].ToLowerInvariant() + ".json";
        SaveGame(Path.Combine(runDirectory!, name), CaptureState());
        planner.RegisterChoiceCheckpoint(key, name);
        run.Coverage.Choices.Single(choice => choice.Key == key).BeforeSelections = run.ActiveSelections?.ToList();
    }

    private void ExploreWorld(GameWorld world)
    {
        WorldState current = session.State.World ?? throw new InvalidDataException("Automatic world exploration requires its current world owner.");
        int waitFrames = WorldControlWaitFrames(current);
        if (waitFrames > 0)
        {
            run.SceneFrames += Apply(new(PlayerActionKind.Wait, Frames: waitFrames)).FramesAdvanced;
            if (run.SceneFrames > 180000)
            {
                throw new TimeoutException("The native world owner did not return control within 180000 callbacks.");
            }
            return;
        }
        string visit = "world:" + session.State.Variables.GetValueOrDefault(256);
        string signature = WorldConditionSignature(session.State);
        IReadOnlyList<WorldAutoPlayTarget> targets = world.DiscoverAutoPlayWorldTargets();
        planner.Observe(new(visit, "world"), targets.Select(target => new AutoPlayEvent(target.Id, AutoPlayEventKind.Transition, Order: target.Order, ConditionSignature: signature)).ToArray());
        RefreshUnavailable(world);
        InspectSurveyVisit(visit);
        if (FinishChoices())
        {
            return;
        }
        if (run.ActiveVisit is not null && run.ActiveEvent is not null)
        {
            RecordActiveControlReturn();
        }
        run.ActiveVisit = null;
        run.ActiveEvent = null;
        run.ActiveStory = null;
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        run.ActiveTravelRoute = null;
        run.ActiveBeforeCheckpoint = null;
        run.ActiveTargetId = null;
        run.ActiveAbsenceGuardKey = null;
        run.ActiveAvailabilityFingerprint = null;
        run.SceneFrames = 0;
        AutoPlayPlanStep next = planner.Plan(visit, run.RefreshTargetVisit is null ? null : _ => false, run.RefreshTargetVisit) ?? throw new NotSupportedException("Reachable world frontier exhausted before the ending.");
        run.RefreshTargetVisit = next.RefreshVisitKey;
        WorldAutoPlayTarget target = targets.Single(item => item.Id == next.Event.Id);
        string beforeCheckpoint = $"world-{run.Steps:D10}.json";
        SaveBundle(Path.GetFileNameWithoutExtension(beforeCheckpoint), CaptureState());
        BeginSurveySegment(world);
        planner.BeginEvent(visit, target.Id);
        run.ActiveVisit = visit;
        run.ActiveEvent = target.Id;
        run.ActiveField = session.State.FieldId;
        run.ActiveStory = session.State.Variables.GetValueOrDefault(256);
        run.ActiveObjectId = -1;
        run.ActiveConditionSignature = signature;
        run.ActiveSelections = [];
        run.ActiveTravelRoute = next.Route;
        run.ActiveBeforeCheckpoint = beforeCheckpoint;
        run.ActiveTargetId = target.Id;
        run.ActiveAbsenceGuardKey = null;
        run.ActiveAvailabilityFingerprint = run.Coverage.Visits.Single(item => item.Visit.Key == visit).Events.Single(item => item.Event.Id == target.Id).AvailabilityFingerprint;
        run.ObservedControlLoss = false;
        run.SettleFrames = 0;
        world.ExecuteAutoPlayWorldTarget(target);
    }

    internal static int WorldControlWaitFrames(WorldState world)
    {
        if (world.NativeScriptedRuntime is not null || world.NativeTrainRuntime is not null)
        {
            return 300;
        }
        NativeWorldEventUi ui = world.EventUi ?? throw new NotSupportedException("Automatic world control requires its executed native UI state.");
        ui.Validate();
        int entryDelay = NativeWorldEntryControl.DelayAfterCallback(world.Frame, 0);
        if (entryDelay > 0)
        {
            return entryDelay;
        }
        return ui.ScriptMode != 0 || !ui.Windows.IsEmpty ? 1 : 0;
    }

    internal static void RecordWorldControlReturn(AutoPlayPlanner planner, string activeVisit, string eventId, string currentVisit, int? entryStory, int story, IReadOnlyList<AutoPlayChoiceSelection>? selections = null, string? entrySignature = null, bool hasChoiceOutcome = false, bool entrySignatureAuthoritative = false, string? entryAvailabilityFingerprint = null, bool availabilityFingerprintAuthoritative = false, string? targetId = null)
    {
        AutoPlayEventCoverage item = planner.State.Visits.Single(visit => visit.Visit.Key == activeVisit).Events.Single(item => item.Event.Id == eventId);
        if (activeVisit.StartsWith("world:", StringComparison.Ordinal) && !hasChoiceOutcome && selections is not { Count: > 0 } && item.Routes.Count == 0)
        {
            planner.Block(activeVisit, eventId, "The native world event returned control without entering the requested field.");
            return;
        }
        planner.Completed(activeVisit, eventId, activeVisit == currentVisit ? null : currentVisit, observedStoryChanged: ObserveStoryChange(entryStory, activeVisit, story), selections: selections, entrySignature: entrySignature, entrySignatureAuthoritative: entrySignatureAuthoritative, entryAvailabilityFingerprint: entryAvailabilityFingerprint, availabilityFingerprintAuthoritative: availabilityFingerprintAuthoritative, targetId: targetId);
    }

    internal static string WorldConditionSignature(GameState state)
    {
        NativeWorldEntryMemory entry = state.WorldEntryMemory ?? throw new NotSupportedException("World conditions require their typed native entry memory.");
        entry.Validate();
        NativeSavedWorldState saved = state.SavedWorld ?? throw new NotSupportedException("World conditions require their native saved-world owner.");
        saved.WorldEventFlags116?.Validate();
        WorldState world = state.World ?? throw new InvalidDataException("World conditions require their actual current entity owner.");
        var conditions = new
        {
            EntryBytes = entry.Bytes,
            saved.WorldEventFlags116,
            saved.WorldEventVariable124,
            saved.WorldEventVariable125,
            Slot4 = saved.Slot4.ShapeId,
            Slot5 = saved.Slot5.ShapeId,
            HasGardenPose = saved.GardenPose3 is not null,
            HasRagnarokPose = saved.RagnarokPose2 is not null,
            FootEntity = world.NativeFootRuntime?.EntityType,
            CarEntity = world.NativeCarRuntime?.EntityType,
            HasGardenOwner = world.NativeGardenRuntime is not null,
            HasRagnarokOwner = world.NativeRagnarokRuntime is not null,
            SavedFlag109 = WorldFlag109Domain(state.EventMemory)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(conditions)));
    }

    private static int[]? WorldFlag109Domain(NativeEventMemory memory)
    {
        foreach ((int address, NativeMemoryValue cell) in memory.Cells)
        {
            if (address <= 271 && 271 < address + cell.Width)
            {
                int shift = (271 - address) * 8;
                return cell.Value.Values.Select(value => (int)((uint)value >> shift & byte.MaxValue)).Distinct().Order().ToArray();
            }
        }
        return null;
    }

    private void HandleDialogue(DialogueState dialogue)
    {
        if (session.State.Scene == SceneKind.WorldMap && dialogue.CanConfirm && dialogue.FirstChoice is null && !dialogue.IsNamePrompt && NeedsWorldConfirmRelease(session.State.WorldProcess?.InputHistory))
        {
            Apply(new(PlayerActionKind.Wait));
            return;
        }
        if (dialogue.IsNamePrompt)
        {
            Apply(new(PlayerActionKind.EnterName));
            return;
        }
        if (dialogue.FirstChoice is not int first || dialogue.LastChoice is not int last)
        {
            Apply(new(PlayerActionKind.Confirm));
            return;
        }
        string key = $"{VisitKey()}:{dialogue.ObjectId}:{dialogue.ScriptId}:{dialogue.Operation}:{dialogue.MessageId}:{first}:{last}";
        planner.ObserveChoice(key, last - first + 1, first);
        AutoPlayChoiceCoverage coverage = run.Coverage.Choices.Single(item => item.Key == key);
        RefreshParticipantChoice(coverage);
        if (run.ActiveTravelRoute is not null && TrySelectTravelChoice(key, first, last, out int recorded))
        {
            run.DialogueSelections[key] = recorded;
            run.ActiveSelections?.Add(new(key, recorded));
            ApplyDialogueSelection(recorded);
            return;
        }
        if (coverage.ActiveOption is not null || coverage.Finished)
        {
            int previous = run.DialogueSelections.GetValueOrDefault(key, coverage.ActiveOption ?? first);
            int repeated = dialogue.CancelChoice ?? first + (previous - first + 1) % (last - first + 1);
            run.DialogueSelections[key] = repeated;
            run.ActiveSelections?.Add(new(key, repeated));
            ApplyDialogueSelection(repeated);
            return;
        }
        AutoPlayChoiceStep next = planner.PlanChoice(key);
        if (next.Kind == AutoPlayChoiceStepKind.SaveBeforeChoice)
        {
            RegisterChoiceSnapshot(key);
            next = planner.PlanChoice(key);
        }
        if (next.Selection is not int selection)
        {
            run.ActiveSelections?.Add(new(key, dialogue.CancelChoice ?? last));
            ApplyDialogueSelection(dialogue.CancelChoice ?? last);
            return;
        }
        planner.BeginChoiceOption(key, selection);
        run.SceneChoices.Add(key);
        run.DialogueSelections[key] = selection;
        run.ActiveSelections?.Add(new(key, selection));
        ApplyDialogueSelection(selection);
    }

    internal static int SelectRecordedTravelChoice(AutoPlayEventRoute route, IReadOnlyList<AutoPlayChoiceSelection>? executed, string key, int first, int last)
    {
        if (route.Selections is null || executed is null || executed.Count >= route.Selections.Count || !route.Selections.Take(executed.Count).SequenceEqual(executed))
        {
            throw new NotSupportedException("The recorded travel route has no authenticated next choice selection.");
        }
        AutoPlayChoiceSelection next = route.Selections[executed.Count];
        if (next.Key != key || next.Selection < first || next.Selection > last)
        {
            throw new NotSupportedException("The actual native choice does not match the recorded travel route key and selection range.");
        }
        return next.Selection;
    }

    private bool TrySelectTravelChoice(string key, int first, int last, out int selection)
    {
        AutoPlayEventRoute route = run.ActiveTravelRoute ?? throw new InvalidOperationException("No active travel route.");
        if (route.Selections is null || run.ActiveSelections is null || run.ActiveSelections.Count > route.Selections.Count || !route.Selections.Take(run.ActiveSelections.Count).SequenceEqual(run.ActiveSelections))
        {
            selection = SelectRecordedTravelChoice(route, run.ActiveSelections, key, first, last);
            return true;
        }
        if (run.ActiveSelections.Count < route.Selections.Count && route.Selections[run.ActiveSelections.Count] is { } next && next.Key == key && next.Selection >= first && next.Selection <= last)
        {
            selection = SelectRecordedTravelChoice(route, run.ActiveSelections, key, first, last);
            return true;
        }
        if (run.ActiveVisit is not string visit || run.ActiveEvent is not string eventId || run.ActiveConditionSignature != route.ConditionSignature || run.ActiveTargetId != route.TargetId || run.ActiveBeforeCheckpoint is null)
        {
            throw new NotSupportedException("A changed travel choice lacks its authenticated source attempt.");
        }
        planner.RetireTravelRoute(visit, eventId, route, key, first, last, run.ActiveSelections);
        run.ActiveTravelRoute = null;
        run.RefreshTargetVisit = null;
        selection = default;
        return false;
    }

    internal static bool NeedsWorldConfirmRelease(NativeWorldInputHistory? history)
    {
        return history is null || (history.CurrentButtons & 0x40) != 0;
    }

    private void ApplyDialogueSelection(int selection)
    {
        Apply(getWorld().CurrentDialogue?.CancelChoice == selection ? new(PlayerActionKind.Cancel) : new(PlayerActionKind.Choose, selection));
    }

    private bool FinishChoices()
    {
        if (run.SceneChoices.Count != 0)
        {
            RecordActiveControlReturn();
            string continuation = $"continuation-{run.Steps:D10}.json";
            SaveGame(Path.Combine(runDirectory!, continuation), CaptureState());
            int story = session.State.Variables.GetValueOrDefault(256);
            int frontier = ContinuationFrontier();
            bool conditionsChanged = run.ActiveVisit is string sourceVisit && run.VisitGuards.TryGetValue(sourceVisit, out AutoPlayVisitGuard? sourceGuard) && sourceGuard.Version == AutoPlayVisitGuard.CurrentVersion && getWorld().Events.ReadPersistentPredicateFingerprint(sourceGuard.Reads) is string currentConditions && currentConditions != sourceGuard.Fingerprint;
            if (run.PrimaryContinuation is null || story > run.PrimaryStory || story == run.PrimaryStory && (frontier > run.PrimaryFrontier || frontier == run.PrimaryFrontier && conditionsChanged && !run.PrimaryConditionsChanged))
            {
                run.PrimaryContinuation = continuation;
                run.PrimaryStory = story;
                run.PrimaryFrontier = frontier;
                run.PrimaryConditionsChanged = conditionsChanged;
                run.PrimarySelections = run.ActiveSelections?.ToList();
            }
            foreach (string key in run.SceneChoices)
            {
                planner.CompleteChoiceAtControlReturn(key, continuation);
            }
            run.SceneChoices.Clear();
        }
        if (run.PrimaryContinuation is null)
        {
            return false;
        }
        foreach (AutoPlayChoiceCoverage choice in run.Coverage.Choices.Where(item => !item.Finished && !item.PolicyExhausted))
        {
            AutoPlayChoiceStep next = planner.PlanChoice(choice.Key);
            if (next.Kind == AutoPlayChoiceStepKind.RestoreContinuation)
            {
                planner.FinishChoice(choice.Key);
                continue;
            }
            if (next.Kind == AutoPlayChoiceStepKind.Replay && next.Checkpoint is not null && next.Selection is int selection)
            {
                restore(GameSession.Load(Path.Combine(runDirectory!, next.Checkpoint)));
                run.ActiveSelections = choice.BeforeSelections?.ToList();
                planner.BeginChoiceOption(choice.Key, selection);
                run.SceneChoices.Add(choice.Key);
                run.SettleFrames = 0;
                if (choice.Key.StartsWith("input:", StringComparison.Ordinal))
                {
                    getWorld().EnsureField();
                    ScriptedInputMetadata input = RestoreScriptedChoiceInput(choice);
                    run.ActiveSelections?.Add(new(choice.Key, selection));
                    PulseScriptedInput(input.Masks[selection], input, getWorld().Events.IsUserControlEnabled, choice.InputCheckpointPrepared);
                }
                else
                {
                    run.DialogueSelections[choice.Key] = selection;
                    run.ActiveSelections?.Add(new(choice.Key, selection));
                    ApplyDialogueSelection(selection);
                }
                return true;
            }
        }
        restore(GameSession.Load(Path.Combine(runDirectory!, run.PrimaryContinuation)));
        run.ActiveSelections = run.PrimarySelections?.ToList();
        run.PrimaryContinuation = null;
        run.PrimaryConditionsChanged = false;
        return true;
    }

    private ScriptedInputMetadata RestoreScriptedChoiceInput(AutoPlayChoiceCoverage choice)
    {
        if (run.ScriptedChoiceInputs.TryGetValue(choice.Key, out ScriptedInputMetadata? saved))
        {
            if (saved.Masks.Count != choice.OptionCount || saved.Queries.Count == 0)
            {
                throw new InvalidDataException("The saved scripted choice metadata does not match its alternatives.");
            }
            return saved;
        }
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        int? source = run.ActiveField == field && run.ActiveObjectId >= 0 ? run.ActiveObjectId : null;
        // Older manifests may precede retained query metadata. Continuous contact scripts can be between callbacks at the saved boundary.
        ScriptedInputMetadata? recovered = null;
        void ObserveQuery(ScriptCommand command)
        {
            if (command.Name is not ("KEYSCAN" or "KEYSCAN2" or "KEYON") || source is not null && command.ObjectId != source)
            {
                return;
            }
            ScriptedInputMetadata? input = world.Events.DiscoverScriptedInput(source);
            if (input is not null && $"input:{field}:{input.Fingerprint}" == choice.Key && input.Masks.Count == choice.OptionCount)
            {
                recovered = input;
            }
        }
        world.Events.CommandExecuted += ObserveQuery;
        try
        {
            for (int callback = 0; callback < 256 && recovered is null && session.State.Scene == SceneKind.Field && session.State.FieldId == field; callback++)
            {
                CheckIdleDeadline();
                world.AdvanceAutoPlayInputFrame();
            }
        }
        finally
        {
            world.Events.CommandExecuted -= ObserveQuery;
        }
        if (recovered is not null)
        {
            run.ScriptedChoiceInputs.Add(choice.Key, recovered);
            return recovered;
        }
        throw new InvalidDataException("The saved scripted input query cannot be recovered from the original callbacks.");
    }

    private int ContinuationFrontier()
    {
        string visit = session.State.Scene == SceneKind.WorldMap ? "world:" + session.State.Variables.GetValueOrDefault(256) : VisitKey();
        return planner.ReachableFrontierCount(visit, run.ActiveVisit, run.ActiveEvent, session.State.Scene == SceneKind.Field ? candidate => NeedsVisitRefresh(getWorld(), candidate, includeUnknown: false) : null);
    }

    private void RefreshUnavailable(GameWorld world)
    {
        if (session.State.Scene != SceneKind.Field)
        {
            return;
        }
        Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);
        AutoPlayVisitCoverage[] visits = run.Coverage.Visits.Where(visit => visit.Events.Any(item => item.Available && item.UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == item.Event.ConditionSignature))).ToArray();
        foreach (string key in visits.SelectMany(visit => visit.Events.Where(item => item.Available).SelectMany(item => item.UnavailableAttempts.Where(attempt => attempt.SourceConditionSignature == item.Event.ConditionSignature))).Select(attempt => attempt.GuardKey).Distinct(StringComparer.Ordinal))
        {
            if (!run.UnavailabilityGuards.TryGetValue(key, out NativePlayerAbsenceReceipt? receipt))
            {
                throw new InvalidDataException("The native unavailable attempt lacks its persisted destination eligibility receipt.");
            }
            string program = world.ReadAutoPlayFieldProgramHash(receipt.FieldId);
            fingerprints[key] = ReadUnavailableGuard(receipt, program, world.Events.ReadPersistentPredicateFingerprint);
        }
        foreach (AutoPlayVisitCoverage visit in visits)
        {
            planner.RefreshUnavailable(visit.Visit.Key, fingerprints);
        }
    }

    private bool NeedsVisitRefresh(GameWorld world, AutoPlayVisit visit, bool includeUnknown = true)
    {
        // Refreshes are a finite survey between genuine coverage gains. INIT may legitimately
        // change persistent NPC flags; those changes must not starve already known frontiers.
        if (run.InspectedVisitsSinceProgress.Contains(visit.Key))
        {
            return false;
        }
        if (!int.TryParse(visit.Key.Split(':')[0], out int field))
        {
            return false;
        }
        if (!run.VisitGuards.TryGetValue(visit.Key, out AutoPlayVisitGuard? guard))
        {
            return includeUnknown;
        }
        if (guard.Version is not (1 or 2 or AutoPlayVisitGuard.CurrentVersion) || guard.FieldId != field)
        {
            throw new InvalidDataException("The visited field eligibility receipt has an unsupported version or field identity.");
        }
        if (guard.Version != AutoPlayVisitGuard.CurrentVersion)
        {
            return includeUnknown;
        }
        return world.ReadAutoPlayFieldProgramHash(field) != guard.ProgramHash || world.Events.ReadPersistentPredicateFingerprint(guard.Reads) != guard.Fingerprint;
    }

    private void InspectSurveyVisit(string visit)
    {
        if (run.InspectedVisitsSinceProgress.Add(visit))
        {
            ResetIdleDeadline();
        }
        if (run.RefreshTargetVisit == visit)
        {
            run.RefreshTargetVisit = null;
        }
    }

    internal static string ReadUnavailableGuard(NativePlayerAbsenceReceipt receipt, string actualProgramHash, Func<IReadOnlyList<NativePersistentPredicateRead>, string?> readPersistent)
    {
        if (receipt.Version != 1)
        {
            throw new InvalidDataException("The destination eligibility receipt has an unsupported native proof version.");
        }
        return actualProgramHash != receipt.ProgramHash ? "changed-program:" + actualProgramHash : readPersistent(receipt.Reads) ?? throw new NotSupportedException("Destination eligibility dependencies cannot be read exactly from their persistent native owners.");
    }

    private bool TryBacktrackNativeUnavailable(GameWorld world)
    {
        NativePlayerAbsenceReceipt? receipt = world.Events.InspectNativePlayerAbsence();
        if (receipt is null)
        {
            return false;
        }
        ValidateNativeBacktrack(run);
        string sourceVisit = run.ActiveVisit!;
        string eventId = run.ActiveEvent!;
        string sourceSignature = run.ActiveConditionSignature!;
        string beforeCheckpoint = run.ActiveBeforeCheckpoint!;
        if (receipt.FieldId != session.State.FieldId || world.ReadAutoPlayFieldProgramHash(receipt.FieldId) != receipt.ProgramHash)
        {
            throw new InvalidDataException("Native unavailability does not identify the currently loaded original destination program.");
        }
        string guardKey = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions)));
        run.UnavailabilityGuards[guardKey] = receipt;
        run.ActiveAbsenceGuardKey = guardKey;
        GameState unavailable = CaptureState();
        string destinationVisit = VisitKey();
        SaveBundle($"unavailable-{run.Steps:D10}", unavailable);
        GameState before = GameSession.Load(Path.Combine(runDirectory!, beforeCheckpoint));
        try
        {
            restore(before);
            GameWorld source = getWorld();
            source.EnsureField();
            if (session.State.Scene != SceneKind.Field || session.State.FieldId != run.ActiveField || VisitKey() != sourceVisit || !source.Events.CanAcceptMovementInput || run.Coverage.Visits.Single(visit => visit.Visit.Key == sourceVisit).Events.Single(item => item.Event.Id == eventId).Event.ConditionSignature != sourceSignature)
            {
                throw new NotSupportedException("The saved pre-event state does not retain the exact available source condition of the native unavailable attempt.");
            }
            if (!source.CanCacheAutoPlayUnavailableSource(eventId, run.ActiveObjectId))
            {
                throw new NotSupportedException("The source event contains native inputs not represented by the unavailable-attempt signature; automatic backtracking cannot suppress it.");
            }
            ValidateNativeBacktrackPredicates(sourceSignature, source.ReadAutoPlayConditions(eventId, run.ActiveObjectId), receipt.Fingerprint, source.Events.ReadPersistentPredicateFingerprint(receipt.Reads));
        }
        catch
        {
            restore(unavailable);
            throw;
        }
        planner.MarkUnavailable(sourceVisit, eventId, new(sourceSignature, destinationVisit, guardKey, receipt.Fingerprint, run.ActiveTargetId));
        RefreshUnavailable(getWorld());
        run.ActiveVisit = null;
        run.ActiveEvent = null;
        run.ActiveStory = null;
        run.ActiveObjectId = -1;
        run.ActiveConditionSignature = null;
        run.ActiveSelections = [];
        run.ActiveTravelRoute = null;
        run.ActiveBeforeCheckpoint = null;
        run.ActiveTargetId = null;
        run.ActiveAbsenceGuardKey = null;
        run.ActiveAvailabilityFingerprint = null;
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        run.ObservedControlLoss = false;
        run.SettleFrames = 4;
        run.SceneFrames = 0;
        run.ScriptedInputSteps = 0;
        SaveBundle($"unavailable-return-{run.Steps:D10}", CaptureState());
        Status = "Restored the actual pre-event checkpoint; the native unavailable destination is deferred under its observed conditions.";
        return true;
    }

    internal static void ValidateNativeBacktrack(AutoPlayRunState state)
    {
        if (state.ActiveVisit is null || state.ActiveEvent is null || state.ActiveConditionSignature is null || state.ActiveBeforeCheckpoint is null || state.ActiveTargetId is null)
        {
            throw new NotSupportedException("Native unavailability requires its actual pre-event standalone checkpoint and exact source condition; legacy active attempts cannot be reconstructed.");
        }
        if (state.SceneChoices.Count != 0 || state.PrimaryContinuation is not null || state.Coverage.Choices.Any(choice => choice.ActiveOption is not null) || state.ActiveSelections is not { Count: 0 })
        {
            throw new NotSupportedException("Automatic native-unavailability backtracking currently requires a proved empty choice path; conditional alternatives are not completed or skipped.");
        }
    }

    internal static void ValidateNativeBacktrackPredicates(string sourceSignature, EventPredicateState source, string destinationGuard, string? sourceGuard)
    {
        if (source.DeferredOperations.Count != 0 || source.Fingerprint != sourceSignature)
        {
            throw new NotSupportedException("The pre-event source has unknown or changed native predicate inputs; automatic backtracking cannot suppress it.");
        }
        if (sourceGuard is null || sourceGuard != destinationGuard)
        {
            throw new NotSupportedException("Destination eligibility inputs are unknown or changed across the native event; automatic backtracking cannot suppress it.");
        }
    }

    internal void RecordActiveControlReturn()
    {
        if (run.ActiveVisit is not string source || run.ActiveEvent is not string eventId)
        {
            return;
        }
        string destination = session.State.Scene == SceneKind.WorldMap ? "world:" + session.State.Variables.GetValueOrDefault(256) : VisitKey();
        if (run.ActiveTravelRoute is { } route && route.DestinationVisitKey != destination)
        {
            throw new NotSupportedException("The executed native choices returned to a different destination than the recorded travel route.");
        }
        if (run.ActiveTravelRoute is { Selections: { } expected } && (run.ActiveSelections is null || !expected.SequenceEqual(run.ActiveSelections)))
        {
            throw new NotSupportedException("The native event returned control before every recorded travel choice was encountered.");
        }
        GameWorld world = getWorld();
        if (run.SurveyInvalidationUnknown)
        {
            run.InspectedVisitsSinceProgress.Clear();
            run.RefreshTargetVisit = null;
            run.SurveyInvalidationUnknown = false;
        }
        ObserveSurveySegmentChanges(world);
        string? exitSignature = run.ActiveField == session.State.FieldId && session.State.Scene == SceneKind.Field ? world.ReadAutoPlayConditions(eventId, run.ActiveObjectId).Fingerprint : null;
        if (destination.StartsWith("world:", StringComparison.Ordinal))
        {
            RecordWorldControlReturn(planner, source, eventId, destination, run.ActiveStory, session.State.Variables.GetValueOrDefault(256), run.ActiveSelections, run.ActiveConditionSignature, run.SceneChoices.Count > 0, entrySignatureAuthoritative: true, entryAvailabilityFingerprint: run.ActiveAvailabilityFingerprint, availabilityFingerprintAuthoritative: true, targetId: run.ActiveTargetId);
        }
        else
        {
            planner.Completed(source, eventId, source == destination ? null : destination, exitSignature, ObserveStoryChange(run.ActiveStory, source, session.State.Variables.GetValueOrDefault(256)), run.ActiveSelections, run.ActiveConditionSignature, entrySignatureAuthoritative: true, entryAvailabilityFingerprint: run.ActiveAvailabilityFingerprint, availabilityFingerprintAuthoritative: true, targetId: run.ActiveTargetId);
        }
    }

    private PlayerActionResult Apply(PlayerAction action)
    {
        PlayerActionResult result = run.ObserveContactInput ? ApplyContactInput(action) : getWorld().Apply(action);
        if (!result.Accepted)
        {
            throw new NotSupportedException($"Automatic input {action.Kind} rejected: {result.Detail}");
        }
        session.SetDialogueDraft(null);
        return result;
    }

    internal static bool? ObserveStoryChange(int? entryStory, string entryVisit, int exitStory)
    {
        if (entryStory is null)
        {
            int separator = entryVisit.LastIndexOf(':');
            if (separator < 0 || !int.TryParse(entryVisit.AsSpan(separator + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int recorded))
            {
                return null;
            }
            entryStory = recorded;
        }
        return entryStory.Value != exitStory;
    }

    private string VisitKey() => $"{session.State.FieldId}:{session.State.Variables.GetValueOrDefault(256)}";

    internal static bool IsEnding(GameState state, JsonElement vm)
    {
        // The terminal ending scene is an explicit product completion policy.
        // A halted script elsewhere never establishes successful completion.
        return state.Scene == SceneKind.Field && state.FieldId == 76 && state.MovieSource is { DiscIndex: 3, MovieIndex: 5 } && state.MoviePlayback is { Completed: true, IsPlaying: false } && vm.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1 && fiber.GetProperty("WaitReason").GetString() == "NativeHalt");
    }

    private string SaveBundle(string name, GameState state)
    {
        string snapshot = name + ".json";
        SaveGame(Path.Combine(runDirectory!, snapshot), state);
        run.Snapshot = snapshot;
        string manifest = Path.Combine(runDirectory!, name + ".run.json");
        string content = JsonSerializer.Serialize(run, JsonOptions);
        AtomicWrite(manifest, content);
        AtomicWrite(Path.Combine(runDirectory!, "latest.run.json"), content);
        return manifest;
    }

    private void Fail(Exception exception)
    {
        Running = false;
        Error = exception.ToString();
        Status = $"Failed at step {run.Steps}: {exception.Message}";
        run.Error = Error;
        try
        {
            if (beforeStep is not null && beforeController is not null)
            {
                AutoPlayRunState before = JsonSerializer.Deserialize<AutoPlayRunState>(beforeController, JsonOptions)!;
                before.Snapshot = $"failure-{run.Steps:D10}-before.json";
                SaveGame(Path.Combine(runDirectory!, before.Snapshot), beforeStep);
                ReproductionPath = Path.Combine(runDirectory!, $"failure-{run.Steps:D10}.run.json");
                AtomicWrite(ReproductionPath, JsonSerializer.Serialize(before, JsonOptions));
            }
            SaveBundle($"failure-{run.Steps:D10}-fault", CaptureState());
        }
        catch (Exception saveError)
        {
            Error += Environment.NewLine + "Failure snapshot: " + saveError;
        }
    }

    private static void SaveGame(string path, GameState state)
    {
        string temporary = path + ".tmp";
        new GameSession(state).Save(temporary);
        File.Move(temporary, path, true);
    }

    private static void AtomicWrite(string path, string content)
    {
        File.WriteAllText(path + ".tmp", content);
        File.Move(path + ".tmp", path, true);
    }
}
