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
    public int ActiveObjectId { get; set; } = -1;
    public bool ObservedControlLoss { get; set; }
    public int SettleFrames { get; set; }
    public long SceneFrames { get; set; }
    public int IdleFrames { get; set; }
    public List<string> SceneChoices { get; set; } = [];
    public string? PrimaryContinuation { get; set; }
    public int PrimaryStory { get; set; }
    public int PrimaryFrontier { get; set; }
    public string? Snapshot { get; set; }
    public string? Error { get; set; }
    public bool ReachedEnding { get; set; }
    public Dictionary<string, int> ScriptedInputs { get; set; } = new(StringComparer.Ordinal);
    public int ScriptedInputSteps { get; set; }
    public Dictionary<string, int> DialogueSelections { get; set; } = new(StringComparer.Ordinal);
    public long LastProgressStep { get; set; }
    public int LastProgressScore { get; set; }
}

/// <summary>The graphics host and command line drive this same autonomous, deterministic controller.</summary>
internal sealed class AutoPlayController
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly GameSession session;
    private readonly Func<GameWorld> getWorld;
    private readonly Action<GameState> restore;
    private readonly string outputDirectory;
    private readonly TimeProvider time;
    private long lastSave;
    private AutoPlayRunState run = new();
    private AutoPlayPlanner planner = new();
    private GameState? beforeStep;
    private string? beforeController;
    private string? runDirectory;

    public AutoPlayController(GameSession session, Func<GameWorld> getWorld, Action<GameState> restore, string outputDirectory, TimeProvider? time = null)
    {
        this.session = session;
        this.getWorld = getWorld;
        this.restore = restore;
        this.outputDirectory = Path.GetFullPath(outputDirectory);
        this.time = time ?? TimeProvider.System;
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
        planner = new(run.Coverage);
        runDirectory = Path.Combine(outputDirectory, time.GetUtcNow().ToString("yyyyMMdd-HHmmss-fffffff"));
        Directory.CreateDirectory(runDirectory);
        Running = true;
        Paused = false;
        Error = null;
        ReproductionPath = null;
        lastSave = time.GetTimestamp();
        Status = "Starting New Game; seed 1.";
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

    public void ResumeFrom(string manifest)
    {
        manifest = Path.GetFullPath(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllText(manifest), JsonOptions) ?? throw new InvalidDataException("Empty automatic play manifest.");
        if (saved.Version != 1 || saved.Seed != 1 || saved.Snapshot is null)
        {
            throw new InvalidDataException("Unsupported automatic play manifest or seed.");
        }
        string directory = Path.GetDirectoryName(manifest)!;
        restore(GameSession.Load(Path.Combine(directory, saved.Snapshot)));
        foreach (AutoPlayChoiceCoverage choice in saved.Coverage.Choices)
        {
            if (choice.BeforeCheckpoint is not null)
            {
                choice.BeforeCheckpoint = Path.GetFullPath(choice.BeforeCheckpoint, directory);
            }
            if (choice.ContinuationCheckpoint is not null)
            {
                choice.ContinuationCheckpoint = Path.GetFullPath(choice.ContinuationCheckpoint, directory);
            }
        }
        if (saved.PrimaryContinuation is not null)
        {
            saved.PrimaryContinuation = Path.GetFullPath(saved.PrimaryContinuation, directory);
        }
        run = saved;
        run.Error = null;
        run.LastProgressStep = run.Steps;
        planner = new(run.Coverage);
        runDirectory = Path.Combine(outputDirectory, time.GetUtcNow().ToString("yyyyMMdd-HHmmss-fffffff") + "-resume");
        Directory.CreateDirectory(runDirectory);
        Running = true;
        Paused = false;
        Error = null;
        ReproductionPath = manifest;
        lastSave = time.GetTimestamp();
        Status = $"Resumed at step {run.Steps}.";
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
                beforeStep = session.State;
                beforeController = JsonSerializer.Serialize(run, JsonOptions);
                beforeStep = CaptureState();
                Tick();
                run.Steps++;
                int score = run.Coverage.Visits.Count + run.Coverage.Choices.Count + run.Coverage.Visits.Sum(visit => visit.Events.Count(item => item.Completed)) + run.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
                if (score != run.LastProgressScore)
                {
                    run.LastProgressScore = score;
                    run.LastProgressStep = run.Steps;
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
            catch (Exception exception)
            {
                Fail(exception);
            }
            if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(12))
            {
                break;
            }
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
        world.EnsureField();
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
        Status = $"{session.State.FieldId}:{session.State.FieldName} · frame {world.Events.Frame} · {run.Coverage.Visits.Count} visits · {run.Steps} steps";
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
            HandleDialogue(dialogue);
            run.SettleFrames = 0;
            return;
        }
        if (world.Events.PartyMenu is not null)
        {
            run.SettleFrames = 0;
            Apply(new(PlayerActionKind.Cancel));
            return;
        }
        if (session.State.Scene == SceneKind.WorldMap)
        {
            ExploreWorld(world);
            return;
        }
        if (!world.Events.IsUserControlEnabled && session.State.FieldButtonInput is { PhysicalHeld: 0 } buttons && (buttons.PendingPulse != 0 || buttons.RawHeld != 0))
        {
            world.AdvanceAutoPlayInputFrame();
            return;
        }
        if (!world.Events.IsUserControlEnabled && world.Events.DiscoverScriptedInput() is { Masks.Count: > 0 } input)
        {
            bool queried = false;
            void ObserveQuery(ScriptCommand command)
            {
                queried |= command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON";
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
            if (queried && session.State.Scene == SceneKind.Field && session.State.FieldId == world.Events.FieldId && world.CurrentDialogue is not { CanConfirm: true } && world.Events.Scene.NativeTutorial is null && world.Events.Scene.NativeMenuTutorial is null)
            {
                DriveScriptedInput(world.Events.DiscoverScriptedInput() ?? input);
            }
            return;
        }
        if (!world.Events.IsUserControlEnabled && run.ActiveEvent is not null)
        {
            run.ObservedControlLoss = true;
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
        if (FinishChoices())
        {
            return;
        }
        string visit = VisitKey();
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        planner.Observe(new(visit, world.AutoPlayRegion), targets.GroupBy(target => target.CoverageId).Select(group => group.First()).Select(target => new AutoPlayEvent(target.CoverageId, target.Kind == AutoPlayTargetKind.Gateway || target.DestinationField is not null ? AutoPlayEventKind.Transition : AutoPlayEventKind.Interaction, ChangesStory: target.ChangesStory, Order: target.Order)).ToArray());
        if (run.ActiveVisit is not null && run.ActiveEvent is not null)
        {
            planner.Completed(run.ActiveVisit, run.ActiveEvent, run.ActiveVisit == visit ? null : visit);
            run.ActiveVisit = null;
            run.ActiveEvent = null;
        }
        AutoPlayPlanStep? next = planner.Plan(visit);
        if (next is null)
        {
            if (run.IdleFrames++ < 1200)
            {
                Apply(new(PlayerActionKind.Wait));
                return;
            }
            throw new NotSupportedException("Reachable event frontier exhausted before the authenticated ending. Missing conditional event, graph edge or progression policy; coverage is saved.");
        }
        run.IdleFrames = 0;
        AutoPlayTarget[] variants = targets.Where(item => item.CoverageId == next.Event.Id).OrderBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        AutoPlayTarget target = variants.FirstOrDefault() ?? throw new InvalidDataException("Planned event is no longer available in the current field.");
        SaveBundle($"event-{run.Steps:D10}", CaptureState());
        run.ActiveVisit = visit;
        run.ActiveEvent = target.CoverageId;
        run.ActiveField = session.State.FieldId;
        run.ActiveObjectId = target.ObjectId;
        run.ObservedControlLoss = false;
        run.SettleFrames = 0;
        if (target.Kind == AutoPlayTargetKind.Talk)
        {
            bool accepted = false;
            foreach (AutoPlayTarget variant in variants)
            {
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
            }
        }
        else
        {
            bool triggered = false;
            foreach (AutoPlayTarget variant in variants)
            {
                if (variant.ApproachX is float approachX && variant.ApproachY is float approachY)
                {
                    world.Events.TeleportPlayer(approachX, approachY, variant.ApproachZ ?? variant.Z, variant.ApproachTriangle ?? variant.Triangle, variant.Direction);
                }
                bool contacted = world.Events.TeleportPlayer(variant.X, variant.Y, variant.Z, variant.Triangle, variant.Direction, true);
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
                run.ActiveObjectId = -1;
            }
            Apply(new(PlayerActionKind.Wait));
        }
    }

    private void DriveScriptedInput(ScriptedInputMetadata input)
    {
        if (++run.ScriptedInputSteps > 10000)
        {
            throw new NotSupportedException("Scripted input exploration exceeded 10000 attempts without a control-return boundary.");
        }
        int attempt = run.ScriptedInputs.GetValueOrDefault(input.Fingerprint);
        run.ScriptedInputs[input.Fingerprint] = attempt + 1;
        string key = $"input:{session.State.FieldId}:{input.Fingerprint}";
        planner.ObserveChoice(key, input.Masks.Count);
        AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(item => item.Key == key);
        int selection = attempt % input.Masks.Count;
        if (choice.ActiveOption is null && !choice.Finished)
        {
            AutoPlayChoiceStep next = planner.PlanChoice(key);
            if (next.Kind == AutoPlayChoiceStepKind.SaveBeforeChoice)
            {
                RegisterChoiceSnapshot(key);
                next = planner.PlanChoice(key);
            }
            if (next.Selection is int planned)
            {
                selection = planned;
                planner.BeginChoiceOption(key, selection);
                run.SceneChoices.Add(key);
            }
        }
        PulseScriptedInput(input.Masks[selection]);
    }

    private void PulseScriptedInput(int mask)
    {
        Apply(new(PlayerActionKind.FieldButtons, 0));
        GameWorld world = getWorld();
        int field = session.State.FieldId;
        bool HasOwner() => session.State.Scene == SceneKind.Field && session.State.FieldId == field && !world.Events.IsUserControlEnabled && world.Events.Scene.NativeTutorial is null && world.Events.Scene.NativeMenuTutorial is null && world.Events.PartyMenu is null;
        for (int release = 0; release < 2 && HasOwner(); release++)
        {
            world.AdvanceAutoPlayInputFrame();
        }
        if (!HasOwner())
        {
            return;
        }
        bool sampled = false;
        void Observe(ScriptCommand command)
        {
            if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" && command.Arguments.Count == 1)
            {
                int value = command.Name == "KEYSCAN2" ? mask : session.State.InputConfiguration!.Map((ushort)mask);
                sampled |= (command.Arguments[0] & value) != 0;
            }
        }
        world.Events.CommandExecuted += Observe;
        Apply(new(PlayerActionKind.FieldButtons, mask));
        try
        {
            for (int callback = 0; callback < 120 && HasOwner() && !sampled; callback++)
            {
                world.AdvanceAutoPlayInputFrame();
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
        run.SettleFrames = 0;
        run.SceneFrames = 0;
    }

    private void RegisterChoiceSnapshot(string key)
    {
        string name = "choice-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant() + ".json";
        SaveGame(Path.Combine(runDirectory!, name), CaptureState());
        planner.RegisterChoiceCheckpoint(key, name);
    }

    private void ExploreWorld(GameWorld world)
    {
        if (session.State.World is { NativeScriptedRuntime: not null } or { NativeTrainRuntime: not null })
        {
            run.SceneFrames += Apply(new(PlayerActionKind.Wait, Frames: 300)).FramesAdvanced;
            if (run.SceneFrames > 180000)
            {
                throw new TimeoutException("The scripted world owner did not return within 180000 callbacks.");
            }
            return;
        }
        if (FinishChoices())
        {
            return;
        }
        string visit = "world:" + session.State.Variables.GetValueOrDefault(256);
        IReadOnlyList<WorldAutoPlayTarget> targets = world.DiscoverAutoPlayWorldTargets();
        planner.Observe(new(visit, "world"), targets.Select(target => new AutoPlayEvent(target.Id, AutoPlayEventKind.Transition, Order: target.Order)).ToArray());
        if (run.ActiveVisit is not null && run.ActiveEvent is not null)
        {
            planner.Completed(run.ActiveVisit, run.ActiveEvent, visit);
        }
        run.ActiveVisit = null;
        run.ActiveEvent = null;
        AutoPlayPlanStep next = planner.Plan(visit) ?? throw new NotSupportedException("Reachable world frontier exhausted before the ending.");
        WorldAutoPlayTarget target = targets.Single(item => item.Id == next.Event.Id);
        SaveBundle($"world-{run.Steps:D10}", CaptureState());
        run.ActiveVisit = visit;
        run.ActiveEvent = target.Id;
        run.ActiveField = session.State.FieldId;
        run.ActiveObjectId = -1;
        run.ObservedControlLoss = false;
        run.SettleFrames = 0;
        world.ExecuteAutoPlayWorldTarget(target);
    }

    private void HandleDialogue(DialogueState dialogue)
    {
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
        if (coverage.ActiveOption is not null || coverage.Finished)
        {
            int previous = run.DialogueSelections.GetValueOrDefault(key, coverage.ActiveOption ?? first);
            int repeated = dialogue.CancelChoice ?? first + (previous - first + 1) % (last - first + 1);
            run.DialogueSelections[key] = repeated;
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
            ApplyDialogueSelection(dialogue.CancelChoice ?? last);
            return;
        }
        planner.BeginChoiceOption(key, selection);
        run.SceneChoices.Add(key);
        run.DialogueSelections[key] = selection;
        ApplyDialogueSelection(selection);
    }

    private void ApplyDialogueSelection(int selection)
    {
        Apply(getWorld().CurrentDialogue?.CancelChoice == selection ? new(PlayerActionKind.Cancel) : new(PlayerActionKind.Choose, selection));
    }

    private bool FinishChoices()
    {
        if (run.SceneChoices.Count != 0)
        {
            string continuation = $"continuation-{run.Steps:D10}.json";
            SaveGame(Path.Combine(runDirectory!, continuation), CaptureState());
            int story = session.State.Variables.GetValueOrDefault(256);
            int frontier = ContinuationFrontier();
            if (run.PrimaryContinuation is null || story > run.PrimaryStory || story == run.PrimaryStory && frontier > run.PrimaryFrontier)
            {
                run.PrimaryContinuation = continuation;
                run.PrimaryStory = story;
                run.PrimaryFrontier = frontier;
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
        foreach (AutoPlayChoiceCoverage choice in run.Coverage.Choices.Where(item => !item.Finished))
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
                planner.BeginChoiceOption(choice.Key, selection);
                run.SceneChoices.Add(choice.Key);
                run.SettleFrames = 0;
                if (choice.Key.StartsWith("input:", StringComparison.Ordinal))
                {
                    getWorld().EnsureField();
                    ScriptedInputMetadata input = getWorld().Events.DiscoverScriptedInput() ?? throw new InvalidDataException("The saved scripted input owner is missing.");
                    PulseScriptedInput(input.Masks[selection]);
                }
                else
                {
                    run.DialogueSelections[choice.Key] = selection;
                    ApplyDialogueSelection(selection);
                }
                return true;
            }
        }
        restore(GameSession.Load(Path.Combine(runDirectory!, run.PrimaryContinuation)));
        run.PrimaryContinuation = null;
        return true;
    }

    private int ContinuationFrontier()
    {
        GameWorld world = getWorld();
        if (session.State.Scene == SceneKind.WorldMap)
        {
            return world.DiscoverAutoPlayWorldTargets().Count;
        }
        AutoPlayVisitCoverage? visit = run.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == VisitKey());
        HashSet<string> completed = visit?.Events.Where(item => item.Completed).Select(item => item.Event.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        return world.DiscoverAutoPlayTargets().Select(item => item.CoverageId).Distinct(StringComparer.Ordinal).Count(id => !completed.Contains(id));
    }

    private PlayerActionResult Apply(PlayerAction action)
    {
        PlayerActionResult result = getWorld().Apply(action);
        if (!result.Accepted)
        {
            throw new NotSupportedException($"Automatic input {action.Kind} rejected: {result.Detail}");
        }
        session.SetDialogueDraft(null);
        return result;
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
        AtomicWrite(manifest, JsonSerializer.Serialize(run, JsonOptions));
        AtomicWrite(Path.Combine(runDirectory!, "latest.run.json"), JsonSerializer.Serialize(run, JsonOptions));
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
