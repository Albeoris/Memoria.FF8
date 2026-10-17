using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum AutoPlayInputTrialOutcome
{
    Pending,
    ReturnedToObservedContext,
    ReachedNewBoundary,
    NativeLoss,
    ControlReturn
}

internal sealed class AutoPlayInputSearchTrial
{
    public int Selection { get; set; }
    public int? Mask { get; set; }
    public bool Sampled { get; set; }
    public ScriptedInputQuery? SampledQuery { get; set; }
    public AutoPlayInputTrialOutcome Outcome { get; set; }
    public string? ChildId { get; set; }
    public NativeGameOverReceipt? Loss { get; set; }
    public string? Diagnostic { get; set; }
}

internal sealed class AutoPlayInputSearchFrame
{
    public string Id { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int? ParentTrial { get; set; }
    public string Checkpoint { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public int FieldId { get; set; }
    public string ProgramHash { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public ScriptedInputMetadata? Input { get; set; }
    public DialogueState? Dialogue { get; set; }
    public bool Prepared { get; set; }
    public bool AllowControlled { get; set; }
    public List<int> Options { get; set; } = [];
    public int? PreferredSelection { get; set; }
    public List<AutoPlayChoiceSelection>? BeforeSelections { get; set; }
    public AutoPlayInputSearchContext Context { get; set; } = new();
    public List<AutoPlayInputSearchTrial> Trials { get; set; } = [];
    public int? ActiveTrial { get; set; }
    public bool Exhausted { get; set; }
}

internal sealed class AutoPlayInputSearchContext
{
    public string? Visit { get; set; }
    public string? Navigation { get; set; }
    public string? Event { get; set; }
    public int Field { get; set; }
    public int? Story { get; set; }
    public int ObjectId { get; set; }
    public string? Conditions { get; set; }
    public string? BeforeCheckpoint { get; set; }
    public string? Target { get; set; }
    public string? AbsenceGuard { get; set; }
    public string? Availability { get; set; }
    public bool ObservedControlLoss { get; set; }
    public List<string> SceneChoices { get; set; } = [];
}

internal sealed class AutoPlayInputSearch
{
    public int Version { get; set; } = 1;
    public const int MaximumTrials = 10000;
    public List<AutoPlayInputSearchFrame> Frames { get; set; } = [];
    public string? ActiveFrameId { get; set; }
    public int TrialCount { get; set; }
    public HashSet<string> SampledPhysicalEdges { get; set; } = new(StringComparer.Ordinal);
    public int DistinctSampledPhysicalEdges { get; set; }
    public bool Closed { get; set; }
}

internal sealed partial class AutoPlayController
{
    private void DriveInputSearchBoundary(ScriptedInputMetadata? input, bool allowControlled, DialogueState? dialogue = null, string? dialogueKey = null, GameState? preparedCheckpoint = null, int? preferredSelection = null)
    {
        if (input is not null && (!input.ObservedQueriesProven || !HasNeutralScriptedInput(session.State.FieldButtonInput, session.State.FieldId, getWorld().Events.Frame)))
        {
            throw new NotSupportedException("Local input search requires an authentic neutral prepared query checkpoint.");
        }
        if (run.InputSearch is null || run.InputSearch.Closed)
        {
            if (run.InputSearch is not null)
            {
                run.InputSearchHistory.Add(run.InputSearch);
            }
            run.InputSearch = new();
        }
        AutoPlayInputSearch search = run.InputSearch;
        if (search.Frames.Count >= AutoPlayInputSearch.MaximumTrials)
        {
            throw new NotSupportedException("Local input search exhausted its bounded authentic checkpoint budget.");
        }
        AutoPlayInputSearchFrame? parent = search.Frames.SingleOrDefault(frame => frame.Id == search.ActiveFrameId);
        string key = input is null ? dialogueKey! : $"input:{session.State.FieldId}:{input.Fingerprint}";
        string checkpoint = $"input-search-{run.Steps:D10}-{search.Frames.Count:D5}.json";
        GameState native = preparedCheckpoint ?? CaptureState();
        if (native.FieldId != session.State.FieldId || native.EventState?.GetProperty("Frame").GetInt64() != getWorld().Events.Frame || preparedCheckpoint is not null && !HasNeutralScriptedInput(native.FieldButtonInput, native.FieldId, getWorld().Events.Frame))
        {
            throw new InvalidDataException("The prepared input checkpoint no longer matches its exact native callback boundary.");
        }
        string program = native.EventState?.GetProperty("ProgramHash").GetString() ?? throw new InvalidDataException("An input search checkpoint requires the actual native program hash.");
        SaveGame(Path.Combine(runDirectory!, checkpoint), native);
        AutoPlayInputSearchFrame frame = new()
        {
            Id = Path.GetFileNameWithoutExtension(checkpoint),
            ParentId = parent?.Id,
            ParentTrial = parent?.ActiveTrial,
            Checkpoint = checkpoint,
            Sha256 = CheckpointHash(Path.Combine(runDirectory!, checkpoint)),
            FieldId = native.FieldId,
            ProgramHash = program,
            Key = key,
            Input = input,
            Dialogue = dialogue,
            Prepared = input is not null,
            AllowControlled = allowControlled,
            Options = input is null ? Enumerable.Range(dialogue!.FirstChoice!.Value, dialogue.LastChoice!.Value - dialogue.FirstChoice.Value + 1).ToList() : Enumerable.Range(0, input.Masks.Count).ToList(),
            PreferredSelection = preferredSelection,
            BeforeSelections = run.ActiveSelections?.ToList(),
            Context = CaptureInputSearchContext()
        };
        if (preferredSelection is int preferred && (parent is not null || !frame.Options.Contains(preferred)))
        {
            throw new InvalidDataException("A planned local replay requires a root occurrence and an actual visible alternative.");
        }
        search.Frames.Add(frame);
        run.InputSearchChoiceKeys.Add(key);
        if (input is not null)
        {
            run.ScriptedChoiceInputs.TryAdd(key, input);
        }
        planner.ObserveChoice(key, frame.Options.Count, frame.Options[0]);
        AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(item => item.Key == key);
        if (choice.BeforeCheckpoint is null)
        {
            planner.RegisterChoiceCheckpoint(key, checkpoint);
            choice.InputCheckpointPrepared = frame.Prepared;
            choice.BeforeSelections = frame.BeforeSelections?.ToList();
        }
        if (parent is not null && parent.ActiveTrial is int active)
        {
            AutoPlayInputSearchTrial trial = parent.Trials[active];
            trial.ChildId = frame.Id;
            trial.Outcome = parent.Key == frame.Key ? AutoPlayInputTrialOutcome.ReturnedToObservedContext : AutoPlayInputTrialOutcome.ReachedNewBoundary;
            if (parent.Key == frame.Key && NextInputSearchSelection(parent) is not null)
            {
                RestoreInputSearchFrame(parent);
                ExecuteInputSearchTrial(parent);
                return;
            }
        }
        search.ActiveFrameId = frame.Id;
        ExecuteInputSearchTrial(frame);
    }

    private void BeginPlannedInputSearchReplay(AutoPlayChoiceCoverage choice, int selection, GameState checkpoint, ScriptedInputMetadata? input = null, bool allowControlled = false)
    {
        if (run.InputSearch is { Closed: false })
        {
            throw new InvalidOperationException("Global choice replay requires the preceding local episode's actual control return.");
        }
        GameWorld world = getWorld();
        world.EnsureField();
        if (input is not null)
        {
            if (!choice.InputCheckpointPrepared)
            {
                throw new NotSupportedException("An observed global input replay requires its authenticated prepared first-occurrence checkpoint.");
            }
            ScriptedInputMetadata? current = world.Events.DiscoverObservedScriptedInput(input.Queries, allowControlled && run.ActiveObjectId >= 0 ? run.ActiveObjectId : null);
            if (current is null || current.Fingerprint != input.Fingerprint || !current.Masks.SequenceEqual(input.Masks) || !current.Queries.SequenceEqual(input.Queries))
            {
                throw new NotSupportedException("A planned global input alternative no longer matches its original observed physical domain.");
            }
            DriveInputSearchBoundary(current, allowControlled, preparedCheckpoint: checkpoint, preferredSelection: selection);
            return;
        }
        DialogueState dialogue = world.CurrentDialogue ?? throw new NotSupportedException("A planned dialogue replay requires its actual saved native choice.");
        string key = $"{VisitKey()}:{dialogue.ObjectId}:{dialogue.ScriptId}:{dialogue.Operation}:{dialogue.MessageId}:{dialogue.FirstChoice}:{dialogue.LastChoice}";
        if (!dialogue.CanConfirm || dialogue.FirstChoice is null || dialogue.LastChoice is null || key != choice.Key)
        {
            throw new NotSupportedException("A planned dialogue replay no longer matches its original native owner and visible range.");
        }
        DriveInputSearchBoundary(null, false, dialogue, key, preferredSelection: selection);
    }

    internal static int? NextInputSearchSelection(AutoPlayInputSearchFrame frame)
    {
        int start = 0;
        if (frame.PreferredSelection is int preferred)
        {
            if (!frame.Options.Contains(preferred))
            {
                throw new InvalidDataException("The planned local input alternative is outside its original domain.");
            }
            start = frame.Options.IndexOf(preferred);
        }
        else if (frame.Dialogue is not null && frame.BeforeSelections?.LastOrDefault(selection => selection.Key == frame.Key) is { } previous && frame.Options.Contains(previous.Selection))
        {
            start = (frame.Options.IndexOf(previous.Selection) + 1) % frame.Options.Count;
        }
        for (int offset = 0; offset < frame.Options.Count; offset++)
        {
            int selection = frame.Options[(start + offset) % frame.Options.Count];
            if (!frame.Trials.Any(trial => trial.Selection == selection))
            {
                return selection;
            }
        }
        return null;
    }

    private void ExecuteInputSearchTrial(AutoPlayInputSearchFrame frame)
    {
        AutoPlayInputSearch search = run.InputSearch!;
        if (search.TrialCount >= AutoPlayInputSearch.MaximumTrials)
        {
            throw new NotSupportedException("Local input search exhausted 10000 actual trials without a native control return.");
        }
        int selection = NextInputSearchSelection(frame) ?? throw new NotSupportedException("The local input checkpoint has no untried physical alternative.");
        AutoPlayInputSearchTrial trial = new() { Selection = selection, Mask = frame.Input?.Masks[selection] };
        frame.ActiveTrial = frame.Trials.Count;
        frame.Trials.Add(trial);
        search.ActiveFrameId = frame.Id;
        search.TrialCount++;
        run.ActiveSelections?.Add(new(frame.Key, selection));
        if (frame.Input is { } input)
        {
            run.ScriptedInputs[input.Fingerprint] = run.ScriptedInputs.GetValueOrDefault(input.Fingerprint) + 1;
            trial.Sampled = PulseScriptedInput(input.Masks[selection], input, frame.AllowControlled, prepared: true, query => trial.SampledQuery = query);
            if (RecordInputSearchKnowledge(search, frame, trial))
            {
                run.LastProgressStep = run.Steps;
                ResetIdleDeadline();
            }
        }
        else
        {
            run.DialogueSelections[frame.Key] = selection;
            ApplyDialogueSelection(selection);
            trial.Sampled = true;
        }
    }

    internal static bool RecordInputSearchKnowledge(AutoPlayInputSearch search, AutoPlayInputSearchFrame frame, AutoPlayInputSearchTrial trial)
    {
        if (!trial.Sampled || trial.Mask is not int mask || frame.Input is null || trial.SampledQuery is null || !frame.Input.Queries.Contains(trial.SampledQuery))
        {
            return false;
        }
        string edge = JsonSerializer.Serialize(new { frame.Key, Mask = mask });
        if (!search.SampledPhysicalEdges.Add(edge))
        {
            return false;
        }
        search.DistinctSampledPhysicalEdges++;
        return true;
    }

    internal static void RestoreInputSearchKnowledge(AutoPlayInputSearch search, bool ledgerPresent)
    {
        if (ledgerPresent)
        {
            return;
        }
        search.SampledPhysicalEdges.Clear();
        search.DistinctSampledPhysicalEdges = 0;
        foreach (AutoPlayInputSearchFrame frame in search.Frames)
        {
            foreach (AutoPlayInputSearchTrial trial in frame.Trials)
            {
                RecordInputSearchKnowledge(search, frame, trial);
            }
        }
    }

    private bool RecoverNativeInputSearchLoss()
    {
        NativeGameOverReceipt? receipt = session.State.NativeGameOver;
        if (receipt is null)
        {
            return false;
        }
        receipt.Validate();
        if (receipt.ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Local input loss recovery requires an authenticated original native loss receipt.");
        }
        AutoPlayInputSearch? search = run.InputSearch;
        AutoPlayInputSearchFrame? frame = search?.Frames.SingleOrDefault(candidate => candidate.Id == search.ActiveFrameId);
        string diagnostic = $"native-loss-{run.Steps:D10}";
        if (frame?.ActiveTrial is not int trialIndex || search is null || search.Closed)
        {
            SaveBundle(diagnostic, CaptureState());
            throw new NotSupportedException("A confirmed native loss has no authenticated local input checkpoint with an untried alternative.");
        }
        AutoPlayInputSearchTrial failed = frame.Trials[trialIndex];
        failed.Outcome = AutoPlayInputTrialOutcome.NativeLoss;
        failed.Loss = receipt;
        failed.Diagnostic = diagnostic + ".run.json";
        SaveBundle(diagnostic, CaptureState());
        if (beforeStep is not null && beforeController is not null)
        {
            AutoPlayRunState before = JsonSerializer.Deserialize<AutoPlayRunState>(beforeController, JsonOptions)!;
            before.Snapshot = diagnostic + "-before.json";
            SaveGame(Path.Combine(runDirectory!, before.Snapshot), beforeStep);
            AtomicWrite(Path.Combine(runDirectory!, diagnostic + "-before.run.json"), JsonSerializer.Serialize(before, BeforeSnapshotJsonOptions));
        }
        while (frame is not null)
        {
            if (NextInputSearchSelection(frame) is not null)
            {
                RestoreInputSearchFrame(frame);
                ExecuteInputSearchTrial(frame);
                return true;
            }
            AutoPlayInputSearchFrame? retained = frame.Trials.Where(trial => trial.ChildId is not null).Select(trial => search.Frames.Single(child => child.Id == trial.ChildId)).FirstOrDefault(child => !child.Exhausted && child.Trials.Count == 0);
            if (retained is not null)
            {
                RestoreInputSearchFrame(retained);
                ExecuteInputSearchTrial(retained);
                return true;
            }
            frame.Exhausted = true;
            frame = search.Frames.SingleOrDefault(parent => parent.Id == frame.ParentId);
        }
        throw new NotSupportedException("The bounded local input search exhausted its retained authentic alternatives after a native loss.");
    }

    private AutoPlayInputSearchContext CaptureInputSearchContext()
    {
        return new()
        {
            Visit = run.ActiveVisit,
            Navigation = run.ActiveNavigationKey,
            Event = run.ActiveEvent,
            Field = run.ActiveField,
            Story = run.ActiveStory,
            ObjectId = run.ActiveObjectId,
            Conditions = run.ActiveConditionSignature,
            BeforeCheckpoint = run.ActiveBeforeCheckpoint,
            Target = run.ActiveTargetId,
            AbsenceGuard = run.ActiveAbsenceGuardKey,
            Availability = run.ActiveAvailabilityFingerprint,
            ObservedControlLoss = run.ObservedControlLoss,
            SceneChoices = run.SceneChoices.ToList()
        };
    }

    private void RestoreInputSearchFrame(AutoPlayInputSearchFrame frame)
    {
        GameState checkpoint = ValidateInputSearchCheckpoint(frame, runDirectory!);
        restore(checkpoint);
        GameWorld world = getWorld();
        world.EnsureField();
        if (frame.Input is { } observed)
        {
            ScriptedInputMetadata? current = world.Events.DiscoverObservedScriptedInput(observed.Queries, frame.AllowControlled && frame.Context.ObjectId >= 0 ? frame.Context.ObjectId : null);
            if (current is null || current.Fingerprint != observed.Fingerprint || !current.Masks.SequenceEqual(observed.Masks) || !current.Queries.SequenceEqual(observed.Queries))
            {
                throw new InvalidDataException("The restored original input query domain differs from its authenticated prepared frame.");
            }
        }
        else if (!Equals(world.CurrentDialogue, frame.Dialogue))
        {
            throw new InvalidDataException("The restored native dialogue differs from its saved original choice owner.");
        }
        AutoPlayInputSearchContext context = frame.Context;
        run.ActiveVisit = context.Visit;
        run.ActiveNavigationKey = context.Navigation;
        run.ActiveEvent = context.Event;
        run.ActiveField = context.Field;
        run.ActiveStory = context.Story;
        run.ActiveObjectId = context.ObjectId;
        run.ActiveConditionSignature = context.Conditions;
        run.ActiveBeforeCheckpoint = context.BeforeCheckpoint;
        run.ActiveTargetId = context.Target;
        run.ActiveAbsenceGuardKey = context.AbsenceGuard;
        run.ActiveAvailabilityFingerprint = context.Availability;
        run.ObservedControlLoss = context.ObservedControlLoss;
        run.SceneChoices = context.SceneChoices.ToList();
        run.ActiveSelections = frame.BeforeSelections?.ToList();
        run.ActiveObservedInput = null;
        run.ObserveContactInput = false;
        run.ActiveTravelRoute = null;
        run.SettleFrames = 0;
        run.SceneFrames = 0;
        run.InputSearch!.ActiveFrameId = frame.Id;
        for (AutoPlayInputSearchFrame? child = frame; child.ParentId is string parentId;)
        {
            AutoPlayInputSearchFrame parent = run.InputSearch.Frames.Single(candidate => candidate.Id == parentId);
            parent.ActiveTrial = child.ParentTrial;
            child = parent;
        }
    }

    private void CompleteInputSearchAtControlReturn()
    {
        if (run.InputSearch is not { Closed: false } search)
        {
            return;
        }
        AutoPlayInputSearchFrame? frame = search.Frames.SingleOrDefault(candidate => candidate.Id == search.ActiveFrameId);
        List<(AutoPlayInputSearchFrame Frame, AutoPlayInputSearchTrial Trial)> path = [];
        while (frame is not null && frame.ActiveTrial is int active)
        {
            AutoPlayInputSearchTrial trial = frame.Trials[active];
            if (!trial.Sampled || trial.Outcome == AutoPlayInputTrialOutcome.NativeLoss)
            {
                throw new InvalidDataException("A failed or unsampled local input trial cannot receive control-return coverage.");
            }
            trial.Outcome = AutoPlayInputTrialOutcome.ControlReturn;
            path.Add((frame, trial));
            frame = search.Frames.SingleOrDefault(parent => parent.Id == frame.ParentId);
        }
        HashSet<string> credited = new(StringComparer.Ordinal);
        for (int index = path.Count - 1; index >= 0; index--)
        {
            (AutoPlayInputSearchFrame observed, AutoPlayInputSearchTrial trial) = path[index];
            if (credited.Add(observed.Key))
            {
                AutoPlayChoiceCoverage choice = run.Coverage.Choices.Single(candidate => candidate.Key == observed.Key);
                choice.ActiveOption ??= trial.Selection;
                if (!run.SceneChoices.Contains(observed.Key))
                {
                    run.SceneChoices.Add(observed.Key);
                }
            }
        }
        search.Closed = true;
    }

    private static string CheckpointHash(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    internal static GameState ValidateInputSearchCheckpoint(AutoPlayInputSearchFrame frame, string directory)
    {
        if (frame.Checkpoint != Path.GetFileName(frame.Checkpoint) || string.IsNullOrEmpty(frame.Checkpoint) || frame.Sha256 != CheckpointHash(Path.Combine(directory, frame.Checkpoint)))
        {
            throw new InvalidDataException("A local input search checkpoint has a missing or changed byte identity.");
        }
        GameState state = GameSession.Load(Path.Combine(directory, frame.Checkpoint));
        ValidateInputSearchCheckpointMetadata(frame, state.FieldId, state.EventState?.GetProperty("ProgramHash").GetString(), state.EventState?.GetProperty("Frame").GetInt64() ?? -1, state.FieldButtonInput);
        return state;
    }

    private static void ValidateInputSearchCheckpointMetadata(AutoPlayInputSearchFrame frame, int field, string? program, long callback, NativeFieldButtonInput? buttons)
    {
        if (field != frame.FieldId || program != frame.ProgramHash || frame.Input is { } input && (!frame.Prepared || !input.ObservedQueriesProven || input.Queries.Count == 0 || input.Masks.Count != frame.Options.Count || !HasNeutralScriptedInput(buttons, frame.FieldId, callback)) || frame.Input is null && (frame.Prepared || frame.Dialogue?.FirstChoice is null))
        {
            throw new InvalidDataException("A local input search checkpoint lost its original field, program, query domain or prepared input owner.");
        }
    }

    private sealed record InputSearchSnapshotHeader([property: JsonRequired] int FieldId, [property: JsonRequired] NativeFieldButtonInput? FieldButtonInput, [property: JsonRequired] InputSearchVmHeader EventState);
    private sealed record InputSearchVmHeader([property: JsonRequired] string ProgramHash, [property: JsonRequired] long Frame);

    private static void ValidateImportedInputSearchCheckpoint(AutoPlayInputSearchFrame frame, string directory)
    {
        if (frame.Checkpoint != Path.GetFileName(frame.Checkpoint) || string.IsNullOrEmpty(frame.Checkpoint))
        {
            throw new InvalidDataException("A local input search checkpoint requires its original basename.");
        }
        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, frame.Checkpoint));
        if (frame.Sha256 != Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())
        {
            throw new InvalidDataException("A local input search checkpoint has a missing or changed byte identity.");
        }
        // Import validates immutable bytes and replay metadata; full owner validation
        // still runs through GameSession.Load whenever a retained state is restored.
        InputSearchSnapshotHeader header = JsonSerializer.Deserialize<InputSearchSnapshotHeader>(bytes, JsonOptions) ?? throw new InvalidDataException("Empty local input search checkpoint.");
        if (header.EventState is null)
        {
            throw new InvalidDataException("A local input search checkpoint requires its native VM header.");
        }
        ValidateInputSearchCheckpointMetadata(frame, header.FieldId, header.EventState.ProgramHash, header.EventState.Frame, header.FieldButtonInput);
    }

    internal static void ValidateInputSearchLedger(AutoPlayInputSearch search)
    {
        if (search.Version != 1 || search.DistinctSampledPhysicalEdges != search.SampledPhysicalEdges.Count || search.DistinctSampledPhysicalEdges > search.TrialCount || search.TrialCount != search.Frames.Sum(frame => frame.Trials.Count) || search.TrialCount > AutoPlayInputSearch.MaximumTrials || search.Frames.Count > AutoPlayInputSearch.MaximumTrials || search.Frames.Select(frame => frame.Id).Distinct(StringComparer.Ordinal).Count() != search.Frames.Count || search.ActiveFrameId is not null && !search.Frames.Any(frame => frame.Id == search.ActiveFrameId))
        {
            throw new InvalidDataException("Unsupported or inconsistent local input search ledger.");
        }
        Dictionary<string, AutoPlayInputSearchFrame> frames = search.Frames.ToDictionary(frame => frame.Id, StringComparer.Ordinal);
        foreach (AutoPlayInputSearchFrame frame in search.Frames)
        {
            int first = frame.Input is null ? frame.Dialogue?.FirstChoice ?? -1 : 0;
            int count = frame.Input is null ? (frame.Dialogue?.LastChoice ?? -2) - first + 1 : frame.Input.Masks.Count;
            if (first < 0 || count <= 0 || frame.PreferredSelection is int preferred && (frame.ParentId is not null || !frame.Options.Contains(preferred)) || !frame.Options.SequenceEqual(Enumerable.Range(first, count)) || frame.Input is { } input && (input.Masks.Distinct().Count() != input.Masks.Count || input.Masks.Any(mask => mask is <= 0 or > ushort.MaxValue)) || frame.Trials.Select(trial => trial.Selection).Distinct().Count() != frame.Trials.Count || frame.ActiveTrial is int active && (active < 0 || active >= frame.Trials.Count))
            {
                throw new InvalidDataException("A local input search ledger has a changed ordered option domain or active trial.");
            }
            if (frame.ParentId is string parentId)
            {
                if (!frames.TryGetValue(parentId, out AutoPlayInputSearchFrame? parent) || frame.ParentTrial is not int parentTrial || parentTrial < 0 || parentTrial >= parent.Trials.Count || parent.Trials[parentTrial].ChildId != frame.Id)
                {
                    throw new InvalidDataException("A local input search ledger has an invalid parent edge.");
                }
            }
            else if (frame.ParentTrial is not null)
            {
                throw new InvalidDataException("A root local input frame cannot have a parent trial.");
            }
            HashSet<string> ancestors = [frame.Id];
            AutoPlayInputSearchFrame ancestor = frame;
            while (ancestor.ParentId is string ancestorId)
            {
                if (!ancestors.Add(ancestorId) || !frames.TryGetValue(ancestorId, out AutoPlayInputSearchFrame? next))
                {
                    throw new InvalidDataException("A local input search ledger has a cyclic or missing ancestor.");
                }
                ancestor = next;
            }
            for (int index = 0; index < frame.Trials.Count; index++)
            {
                AutoPlayInputSearchTrial trial = frame.Trials[index];
                if (!frame.Options.Contains(trial.Selection) || trial.Mask != frame.Input?.Masks[trial.Selection] || trial.Sampled && frame.Input is not null && (trial.SampledQuery is null || !frame.Input.Queries.Contains(trial.SampledQuery)) || trial.Outcome is AutoPlayInputTrialOutcome.ReturnedToObservedContext or AutoPlayInputTrialOutcome.ReachedNewBoundary or AutoPlayInputTrialOutcome.ControlReturn && !trial.Sampled || (trial.Outcome == AutoPlayInputTrialOutcome.NativeLoss) != (trial.Loss is not null))
                {
                    throw new InvalidDataException("A local input search trial has inconsistent sampling, outcome or physical alternative.");
                }
                trial.Loss?.Validate();
                if (trial.Loss is { } loss && loss.ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash)
                {
                    throw new InvalidDataException("A failed local input trial requires its authenticated original loss receipt.");
                }
                if (trial.ChildId is string childId && (!frames.TryGetValue(childId, out AutoPlayInputSearchFrame? child) || child.ParentId != frame.Id || child.ParentTrial != index))
                {
                    throw new InvalidDataException("A local input search trial has an invalid retained child edge.");
                }
            }
        }
        AutoPlayInputSearch observedKnowledge = new();
        foreach (AutoPlayInputSearchFrame frame in search.Frames)
        {
            foreach (AutoPlayInputSearchTrial trial in frame.Trials)
            {
                RecordInputSearchKnowledge(observedKnowledge, frame, trial);
            }
        }
        if (!search.SampledPhysicalEdges.SetEquals(observedKnowledge.SampledPhysicalEdges))
        {
            throw new InvalidDataException("Local input search progress must match its actual sampled native receipts.");
        }
    }

    private static void ImportInputSearch(AutoPlayRunState saved, string directory, Func<string?, string?> import, JsonElement manifest)
    {
        IEnumerable<AutoPlayInputSearch> searches = saved.InputSearch is { } active ? saved.InputSearchHistory.Append(active) : saved.InputSearchHistory;
        int historyIndex = 0;
        foreach (AutoPlayInputSearch search in searches)
        {
            JsonElement serialized = ReferenceEquals(search, saved.InputSearch) ? manifest.GetProperty(nameof(AutoPlayRunState.InputSearch)) : manifest.GetProperty(nameof(AutoPlayRunState.InputSearchHistory))[historyIndex++];
            RestoreInputSearchKnowledge(search, serialized.TryGetProperty(nameof(AutoPlayInputSearch.SampledPhysicalEdges), out _));
            ValidateInputSearchLedger(search);
            foreach (AutoPlayInputSearchFrame frame in search.Frames)
            {
                if (!saved.InputSearchChoiceKeys.Contains(frame.Key))
                {
                    throw new InvalidDataException("A local input search frame lost its separate coverage ownership.");
                }
                ValidateImportedInputSearchCheckpoint(frame, directory);
                frame.Checkpoint = import(frame.Checkpoint)!;
                frame.Context.BeforeCheckpoint = import(frame.Context.BeforeCheckpoint);
            }
        }
    }
}
