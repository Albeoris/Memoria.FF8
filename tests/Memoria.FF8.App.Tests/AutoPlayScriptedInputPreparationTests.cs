using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedInputPreparationTests
{
    [Fact]
    public void ScheduledNativeExitAfterARealQueryDoesNotCreditThePendingInputAlternative()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayScriptedInputPreparationTests.1");
        byte[] original = File.ReadAllBytes(manifest);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(235, run.Session.State.FieldId);
        Dictionary<string, string> choices = run.Controller.State.Coverage.Choices.ToDictionary(choice => choice.Key, choice => JsonSerializer.Serialize(choice));
        int completed = run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
        string[] sceneChoices = run.Controller.State.SceneChoices.ToArray();
        for (int tick = 0; tick < 4 && run.Session.State.FieldId == 235 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(234, run.Session.State.FieldId);
        AutoPlayInputSearchFrame frame = Assert.Single(run.Controller.State.InputSearch!.Frames);
        AutoPlayInputSearchTrial trial = Assert.Single(frame.Trials);
        Assert.Equal(235, frame.FieldId);
        Assert.True(trial.Sampled);
        ScriptedInputQuery query = Assert.IsType<ScriptedInputQuery>(trial.SampledQuery);
        Assert.NotEqual(AutoPlayInputTrialOutcome.ControlReturn, trial.Outcome);
        Assert.Null(trial.Loss);
        AutoPlayChoiceCoverage choice = Assert.Single(run.Controller.State.Coverage.Choices, candidate => candidate.Key == frame.Key);
        Assert.Null(choice.ActiveOption);
        Assert.Empty(choice.CompletedOptions);
        Assert.False(choice.Finished);
        Assert.Equal(completed, run.Controller.State.Coverage.Choices.Sum(candidate => candidate.CompletedOptions.Count));
        foreach (KeyValuePair<string, string> previous in choices)
        {
            Assert.Equal(previous.Value, JsonSerializer.Serialize(run.Controller.State.Coverage.Choices.Single(candidate => candidate.Key == previous.Key)));
        }
        Assert.Equal(sceneChoices, run.Controller.State.SceneChoices);
        string checkpointPath = Assert.Single(Directory.GetFiles(run.Output, frame.Checkpoint, SearchOption.AllDirectories));
        GameState checkpoint = GameSession.Load(checkpointPath);
        long preparedFrame = checkpoint.EventState!.Value.GetProperty("Frame").GetInt64();
        Assert.True(AutoPlayController.HasNeutralScriptedInput(checkpoint.FieldButtonInput, 235, preparedFrame));
        int sampledIndex = run.QueryTimeline.FindIndex(sample => sample.Command.FieldId == 235 && sample.Command.ObjectId == query.ObjectId && sample.Command.ScriptId == query.ScriptId && sample.Command.Operation == query.Operation && sample.Command.Name == query.Opcode && sample.Command.Arguments.Count == 1 && sample.Command.Arguments[0] == query.Mask && sample.Input.PhysicalHeld == trial.Mask && (sample.Command.Arguments[0] & (query.Opcode == "KEYSCAN2" ? sample.Input.RawHeld : query.Opcode == "KEYON" ? sample.Input.Pressed.GetValueOrDefault() : sample.Input.Held.GetValueOrDefault())) != 0);
        Assert.True(sampledIndex >= 0, JsonSerializer.Serialize(run.QueryTimeline.Select(sample => new { sample.Command, sample.Input })));
        Assert.Equal(preparedFrame + 1, run.QueryTimeline[sampledIndex].Input.SampledFrame);
        int exitIndex = run.QueryTimeline.FindIndex(sample => sample.Command.FieldId == 235 && sample.Command.Name is "MAPJUMP" or "MAPJUMP3" or "MAPJUMPO");
        Assert.True(exitIndex > sampledIndex, JsonSerializer.Serialize(run.QueryTimeline.Select(sample => new { sample.Command, sample.Input })));
        Assert.DoesNotContain(run.InputCallbacks, sample => sample.Field == 234 && sample.Mask != 0);
        Assert.Equal(0, run.Session.State.FieldButtonInput!.PhysicalHeld);
        Assert.Equal(original, File.ReadAllBytes(manifest));
        string? timelineReport = Environment.GetEnvironmentVariable("MEMORIA_FF8_INPUT_TIMELINE_REPORT");
        if (!string.IsNullOrWhiteSpace(timelineReport))
        {
            string path = Path.GetFullPath(timelineReport);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { PreparedFrame = preparedFrame, FinalField = run.Session.State.FieldId, FinalFrame = run.World.Events.Frame, SampledQueryIndex = sampledIndex, NativeExitIndex = exitIndex, PendingTrial = trial, Timeline = run.QueryTimeline.Select(sample => new { sample.Command, sample.Input }) }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void NewPreparedCheckpointReplaysWithoutAddingNeutralCallbacksBeforeTheFirstPress()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.5");
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        AutoPlayAcceptanceTests.ScopeNativeInputCoverageToWardrobe(run.Controller);
        AutoPlayChoiceCoverage? inputChoice = null;
        for (int step = 0; step < 16 && inputChoice is null; step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
            inputChoice = run.Controller.State.Coverage.Choices.SingleOrDefault(choice => choice.Key.StartsWith("input:240:", StringComparison.Ordinal));
        }
        Assert.NotNull(inputChoice);
        Assert.True(inputChoice.InputCheckpointPrepared);
        ScriptedInputMetadata input = run.Controller.State.ScriptedChoiceInputs[inputChoice.Key];
        Assert.Equal(2, input.Masks.Count);
        string saved = Assert.Single(Directory.GetFiles(run.Output, inputChoice.BeforeCheckpoint!, SearchOption.AllDirectories));
        GameState checkpoint = GameSession.Load(saved);
        long preparedFrame = checkpoint.EventState!.Value.GetProperty("Frame").GetInt64();
        Assert.Equal(0, checkpoint.FieldButtonInput!.PhysicalHeld);
        Assert.Equal(preparedFrame + 1, run.InputCallbacks.First(sample => sample.Field == 240 && sample.Mask == input.Masks[0]).Frame);
        AutoPlayInputSearch search = run.Controller.State.InputSearch!;
        AutoPlayInputSearchFrame searchFrame = Assert.Single(search.Frames, frame => frame.Key == inputChoice.Key);
        for (int step = 0; step < 128 && !inputChoice.CompletedOptions.Contains(0); step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }
        Assert.Contains(0, inputChoice.CompletedOptions);
        Assert.True(search.Closed);
        // Verify replay preparation directly from the authentic saved frame after its real control return.
        System.Reflection.MethodInfo restoreFrame = typeof(AutoPlayController).GetMethod("RestoreInputSearchFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        System.Reflection.MethodInfo executeTrial = typeof(AutoPlayController).GetMethod("ExecuteInputSearchTrial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        restoreFrame.Invoke(run.Controller, [searchFrame]);
        executeTrial.Invoke(run.Controller, [searchFrame]);
        Assert.Null(inputChoice.ActiveOption);
        Assert.Equal(1, searchFrame.Trials.Last().Selection);
        Assert.True(searchFrame.Trials.Last().Sampled);
        Assert.Equal(input.Masks[1], searchFrame.Trials.Last().Mask);
        Assert.Equal(preparedFrame + 1, run.InputCallbacks.First(sample => sample.Field == 240 && sample.Mask == input.Masks[1]).Frame);
    }

    [Fact]
    public void OldChoiceMetadataDoesNotClaimItsCheckpointWasAlreadyPrepared()
    {
        AutoPlayChoiceCoverage choice = JsonSerializer.Deserialize<AutoPlayChoiceCoverage>("{\"Key\":\"input\",\"BeforeCheckpoint\":\"before.json\"}")!;
        Assert.False(choice.InputCheckpointPrepared);
        choice.InputCheckpointPrepared = true;
        Assert.True(JsonSerializer.Deserialize<AutoPlayChoiceCoverage>(JsonSerializer.Serialize(choice))!.InputCheckpointPrepared);
    }

    [Fact]
    public void LegacyInputReplayReobservesItsAuthenticCheckpointWithoutCopyingCompletedIndexes()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.5");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new() { PreserveArtifacts = true };
        run.Controller.ResumeFrom(manifest);
        AutoPlayAcceptanceTests.ScopeNativeInputCoverageToWardrobe(run.Controller);
        AutoPlayChoiceCoverage? legacyChoice = null;
        for (int step = 0; step < 16 && legacyChoice is null && run.Controller.Error is null; step++)
        {
            run.Controller.Advance(1);
            legacyChoice = run.Controller.State.Coverage.Choices.SingleOrDefault(choice => choice.Key.StartsWith("input:240:", StringComparison.Ordinal) && run.Controller.State.ScriptedChoiceInputs.TryGetValue(choice.Key, out ScriptedInputMetadata? input) && input.ObservedQueriesProven);
        }
        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.NotNull(legacyChoice);
        Assert.Null(legacyChoice.ActiveOption);
        AutoPlayInputSearchTrial initialTrial = Assert.Single(Assert.Single(run.Controller.State.InputSearch!.Frames, frame => frame.Key == legacyChoice.Key).Trials);
        Assert.Equal(0, initialTrial.Selection);
        Assert.True(initialTrial.Sampled);
        Assert.NotNull(initialTrial.SampledQuery);
        Assert.Empty(legacyChoice.CompletedOptions);
        Assert.True(legacyChoice.InputCheckpointPrepared);
        string scopedKey = legacyChoice.Key;
        ScriptedInputMetadata scoped = run.Controller.State.ScriptedChoiceInputs[scopedKey];
        string checkpointPath = Assert.Single(Directory.GetFiles(run.Output, legacyChoice.BeforeCheckpoint!, SearchOption.AllDirectories));
        byte[] checkpointBytes = File.ReadAllBytes(checkpointPath);
        GameState checkpoint = GameSession.Load(checkpointPath);
        long frame = checkpoint.EventState!.Value.GetProperty("Frame").GetInt64();
        ScriptedInputMetadata? discoveredLegacy = null;
        using (GameWorld before = new(new GameSession(checkpoint), TestResources.GameDirectory, validateWorldPresentation: false))
        {
            before.EnsureField();
            void ObserveQuery(ScriptCommand command)
            {
                if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON")
                {
                    discoveredLegacy ??= before.Events.DiscoverScriptedInput(command.ObjectId);
                }
            }
            before.Events.CommandExecuted += ObserveQuery;
            try
            {
                for (int callback = 0; callback < 8 && discoveredLegacy is null; callback++)
                {
                    before.AdvanceAutoPlayInputFrame();
                    Assert.Null(before.Events.LastFault);
                }
            }
            finally
            {
                before.Events.CommandExecuted -= ObserveQuery;
            }
        }
        ScriptedInputMetadata legacy = Assert.IsType<ScriptedInputMetadata>(discoveredLegacy);
        Assert.Equal(checkpointBytes, File.ReadAllBytes(checkpointPath));
        Assert.False(legacy.ObservedQueriesProven);
        Assert.Equal(scoped.Masks, legacy.Masks);
        Assert.Equal(2, legacy.Masks.Count);
        Assert.NotEqual(scoped.Fingerprint, legacy.Fingerprint);
        Assert.Contains(run.InputCallbacks, sample => sample.Field == 240 && sample.Frame == frame + 1 && sample.Mask == scoped.Masks[0]);
        int firstPressReceipts = run.InputCallbacks.Count(sample => sample.Field == 240 && sample.Frame == frame + 1 && sample.Mask == scoped.Masks[0]);
        JsonElement native = run.World.Events.Capture();
        GameState currentState = run.Session.State;
        string legacyKey = $"input:240:{legacy.Fingerprint}";
        Assert.DoesNotContain(run.Controller.State.Coverage.Choices, choice => choice.Key == legacyKey);

        // Model old controller metadata only: its active option was genuinely sampled from this unchanged checkpoint.
        run.Controller.State.InputSearch = null;
        run.Controller.State.InputSearchChoiceKeys.Remove(scopedKey);
        legacyChoice.ActiveOption = initialTrial.Selection;
        run.Controller.State.SceneChoices.Add(scopedKey);
        List<AutoPlayChoiceSelection>? Rename(IReadOnlyList<AutoPlayChoiceSelection>? selections)
        {
            return selections?.Select(selection => selection.Key == scopedKey ? selection with { Key = legacyKey } : selection).ToList();
        }
        legacyChoice.Key = legacyKey;
        run.Controller.State.ScriptedChoiceInputs.Remove(scopedKey);
        run.Controller.State.ScriptedChoiceInputs.Add(legacyKey, legacy);
        Assert.True(run.Controller.State.ScriptedInputs.Remove(scoped.Fingerprint, out int attempts));
        run.Controller.State.ScriptedInputs.Add(legacy.Fingerprint, attempts);
        run.Controller.State.ActiveSelections = Rename(run.Controller.State.ActiveSelections);
        run.Controller.State.PrimarySelections = Rename(run.Controller.State.PrimarySelections);
        for (int index = 0; index < run.Controller.State.SceneChoices.Count; index++)
        {
            if (run.Controller.State.SceneChoices[index] == scopedKey)
            {
                run.Controller.State.SceneChoices[index] = legacyKey;
            }
        }
        foreach (AutoPlayChoiceCoverage choice in run.Controller.State.Coverage.Choices)
        {
            choice.BeforeSelections = Rename(choice.BeforeSelections);
            foreach (string scope in choice.PolicyCheckpoints.Keys.ToArray())
            {
                AutoPlayPolicyChoiceCheckpoint policy = choice.PolicyCheckpoints[scope];
                choice.PolicyCheckpoints[scope] = policy with { BeforeSelections = Rename(policy.BeforeSelections) };
            }
        }
        if (run.Controller.State.ActiveObservedInput?.Fingerprint == scoped.Fingerprint)
        {
            run.Controller.State.ActiveObservedInput = legacy;
        }
        Assert.Same(currentState, run.Session.State);
        Assert.True(JsonElement.DeepEquals(native, run.World.Events.Capture()));

        for (int step = 0; step < 256 && legacyChoice.SupersededInputDomain is null && run.Controller.Error is null; step++)
        {
            run.Controller.Advance(1);
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Equal(new[] { 0 }, legacyChoice.CompletedOptions);
        Assert.False(legacyChoice.Finished);
        Assert.Null(legacyChoice.ActiveOption);
        string replacementKey = Assert.IsType<string>(legacyChoice.SupersededInputDomain);
        Assert.NotEqual(legacyKey, replacementKey);
        AutoPlayChoiceCoverage replacement = Assert.Single(run.Controller.State.Coverage.Choices, choice => choice.Key == replacementKey);
        Assert.True(run.Controller.State.ScriptedChoiceInputs[replacementKey].ObservedQueriesProven);
        Assert.Null(replacement.ActiveOption);
        AutoPlayInputSearchTrial replacementTrial = Assert.Single(Assert.Single(run.Controller.State.InputSearch!.Frames, frame => frame.Key == replacementKey).Trials);
        Assert.Equal(0, replacementTrial.Selection);
        Assert.True(replacementTrial.Sampled);
        Assert.NotNull(replacementTrial.SampledQuery);
        Assert.Empty(replacement.CompletedOptions);
        Assert.False(replacement.Finished);
        Assert.True(replacement.InputCheckpointPrepared);
        Assert.True(run.InputCallbacks.Count(sample => sample.Field == 240 && sample.Frame == frame + 1 && sample.Mask == scoped.Masks[0]) > firstPressReceipts);
        Assert.DoesNotContain(run.InputCallbacks, sample => sample.Field == 240 && sample.Mask == legacy.Masks[1]);
        Assert.True(run.World.HasCurrentFieldAllocation);
        run.Controller.SaveAndStop();
        native = run.World.Events.Capture();
        using InstalledRun cold = new() { PreserveArtifacts = true };
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        AutoPlayChoiceCoverage old = Assert.Single(cold.Controller.State.Coverage.Choices, choice => choice.Key == legacyKey);
        Assert.Equal(replacementKey, old.SupersededInputDomain);
        Assert.Equal(new[] { 0 }, old.CompletedOptions);
        Assert.False(old.Finished);
        AutoPlayChoiceCoverage restored = Assert.Single(cold.Controller.State.Coverage.Choices, choice => choice.Key == replacementKey);
        Assert.Null(restored.ActiveOption);
        AutoPlayInputSearchTrial restoredTrial = Assert.Single(Assert.Single(cold.Controller.State.InputSearch!.Frames, frame => frame.Key == replacementKey).Trials);
        Assert.Equal(replacementTrial.Mask, restoredTrial.Mask);
        Assert.Equal(replacementTrial.SampledQuery, restoredTrial.SampledQuery);
        Assert.True(restoredTrial.Sampled);
        Assert.Empty(restored.CompletedOptions);
        Assert.True(cold.Controller.State.ScriptedChoiceInputs[replacementKey].ObservedQueriesProven);
        Assert.Equal(checkpointBytes, File.ReadAllBytes(Assert.Single(Directory.GetFiles(cold.Output, old.BeforeCheckpoint!, SearchOption.AllDirectories))));
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.PreserveArtifacts = false;
        cold.PreserveArtifacts = false;
    }

    [Theory]
    [InlineData("dangling")]
    [InlineData("unobserved")]
    [InlineData("cycle")]
    public void SupersededInputDomainsRejectInvalidReferencesBeforeLoadingOrChangingNativeState(string invalid)
    {
        GameSession session = GameSession.CreateColdProcess();
        GameState native = session.State;
        string output = Path.Combine(Path.GetTempPath(), "ff8-invalid-input-domain-" + Guid.NewGuid().ToString("N"));
        int worldLookups = 0;
        int restores = 0;
        AutoPlayController controller = new(session, () =>
        {
            worldLookups++;
            throw new InvalidOperationException("Invalid domain metadata must be rejected before native resource access.");
        }, state =>
        {
            restores++;
            session.Restore(state);
        }, output);
        AutoPlayRunState manifest = new() { Snapshot = "must-not-be-opened.json" };
        manifest.Coverage.Choices.Add(new() { Key = "input:600:legacy", SupersededInputDomain = "input:600:replacement" });
        if (invalid != "dangling")
        {
            manifest.Coverage.Choices.Add(new() { Key = "input:600:replacement", SupersededInputDomain = invalid == "cycle" ? "input:600:legacy" : null });
            manifest.ScriptedChoiceInputs.Add("input:600:replacement", new("replacement", [64], [new(0, 2, 4, "KEYSCAN", 64)]) { ObservedQueriesProven = invalid != "unobserved" });
            if (invalid == "cycle")
            {
                manifest.ScriptedChoiceInputs.Add("input:600:legacy", new("legacy", [64], [new(0, 2, 4, "KEYSCAN", 64)]) { ObservedQueriesProven = true });
            }
        }
        Directory.CreateDirectory(output);
        try
        {
            string path = Path.Combine(output, "invalid-domain.run.json");
            File.WriteAllText(path, JsonSerializer.Serialize(manifest, AutoPlayController.JsonOptions));
            Assert.Throws<InvalidDataException>(() => controller.ResumeFrom(path));
            Assert.Same(native, session.State);
            Assert.Equal(0, worldLookups);
            Assert.Equal(0, restores);
            Assert.False(controller.Running);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new AutoPlayController(Session, GetWorld, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-input-preparation-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<(int Field, long Frame, int Mask)> InputCallbacks { get; } = [];
        public List<(ScriptCommand Command, NativeFieldButtonInput Input)> QueryTimeline { get; } = [];
        public GameWorld World => GetWorld();
        public bool PreserveArtifacts { get; set; }

        private GameWorld GetWorld()
        {
            if (world is null)
            {
                world = new GameWorld(Session, gameDirectory, validateWorldPresentation: false);
                GameWorld observed = world;
                observed.Events.CommandExecuted += command =>
                {
                    InputCallbacks.Add((observed.Events.FieldId, observed.Events.Frame, Session.State.FieldButtonInput!.PhysicalHeld));
                    if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" or "KEYOFF" or "MAPJUMP" or "MAPJUMP3" or "MAPJUMPO")
                    {
                        QueryTimeline.Add((command, Session.State.FieldButtonInput!));
                    }
                };
            }
            return world;
        }

        private void Restore(GameState state)
        {
            world?.Dispose();
            world = null;
            Session.Restore(state);
        }

        public void Dispose()
        {
            world?.Dispose();
            if (!PreserveArtifacts && Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
