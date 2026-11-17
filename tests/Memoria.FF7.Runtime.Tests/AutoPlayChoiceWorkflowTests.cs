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
        RunStableFieldIdentity();
        RunRecordedTravelChoices();
        RunChangedTravelChoice();
        RunCompletedGatewayAdvice();
        RunStoryProgress(false, true);
        RunStoryProgress(true, true);
        RunStoryProgress(true, false);
        RunStoryProgress(false, true, 591);
        RunProgressingAlternative();

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

    private static void RunStableFieldIdentity()
    {
        Ff7SessionSave original = Fixture(1, 104, 2, 3);
        string key = ReadFieldChoiceKey(original, "entry");
        byte[] persistent = original.Vm.PersistentVariables.ToArray();
        byte[] map = original.Vm.MapVariables.ToArray();
        persistent[17] = 91;
        map[23] = 42;
        Ff7SessionSave changed = original with { Vm = original.Vm with { PersistentVariables = persistent, MapVariables = map, Tick = original.Vm.Tick + 10 } };
        Check(ReadFieldChoiceKey(changed, "entry") == key, "Incidental native variables or ticks changed a new field choice's stable identity.");
        Check(ReadFieldChoiceKey(changed, "changed-entry") != key, "A changed native entry predicate reused an older choice identity.");
        Check(ReadFieldChoiceKey(Fixture(2, 105, 2, 3), "entry") != key, "A distinct native script owner reused an older choice identity.");
        Ff7AutoPlayState state = new() { Active = ChoiceActive("entry") };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => original, planner, state, (_, _) => "legacy-before.ffsave");
        workflow.ChooseSelection(original.Process.Dialogues.Single());
        state.Coverage.Choices.Single().Key = "legacy-active-key";
        state.Choice = state.Choice! with { Key = "legacy-active-key" };
        state = state.Clone();
        planner = new(state.Coverage);
        workflow = new(() => changed, planner, state, (_, _) => throw new Exception("An existing legacy choice must retain its original checkpoint."));
        Check(workflow.ChooseSelection(changed.Process.Dialogues.Single()) == 2 && state.Choice!.Key == "legacy-active-key" && state.Coverage.Choices.Count == 1, "Cold restoration renamed an active legacy choice or replayed its completed setup.");
    }

    private static void RunRecordedTravelChoices()
    {
        Ff7SessionSave snapshot = Fixture(1, 104, 2, 3);
        string outer = ReadFieldChoiceKey(snapshot, "entry");
        string inner = ReadFieldChoiceKey(Fixture(2, 105, 4, 5), "entry");
        AutoPlayEventRoute route = new("destination", "entry", false, [new(outer, 3), new(inner, 5)], "event");
        Ff7AutoPlayState state = new() { Active = ChoiceActive("entry") with { TravelRoute = route, TravelSelections = [] } };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        int checkpoints = 0;
        Ff7AutoPlayChoiceWorkflow CreateWorkflow()
        {
            return new(() => snapshot, planner, state, (_, _) =>
            {
                checkpoints++;
                return "unexpected.ffsave";
            });
        }
        Ff7AutoPlayChoiceWorkflow workflow = CreateWorkflow();
        string unchanged = NativeJson(snapshot);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 3, "Recorded field travel ignored its authenticated nondefault first option.");
        Check(NativeJson(snapshot) == unchanged, "Selecting a recorded option changed native state before its ordinary execution.");
        Check(state.Active!.TravelSelections!.SequenceEqual(new[] { new AutoPlayChoiceSelection(outer, 3) }), "Recorded travel omitted its exact executed prefix.");
        state = JsonSerializer.Deserialize<Ff7AutoPlayState>(JsonSerializer.Serialize(state))!;
        planner = new(state.Coverage);
        workflow = CreateWorkflow();
        snapshot = Fixture(2, 105, 4, 5);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 5, "Cold travel restoration lost its authenticated prefix or next recorded option.");
        Check(state.Active!.TravelSelections!.SequenceEqual(route.Selections!), "Cold travel restoration changed its recorded executed sequence.");
        Check(state.Choice is null && state.Coverage.Choices.Count == 0 && state.PendingRestore is null && checkpoints == 0, "Recorded travel incorrectly opened exploratory choices or checkpoint restores.");
    }

    private static void RunChangedTravelChoice()
    {
        Ff7SessionSave snapshot = Fixture(1, 104, 2, 3);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        planner.Observe(new("visit", "resource"), [new("event", AutoPlayEventKind.Interaction, ConditionSignature: "entry"), new("unrelated", AutoPlayEventKind.Interaction, ConditionSignature: "entry")]);
        planner.Completed("visit", "event", "destination", observedStoryChanged: false, selections: [new("legacy-first", 3)], entrySignature: "entry", entrySignatureAuthoritative: true, targetId: "event");
        planner.Completed("visit", "event", "destination", observedStoryChanged: false, selections: [new("legacy-second", 2)], entrySignature: "entry", entrySignatureAuthoritative: true, targetId: "event");
        planner.Completed("visit", "unrelated", "destination", observedStoryChanged: false, selections: [new("unrelated-key", 3)], entrySignature: "entry", entrySignatureAuthoritative: true, targetId: "unrelated");
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "event");
        AutoPlayEventRoute route = coverage.Routes.First();
        state.Active = ChoiceActive("entry") with { TravelRoute = route, TravelSelections = [] };
        int checkpoints = 0;
        Ff7AutoPlayChoiceWorkflow workflow = new(() => snapshot, planner, state, (_, _) =>
        {
            checkpoints++;
            return "current-domain.ffsave";
        });
        string unchanged = NativeJson(snapshot);
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 2, "A changed native choice blindly applied its obsolete recorded option.");
        Check(NativeJson(snapshot) == unchanged, "Retiring stale route observations changed native progress, PC or RNG.");
        Check(state.Active!.TravelRoute is null && state.Active.TravelSelections is null, "A changed choice retained its unauthenticated active route.");
        Check(coverage.Routes.Count == 0 && coverage.RetiredRoutes.Count == 2 && coverage.GlobalRouteRediscovery, "Reached legacy choice routes were not retired and scheduled for current-domain observation.");
        Check(state.Coverage.Visits.Single().Events.Single(item => item.Event.Id == "unrelated").Routes.Count == 1, "Choice identity migration retired an unrelated event's route.");
        Check(state.Choice is not null && state.Coverage.Choices.Single().ActiveOption == 2 && checkpoints == 1 && state.PendingRestore is null, "Changed travel did not explore its currently reached native choice from an exact before-choice checkpoint.");
    }

    private static void RunCompletedGatewayAdvice()
    {
        string source = "ff7:100:source-native";
        string destination = "ff7:200:destination-native";
        string condition = "source-native:gateway:0:200:3:native-transfer:1";
        AutoPlayChoiceSelection incidental = new("historical-approach-choice", 0);
        AutoPlayEventRoute route = new(destination, condition, false, [incidental], "gateway:0");
        Ff7AutoPlayActiveEvent active = new(source, "gateway:0", condition, 1, -1, -1, Ff7AutoPlayTargetKind.Gateway, 200, 10, "before-native-transfer", NavigationKey: "source-component", TravelRoute: route, TravelSelections: []);
        Ff7AutoPlayObservation observation = new(destination, "destination-native", 2, [], true, true) { NavigationKey = "destination-component" };
        Ff7AutoPlayState state = new() { Active = active };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        AutoPlayEvent gateway = new(active.EventId, AutoPlayEventKind.Transition, ConditionSignature: condition);
        planner.Observe(new(source, "source-native"), [gateway]);
        planner.ObserveNavigation(source, active.NavigationKey!, [gateway]);
        planner.Observe(new(destination, "destination-native"), []);
        planner.ObserveNavigation(destination, observation.NavigationKey, []);
        planner.ObserveChoice(incidental.Key, 2);
        planner.Completed(source, gateway.Id, destination, observedStoryChanged: false, selections: route.Selections, entrySignature: condition, entrySignatureAuthoritative: true, targetId: gateway.Id);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> coverage = state.Coverage.Visits.Single(visit => visit.Visit.Key == source).Events.Single();
        string history = JsonSerializer.Serialize(state.Coverage);
        string choices = JsonSerializer.Serialize(state.Coverage.Choices);
        Ff7AutoPlayActiveEvent refreshed = Ff7AutoPlayPolicy.RefreshCompletedGatewayTravel(active, observation, nativeGatewayProved: true, liveChoice: false);
        AutoPlayTravelChoice.RequireComplete(refreshed.TravelRoute!, refreshed.TravelSelections);
        Check(refreshed.TravelRoute!.Selections is { Count: 0 } && refreshed.TravelSelections is { Count: 0 } && !refreshed.Accepted && !refreshed.TransitionOwned, "An actually proved native gateway handoff may refresh only its selected zero-choice advice without inventing script acceptance or transition-owner flags.");
        Check(route.Selections!.SequenceEqual([incidental]) && JsonSerializer.Serialize(state.Coverage) == history, "Refreshing derived gateway advice must preserve the historical route, all choice ledgers and all source coverage.");
        Ff7AutoPlayPolicy.RecordControlReturn(planner, refreshed, observation, navigationReceipt: true, exitSignature: null, progressChanged: false, selections: refreshed.TravelSelections!);
        Check(coverage.Routes.Any(known => known.Selections!.SequenceEqual([incidental])) && coverage.Routes.Any(known => known.Selections is { Count: 0 }) && JsonSerializer.Serialize(state.Coverage.Choices) == choices, "Normal completion must append the actual zero-choice route while retaining the earlier journey and its choices.");
        Reject(active, observation, nativeProof: false);
        Reject(active, observation, liveChoice: true);
        Reject(active with { Kind = Ff7AutoPlayTargetKind.ActorTalk, Entity = 1, Script = 1 }, observation);
        Reject(active with { Entity = 1 }, observation);
        Reject(active with { Script = 1 }, observation);
        Reject(active with { DestinationField = 201 }, observation);
        Reject(active, observation with { VisitId = "ff7:201:other" });
        Reject(active, observation with { Generation = active.Generation });
        Reject(active with { VisitId = destination }, observation);
        Reject(active with { ConditionFingerprint = "changed-source-condition" }, observation);
        Reject(active with { TravelRoute = route with { TargetId = "other-event" } }, observation);
        Reject(active with { TravelRoute = route with { DestinationVisitKey = "ff7:201:other" } }, observation);
        Reject(active with { TravelRoute = route with { Selections = [incidental, new("next-choice", 1)] }, TravelSelections = [incidental] }, observation);
        Reject(active with { TravelSelections = [new("unexpected-choice", 1)] }, observation);
        Ff7AutoPlayActiveEvent complete = active with { Kind = Ff7AutoPlayTargetKind.ActorTalk, TravelSelections = [incidental] };
        Check(ReferenceEquals(Ff7AutoPlayPolicy.RefreshCompletedGatewayTravel(complete, observation, nativeGatewayProved: false, liveChoice: false), complete), "An already authenticated recorded choice execution must keep the existing completion contract unchanged.");
        AutoPlayTravelChoice.RequireComplete(complete.TravelRoute!, complete.TravelSelections);
        Ff7AutoPlayActiveEvent old = active with { TravelRoute = route with { Selections = [] }, TravelSelections = null };
        Ff7AutoPlayActiveEvent restored = Ff7AutoPlayPolicy.RefreshCompletedGatewayTravel(old, observation, nativeGatewayProved: true, liveChoice: false);
        AutoPlayTravelChoice.RequireComplete(restored.TravelRoute!, restored.TravelSelections);

        static void Reject(Ff7AutoPlayActiveEvent attempt, Ff7AutoPlayObservation actual, bool nativeProof = true, bool liveChoice = false)
        {
            Ff7AutoPlayActiveEvent result = Ff7AutoPlayPolicy.RefreshCompletedGatewayTravel(attempt, actual, nativeProof, liveChoice);
            bool rejected = false;
            try
            {
                AutoPlayTravelChoice.RequireComplete(result.TravelRoute!, result.TravelSelections);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected && ReferenceEquals(result, attempt), "A genuine replay mismatch, missing native proof, partial prefix or live choice must retain strict shared completion rejection.");
        }
    }

    private static string ReadFieldChoiceKey(Ff7SessionSave snapshot, string condition)
    {
        Ff7AutoPlayState state = new() { Active = ChoiceActive(condition) };
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => snapshot, planner, state, (_, _) => "identity.ffsave");
        workflow.ChooseSelection(snapshot.Process.Dialogues.Single());
        return state.Choice!.Key;
    }

    private static Ff7AutoPlayActiveEvent ChoiceActive(string condition)
    {
        return new("visit", "event", condition, 0, 0, 1, Ff7AutoPlayTargetKind.ActorTalk, null, 0, "witness", Accepted: true);
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

    private static void RunStoryProgress(bool legacy, bool baselineKnown, short continuationStory = 598)
    {
        Dictionary<string, Ff7SessionSave> checkpoints = new(StringComparer.Ordinal);
        Ff7SessionSave snapshot = WithStory(Fixture(1, 104, 2, 3), 592);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => snapshot, planner, state, (key, save) =>
        {
            checkpoints[key] = save;
            return key;
        }, checkpoints.ContainsKey, path => baselineKnown ? checkpoints[path].Vm.PersistentVariables[0] | checkpoints[path].Vm.PersistentVariables[1] << 8 : null);
        workflow.ChooseSelection(snapshot.Process.Dialogues.Single());
        snapshot = WithStory(Fixture(2, 105, 4, 5), 592);
        workflow.ChooseSelection(snapshot.Process.Dialogues.Single());
        if (legacy)
        {
            state.Choice = state.Choice! with { BeforeStory = null, Parent = state.Choice!.Parent! with { BeforeStory = null } };
        }
        state = state.Clone();
        Check(legacy || state.Choice!.BeforeStory == 592 && state.Choice.Parent!.BeforeStory == 592, "Cold choice serialization lost its exact native story baselines.");
        planner = new(state.Coverage);
        workflow = new(() => snapshot, planner, state, (key, save) =>
        {
            checkpoints[key] = save;
            return key;
        }, checkpoints.ContainsKey, path => baselineKnown ? checkpoints[path].Vm.PersistentVariables[0] | checkpoints[path].Vm.PersistentVariables[1] << 8 : null);
        snapshot = WithStory(Continuation(snapshot, 73), continuationStory);
        string native = NativeJson(snapshot);
        bool restore = workflow.CompleteAtControlReturn();
        Check(NativeJson(snapshot) == native, "Choice policy changed actual native PCs, inventory, rewards or RNG.");
        if (baselineKnown && continuationStory > 592)
        {
            Check(!restore && state.PendingRestore is null && state.Choice is null, "Native story progress restored an earlier branch instead of retaining its current continuation.");
            Check(state.Coverage.Choices.All(choice => choice.CompletedOptions.Count == 1 && choice.PolicyDeferredOptions.Count == 1 && choice.PolicyExhausted && !choice.Finished), "Story progress falsely completed unobserved nested alternatives.");
        }
        else
        {
            Check(restore && state.PendingRestore is not null && state.Choice?.Parent is not null, "An unknown or decreased story baseline invented progress or discarded fallback exploration.");
            Check(state.Coverage.Choices.All(choice => choice.PolicyDeferredOptions.Count == 0), "A continuation without proved story advance deferred unobserved native alternatives.");
        }
    }

    private static void RunProgressingAlternative()
    {
        Dictionary<string, Ff7SessionSave> checkpoints = new(StringComparer.Ordinal);
        Ff7SessionSave snapshot = WithStory(Fixture(1, 104, 2, 4), 592);
        Ff7AutoPlayState state = new();
        AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
        Ff7AutoPlayChoiceWorkflow workflow = new(() => snapshot, planner, state, (key, save) =>
        {
            checkpoints[key] = save;
            return key;
        });
        workflow.ChooseSelection(snapshot.Process.Dialogues.Single());
        snapshot = Continuation(snapshot, 11);
        Check(workflow.CompleteAtControlReturn(), "A choice without story progress stopped exploring its alternatives.");
        snapshot = checkpoints[state.PendingRestore!.Path];
        state.PendingRestore = null;
        Check(workflow.ChooseSelection(snapshot.Process.Dialogues.Single()) == 3, "The fallback did not execute the next native option.");
        snapshot = WithStory(Continuation(snapshot, 29), 598);
        string successful = NativeJson(snapshot);
        Check(!workflow.CompleteAtControlReturn() && state.PendingRestore is null && state.Choice is null, "A progressing alternative restored the older nonprogressing primary continuation.");
        Check(NativeJson(snapshot) == successful, "The successful alternative's native continuation was replaced.");
        AutoPlayChoiceCoverage coverage = state.Coverage.Choices.Single();
        Check(coverage.CompletedOptions.SequenceEqual([2, 3]) && coverage.PolicyDeferredOptions.SetEquals([4]) && coverage.PolicyExhausted && !coverage.Finished, "A progressing alternative lost observed options or claimed an unexecuted alternative completed.");
    }

    private static Ff7SessionSave WithStory(Ff7SessionSave snapshot, short story)
    {
        byte[] persistent = snapshot.Vm.PersistentVariables.ToArray();
        persistent[0] = unchecked((byte)story);
        persistent[1] = unchecked((byte)(story >> 8));
        return snapshot with { Vm = snapshot.Vm with { PersistentVariables = persistent } };
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
        persistent[2] = outcome;
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
