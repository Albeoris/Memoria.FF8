using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayChoiceState(string Key, Ff7AutoPlayActiveEvent? BeforeActive, Ff7AutoPlayChoiceState? Parent = null, Ff7AutoPlayActiveEvent? ContinuationActive = null, string? OwnerIdentity = null, int? BeforeStory = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string[]? RetiredCandidatePaths = null);
public sealed record Ff7AutoPlayRestoreRequest(string Path, string[]? RetiredPaths = null);

/// <summary>Adapts shared choice coverage to exact native before-choice and primary-continuation saves.</summary>
public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    private readonly Ff7Session? nativeSession;
    private readonly Func<Ff7SessionSave> capture;
    private readonly AutoPlayPlanner<Ff7AutoPlayDeferral> planner;
    private readonly Ff7AutoPlayState state;
    private readonly Func<string, Ff7SessionSave, string> writeCheckpoint;
    private readonly Func<string, bool> checkpointExists;
    private readonly Func<string, int?> readCheckpointStory;

    public Ff7AutoPlayChoiceWorkflow(Ff7Session session, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayState state, Func<string, Ff7SessionSave, string> writeCheckpoint) : this(session.Capture, planner, state, writeCheckpoint, File.Exists)
    {
        nativeSession = session;
    }

    internal Ff7AutoPlayChoiceWorkflow(Func<Ff7SessionSave> capture, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayState state, Func<string, Ff7SessionSave, string> writeCheckpoint, Func<string, bool>? checkpointExists = null, Func<string, int?>? readCheckpointStory = null)
    {
        this.capture = capture;
        this.planner = planner;
        this.state = state;
        this.writeCheckpoint = writeCheckpoint;
        this.checkpointExists = checkpointExists ?? (_ => true);
        this.readCheckpointStory = readCheckpointStory ?? ReadCheckpointStory;
    }

    public IReadOnlyList<AutoPlayChoiceSelection> Selections
    {
        get
        {
            List<AutoPlayChoiceSelection> selections = [];
            for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
            {
                AutoPlayChoiceCoverage coverage = Coverage(choice.Key);
                if (coverage.ActiveOption is int selection)
                {
                    selections.Add(new(choice.Key, selection));
                }
            }
            selections.Reverse();
            return selections;
        }
    }

    public int? ChooseSelection(Ff7Dialogue dialogue)
    {
        if (dialogue.FirstChoice is not int first || dialogue.LastChoice is not int last)
        {
            return null;
        }
        if (state.PendingRestore is not null)
        {
            throw new InvalidOperationException("Restore the queued native choice checkpoint before selecting another option.");
        }
        Ff7SessionSave before = capture();
        string owner = ReadOwnerIdentity(before, dialogue, first, last);
        Ff7AutoPlayChoiceState? current = state.Choice;
        string key = current?.OwnerIdentity == owner ? current.Key : before.World is { } world ? Hash(new { Owner = owner, before.Vm.PersistentVariables, Local = world.Vm.Local, Conditions = state.Active?.ConditionFingerprint, Parent = current?.Key, ParentSelections = Selections }) : "field-choice-v2:" + Hash(new { Owner = owner, Visit = state.Active?.VisitId ?? before.FieldName, Conditions = state.Active?.ConditionFingerprint, Parent = current?.Key, ParentSelections = Selections });
        if (state.Active is { TravelRoute: { } route } travel)
        {
            IReadOnlyList<AutoPlayChoiceSelection> executed = travel.TravelSelections ?? [];
            if (route.Selections is not null && executed.Count < route.Selections.Count && route.Selections.Take(executed.Count).SequenceEqual(executed) && route.Selections[executed.Count] is { } expected && expected.Key == key && expected.Selection >= first && expected.Selection <= last)
            {
                int selection = AutoPlayTravelChoice.SelectRecordedSelection(route, executed, key, first, last);
                state.Active = travel with { TravelSelections = [.. executed, new(key, selection)] };
                return selection;
            }
            RetireChangedTravelChoice(travel, key, first, last, executed);
        }
        if (current?.OwnerIdentity != owner)
        {
            planner.ObserveChoice(key, checked(last - first + 1), first);
            if (planner.PlanChoice(key).Kind == AutoPlayChoiceStepKind.Complete)
            {
                return SelectCompletedNativeChoice(before, dialogue, Coverage(key), first);
            }
            current = new(key, state.Active, current, OwnerIdentity: owner, BeforeStory: ReadStory(before));
            state.Choice = current;
        }
        Ff7AutoPlayChoiceState selectedChoice = current ?? throw new InvalidDataException("The native choice has no exploration owner.");
        AutoPlayChoiceCoverage coverage = Coverage(selectedChoice.Key);
        if (coverage.ActiveOption is int active)
        {
            return active;
        }
        AutoPlayChoiceStep step = planner.PlanChoice(selectedChoice.Key);
        if (step.Kind == AutoPlayChoiceStepKind.SaveBeforeChoice)
        {
            string path = Write(selectedChoice.Key + "-before", before);
            planner.RegisterChoiceCheckpoint(selectedChoice.Key, path);
            step = planner.PlanChoice(selectedChoice.Key);
        }
        if (step.Selection is not int selected)
        {
            throw new InvalidOperationException("The restored choice has no unconsumed native option.");
        }
        planner.BeginChoiceOption(selectedChoice.Key, selected);
        return selected;
    }

    private void RetireChangedTravelChoice(Ff7AutoPlayActiveEvent active, string key, int first, int last, IReadOnlyList<AutoPlayChoiceSelection> executed)
    {
        AutoPlayEventRoute route = active.TravelRoute!;
        if (route.ConditionSignature != active.ConditionFingerprint || route.TargetId != active.EventId)
        {
            throw new NotSupportedException("A changed travel choice lacks its authenticated native source attempt.");
        }
        if (route.Selections is null || executed.Count > route.Selections.Count || !route.Selections.Take(executed.Count).SequenceEqual(executed))
        {
            AutoPlayTravelChoice.SelectRecordedSelection(route, executed, key, first, last);
        }
        planner.RetireTravelRoute(active.VisitId, active.EventId, route, key, first, last, executed);
        if (key.StartsWith("field-choice-v2:", StringComparison.Ordinal) && executed.Count == 0)
        {
            // Retire only reached event routes whose first choice uses the superseded identity schema.
            var coverage = planner.State.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
            foreach (AutoPlayEventRoute legacy in coverage.Routes.Where(candidate => candidate.ConditionSignature == active.ConditionFingerprint && candidate.TargetId == active.EventId && candidate.Selections is { Count: > 0 } selections && !selections[0].Key.StartsWith("field-choice-v2:", StringComparison.Ordinal) && candidate.ObservedStoryChanged == false).ToArray())
            {
                planner.RetireTravelRoute(active.VisitId, active.EventId, legacy, key, first, last, []);
            }
        }
        state.Active = active with { TravelRoute = null, TravelSelections = null };
        state.Status = "The reached native choice changed; exploring its current authenticated domain.";
    }

    public bool CompleteAtControlReturn()
    {
        if (state.PendingRestore is not null)
        {
            return true;
        }
        if (state.Choice is not Ff7AutoPlayChoiceState choice)
        {
            return false;
        }
        AutoPlayChoiceCoverage coverage = Coverage(choice.Key);
        if (coverage.ActiveOption is null)
        {
            throw new InvalidOperationException("A choice continuation cannot complete before its selected native option executes.");
        }
        Ff7SessionSave current = capture();
        int? beforeStory = choice.BeforeStory ?? (coverage.BeforeCheckpoint is string path ? readCheckpointStory(path) : null);
        bool storyAdvanced = beforeStory is int baseline && ReadStory(current) > baseline;
        string? continuation = coverage.ContinuationCheckpoint;
        if (continuation is null)
        {
            continuation = Write(choice.Key + "-continuation", current);
            choice = choice with { ContinuationActive = state.Active };
            state.Choice = choice;
        }
        else
        {
            continuation = PreferActualContinuation(choice, coverage, current) ?? continuation;
            choice = state.Choice ?? choice;
        }
        planner.CompleteChoiceAtControlReturn(choice.Key, continuation);
        if (storyAdvanced)
        {
            foreach (int option in Enumerable.Range(coverage.FirstOption, coverage.OptionCount).Where(option => !coverage.CompletedOptions.Contains(option)))
            {
                coverage.PolicyDeferredOptions.Add(option);
            }
            planner.FinishChoice(choice.Key);
            state.Choice = choice.Parent;
            state.Status = "Optional choice alternatives were deferred after native story progress; continuing from the current native state.";
            return CompleteAtControlReturn();
        }
        AutoPlayChoiceStep next = planner.PlanChoice(choice.Key);
        if (next.Kind == AutoPlayChoiceStepKind.Replay && next.Checkpoint is string beforeCheckpoint && !checkpointExists(beforeCheckpoint))
        {
            foreach (int option in Enumerable.Range(coverage.FirstOption, coverage.OptionCount).Where(option => !coverage.CompletedOptions.Contains(option)))
            {
                coverage.PolicyDeferredOptions.Add(option);
            }
            state.Status = "Optional choice exploration was deferred because its before-choice checkpoint is missing; preserving the recorded native continuation.";
            next = planner.PlanChoice(choice.Key);
        }
        if (next.Kind == AutoPlayChoiceStepKind.Replay)
        {
            state.Active = choice.BeforeActive;
            state.PendingRestore = new(next.Checkpoint ?? throw new InvalidDataException("An alternative requires its original native checkpoint."));
            return true;
        }
        if (next.Kind != AutoPlayChoiceStepKind.RestoreContinuation || next.Checkpoint is null)
        {
            throw new InvalidOperationException("Choice coverage did not provide its primary native continuation.");
        }
        planner.FinishChoice(choice.Key);
        state.Choice = choice.Parent;
        state.Active = choice.ContinuationActive;
        state.PendingRestore = new(next.Checkpoint, new[] { coverage.BeforeCheckpoint, coverage.ContinuationCheckpoint }.OfType<string>().Concat(choice.RetiredCandidatePaths ?? []).Distinct(StringComparer.Ordinal).ToArray());
        return true;
    }

    private AutoPlayChoiceCoverage Coverage(string key)
    {
        return planner.State.Choices.Single(choice => choice.Key == key);
    }

    private static int ReadStory(Ff7SessionSave snapshot)
    {
        return unchecked((short)(snapshot.Vm.PersistentVariables[0] | snapshot.Vm.PersistentVariables[1] << 8));
    }

    private static int? ReadCheckpointStory(string path)
    {
        try
        {
            using FileStream input = File.OpenRead(path);
            return CombinedSessionCodec.Read(input).Ff7 is { } snapshot ? ReadStory(snapshot) : throw new InvalidDataException("The native choice checkpoint does not contain its FFVII session.");
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private string Write(string key, Ff7SessionSave snapshot)
    {
        string path = writeCheckpoint(key, snapshot with { AutoPlay = null });
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path;
    }

    private string ReadOwnerIdentity(Ff7SessionSave snapshot, Ff7Dialogue dialogue, int first, int last)
    {
        if (snapshot.World is { } world)
        {
            if (state.WorldDialogueOwner is { Command.Operation: WorldOperation.Choice } owner && owner.Id == dialogue.RequestId)
            {
                return Hash(new { world.Vm.ProgramIdentity, owner.ExecutingModel, owner.ActiveModel, owner.ProgramCounter, First = first, Last = last });
            }
            if (state.Active?.Kind is Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel && state.Choice?.OwnerIdentity is string retained)
            {
                return retained;
            }
            // Older native saves retain a live window without the optional observed host packet.
            // Establish a neutral observation identity from those saved facts; never replay its script.
            return Hash(new { world.Vm.ProgramIdentity, Request = dialogue.RequestId, dialogue.Window, First = first, Last = last, System = world.Vm.System.ProgramCounter });
        }
        FieldHostRequest? request = (snapshot.Vm.PendingRequests ?? []).SingleOrDefault(request => request.Id == dialogue.RequestId && request.Command is FieldDialogueCommand);
        request ??= snapshot.Process.Receipts.Select(receipt => receipt.Request).SingleOrDefault(request => request.Id == dialogue.RequestId && request.Command is FieldDialogueCommand);
        if (request is null)
        {
            throw new NotSupportedException("Automatic choice exploration requires the exact native dialogue script owner.");
        }
        return Hash(new { snapshot.FieldName, snapshot.Vm.ProgramIdentity, request.Entity, request.Priority, request.ProgramCounter, First = first, Last = last });
    }

    private static string Hash<T>(T value)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    }
}
