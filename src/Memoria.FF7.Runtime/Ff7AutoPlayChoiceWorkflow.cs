using System.Security.Cryptography;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayChoiceState(string Key, Ff7AutoPlayActiveEvent? BeforeActive, Ff7AutoPlayChoiceState? Parent = null, Ff7AutoPlayActiveEvent? ContinuationActive = null, string? OwnerIdentity = null);
public sealed record Ff7AutoPlayRestoreRequest(string Path, string[]? RetiredPaths = null);

/// <summary>Adapts shared choice coverage to exact native before-choice and primary-continuation saves.</summary>
public sealed class Ff7AutoPlayChoiceWorkflow
{
    private readonly Func<Ff7SessionSave> capture;
    private readonly AutoPlayPlanner<Ff7AutoPlayDeferral> planner;
    private readonly Ff7AutoPlayState state;
    private readonly Func<string, Ff7SessionSave, string> writeCheckpoint;

    public Ff7AutoPlayChoiceWorkflow(Ff7Session session, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayState state, Func<string, Ff7SessionSave, string> writeCheckpoint) : this(session.Capture, planner, state, writeCheckpoint)
    {
    }

    internal Ff7AutoPlayChoiceWorkflow(Func<Ff7SessionSave> capture, AutoPlayPlanner<Ff7AutoPlayDeferral> planner, Ff7AutoPlayState state, Func<string, Ff7SessionSave, string> writeCheckpoint)
    {
        this.capture = capture;
        this.planner = planner;
        this.state = state;
        this.writeCheckpoint = writeCheckpoint;
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
        if (current?.OwnerIdentity != owner)
        {
            string key = Hash(new { Owner = owner, before.Vm.PersistentVariables, before.Vm.MapVariables, Conditions = state.Active?.ConditionFingerprint, Parent = current?.Key, ParentSelections = Selections });
            planner.ObserveChoice(key, checked(last - first + 1), first);
            if (planner.PlanChoice(key).Kind == AutoPlayChoiceStepKind.Complete)
            {
                return first;
            }
            current = new(key, state.Active, current, OwnerIdentity: owner);
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
        string? continuation = coverage.ContinuationCheckpoint;
        if (continuation is null)
        {
            continuation = Write(choice.Key + "-continuation", capture());
            choice = choice with { ContinuationActive = state.Active };
            state.Choice = choice;
        }
        planner.CompleteChoiceAtControlReturn(choice.Key, continuation);
        AutoPlayChoiceStep next = planner.PlanChoice(choice.Key);
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
        state.PendingRestore = new(next.Checkpoint, new[] { coverage.BeforeCheckpoint, coverage.ContinuationCheckpoint }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray());
        return true;
    }

    private AutoPlayChoiceCoverage Coverage(string key)
    {
        return planner.State.Choices.Single(choice => choice.Key == key);
    }

    private string Write(string key, Ff7SessionSave snapshot)
    {
        string path = writeCheckpoint(key, snapshot with { AutoPlay = null });
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path;
    }

    private static string ReadOwnerIdentity(Ff7SessionSave snapshot, Ff7Dialogue dialogue, int first, int last)
    {
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
