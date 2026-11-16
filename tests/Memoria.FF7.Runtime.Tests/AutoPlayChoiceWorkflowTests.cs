using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class AutoPlayChoiceWorkflowTests
{
    public static void Run()
    {
        Dictionary<string, byte[]> checkpoints = new(StringComparer.Ordinal);
        Ff7SessionSave snapshot = Fixture(1, 104, 2, 3);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = CreateWorkflow();
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 2, "A native domain beginning at two must execute option two first.");
        string parentKey = state.Choice!.Key;
        string beforePath = planner.State.Choices.Single().BeforeCheckpoint!;
        Check(checkpoints.Count == 1, "The first selection must create one exact before-choice checkpoint.");
        snapshot = Fixture(91, 104, 2, 3);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 2 && state.Choice!.Key == parentKey && state.Choice.Parent is null, "Request numbering must not change native choice identity or create a nested choice.");
        snapshot = Fixture(92, 105, 4, 5);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 4 && state.Choice!.Parent!.Key == parentKey, "A nested native owner must preserve its active parent choice.");
        string childKey = state.Choice!.Key;
        snapshot = Continuation(snapshot, 11);
        string primaryChild = NativeJson(snapshot);
        Check(workflow.CompleteAtControlReturn(), "A remaining nested alternative must queue its original before-choice restore.");
        Check(state.PendingRestore!.Path == planner.State.Choices.Single(choice => choice.Key == childKey).BeforeCheckpoint, "Alternative exploration must use the exact recorded before-choice path.");
        RestorePending();
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 5, "Cold restoration must execute the remaining nested alternative.");
        snapshot = Continuation(snapshot, 29);
        Check(workflow.CompleteAtControlReturn(), "Exhausted nested options must queue the first primary continuation.");
        Check(state.Choice!.Key == parentKey && state.PendingRestore!.RetiredPaths!.Length == 2, "Nested completion must retain the parent and retire only its own checkpoint pair.");
        RestorePending();
        Check(NativeJson(snapshot) == primaryChild, "Nested completion did not restore its exact primary native PC, variables and RNG state.");
        Check(workflow.CompleteAtControlReturn() && state.PendingRestore!.Path == beforePath, "Parent exploration must continue from its own original before-choice save.");
        RestorePending();
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 3, "Parent replay must execute its remaining native option.");
        snapshot = Continuation(snapshot, 41);
        Check(workflow.CompleteAtControlReturn() && state.Choice is null, "All parent alternatives must finish the active choice chain.");
        RestorePending();
        Check(NativeJson(snapshot) == primaryChild, "Finishing the parent must restore the primary continuation rather than the last alternative.");
        Check(state.Coverage.Choices.All(choice => choice.Finished && choice.CompletedOptions.Count == 2), "Cold native restores must retain all observed shared choice coverage.");
        RunMissingCheckpoint(false);
        RunMissingCheckpoint(true);

        Ff7AutoPlayChoiceWorkflow CreateWorkflow()
        {
            return new(() => snapshot with { AutoPlay = state }, planner, state, (key, save) =>
            {
                Check(save.AutoPlay is null, "Choice snapshots must not recursively embed their autoplay checkpoint graph.");
                string path = key + ".ffsave";
                using MemoryStream output = new();
                CombinedSessionCodec.Write(output, new() { ActiveGame = SessionGame.Ff7, Ff7 = save });
                checkpoints[path] = output.ToArray();
                return path;
            });
        }

        void RestorePending()
        {
            Ff7AutoPlayRestoreRequest restore = state.PendingRestore!;
            using MemoryStream input = new(checkpoints[restore.Path]);
            snapshot = CombinedSessionCodec.Read(input).Ff7!;
            Check(snapshot.AutoPlay is null, "A native choice checkpoint unexpectedly contained observation state.");
            state = state.Clone();
            state.PendingRestore = null;
            planner = new(state.Coverage);
            workflow = CreateWorkflow();
            foreach (string retired in restore.RetiredPaths ?? [])
            {
                checkpoints.Remove(retired);
            }
        }
    }

    private static void RunMissingCheckpoint(bool completePriorOption)
    {
        Dictionary<string, Ff7SessionSave> checkpoints = new(StringComparer.Ordinal);
        Ff7SessionSave snapshot = Fixture(1, 104, 2, 3);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => snapshot, planner, state, (key, save) =>
        {
            string path = key + ".ffsave";
            checkpoints[path] = save;
            return path;
        }, checkpoints.ContainsKey);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 2, "The authored parent choice did not begin.");
        Ff7AutoPlayChoiceState parent = state.Choice!;
        snapshot = Fixture(2, 105, 4, 6);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 4, "The authored nested choice did not begin.");
        AutoPlayChoiceCoverage child = planner.State.Choices.Single(choice => choice.Key == state.Choice!.Key);
        snapshot = Continuation(snapshot, 11);
        string primaryContinuation = NativeJson(snapshot);
        if (completePriorOption)
        {
            Check(workflow.CompleteAtControlReturn(), "The first observed option did not record its primary continuation.");
            snapshot = checkpoints[state.PendingRestore!.Path];
            state.PendingRestore = null;
            Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 5, "The second observed option did not begin from the exact before-choice boundary.");
            snapshot = Continuation(snapshot, 29);
        }
        checkpoints.Remove(child.BeforeCheckpoint!);
        string unchangedNative = NativeJson(snapshot);
        Check(workflow.CompleteAtControlReturn(), "A missing optional before-choice checkpoint prevented native continuation.");
        Check(NativeJson(snapshot) == unchangedNative, "Deferring missing exploration changed current native progress or RNG.");
        Check(child.CompletedOptions.SequenceEqual(completePriorOption ? new[] { 4, 5 } : new[] { 4 }), "Missing optional exploration changed the observed completed options.");
        Check(child.PolicyDeferredOptions.SetEquals(completePriorOption ? new[] { 6 } : new[] { 5, 6 }), "Unobserved alternatives were completed or omitted rather than deferred.");
        Check(child.PolicyExhausted && !child.Finished, "Deferred coverage was incorrectly reported as fully observed.");
        Check(state.Status?.Contains("before-choice checkpoint is missing", StringComparison.Ordinal) == true, "Missing optional exploration omitted its explicit status warning.");
        Check(state.Choice == parent && planner.State.Choices.Single(choice => choice.Key == parent.Key).ActiveOption == 2, "Missing nested exploration discarded or completed its parent.");
        Ff7AutoPlayRestoreRequest restore = state.PendingRestore!;
        Check(restore.Path == child.ContinuationCheckpoint && checkpoints.ContainsKey(restore.Path), "Missing optional exploration queued its absent before-choice checkpoint.");
        snapshot = checkpoints[restore.Path];
        state.PendingRestore = null;
        Check(NativeJson(snapshot) == primaryContinuation, "Missing optional exploration failed to restore the exact native primary continuation, including PC, variables and RNG.");
        Check(snapshot.AutoPlay is null && state.Choice == parent, "Restoring the native continuation replaced its surviving exploration parent.");
    }

    private static Ff7SessionSave Fixture(long requestId, int pc, int first, int last)
    {
        var field = NativeDialogueTests.Field();
        Ff7FieldHost host = new(field, (_, _) => 1, new Ff7FieldProcess { Tick = 42, Windows = [new Ff7Window(0, 10, 20, 200, 70)] });
        host.Execute(new(requestId, 0, 1, pc, new FieldDialogueCommand(0, 0, first, last, first)));
        FieldMachine machine = new(FieldProgram.FromPcScripts(field.Scripts));
        machine.Initialize();
        return new()
        {
            ResourceProfile = "authored-dialogue",
            Language = "en",
            FieldName = field.Name,
            Progress = new Ff7Progress { InitialKernelData = [1], Characters = [new Ff7CharacterState(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [], Materia = [], Gil = 240, FieldRandomIndex = 73 },
            Vm = machine.Capture() with { NextRequestId = requestId + 1 },
            Process = host.Capture()
        };
    }

    private static Ff7SessionSave Continuation(Ff7SessionSave snapshot, byte outcome)
    {
        byte[] persistent = snapshot.Vm.PersistentVariables.ToArray();
        persistent[0] = outcome;
        return snapshot with { Progress = snapshot.Progress with { FieldRandomIndex = outcome }, Vm = snapshot.Vm with { PersistentVariables = persistent, Tick = snapshot.Vm.Tick + 1 }, Process = snapshot.Process with { Tick = snapshot.Process.Tick + 1 } };
    }

    private static string NativeJson(Ff7SessionSave snapshot)
    {
        return JsonSerializer.Serialize(snapshot with { AutoPlay = null });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
