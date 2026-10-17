using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayObservedInputTests
{
    [Fact]
    public void NativeCodeInputUsesOnlyQueriesReachedByTheNeutralCallbackAndColdPreservesItsChoice()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayObservedInputTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(902, run.Session.State.FieldId);
        Assert.Equal(395, run.World.Events.Frame);
        Assert.False(run.World.Events.IsUserControlEnabled);
        int[] party = run.World.Events.Scene.Party.ToArray();
        Assert.Equal(1, run.World.Events.EventMemory.Read(1024, 1).RequireExact());
        Assert.Equal(0, run.World.Events.EventMemory.Read(1025, 1).RequireExact());
        Assert.Equal(1, run.World.Events.EventMemory.Read(1026, 1).RequireExact());
        Assert.Equal(0, run.World.Events.EventMemory.Read(1027, 1).RequireExact());
        JsonElement beforeRead = run.World.Events.Capture();
        GameState stateBeforeRead = run.Session.State;
        int queryCount = run.Queries.Count;

        ScriptedInputMetadata passive = Assert.IsType<ScriptedInputMetadata>(run.World.Events.DiscoverScriptedInput());

        Assert.Contains(4, passive.Masks);
        Assert.Contains(4096, passive.Masks);
        Assert.Contains(16384, passive.Masks);
        Assert.Equal(queryCount, run.Queries.Count);
        Assert.True(JsonElement.DeepEquals(beforeRead, run.World.Events.Capture()));
        Assert.Same(stateBeforeRead, run.Session.State);
        HashSet<string> existingChoices = run.Controller.State.Coverage.Choices.Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        int completedOptions = run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count);
        AutoPlayChoiceCoverage? selected = null;
        for (int step = 0; step < 16 && selected is null && run.Controller.Error is null; step++)
        {
            run.Controller.Advance(1);
            selected = run.Controller.State.Coverage.Choices.SingleOrDefault(choice => choice.Key.StartsWith("input:902:", StringComparison.Ordinal) && !existingChoices.Contains(choice.Key));
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.NotNull(selected);
        Assert.Equal(902, run.Session.State.FieldId);
        Assert.False(run.World.Events.IsUserControlEnabled);
        Assert.Equal(party, run.World.Events.Scene.Party);
        Assert.All(run.PartySamples, sample => Assert.Equal(party, sample));
        ScriptedInputMetadata scoped = run.Controller.State.ScriptedChoiceInputs[selected.Key];
        Assert.NotEqual(passive.Fingerprint, scoped.Fingerprint);
        Assert.NotEmpty(scoped.Queries);
        Assert.DoesNotContain(4096, scoped.Masks);
        Assert.All(scoped.Masks, mask => Assert.Contains(mask, passive.Masks));
        Assert.DoesNotContain(scoped.Queries, query => query.ObjectId is 9 or 11);
        Assert.Empty(selected.CompletedOptions);
        Assert.False(selected.Finished);
        Assert.Null(selected.ActiveOption);
        AutoPlayInputSearchFrame selectedFrame = Assert.Single(run.Controller.State.InputSearch!.Frames, frame => frame.Key == selected.Key);
        AutoPlayInputSearchTrial selectedTrial = Assert.Single(selectedFrame.Trials);
        Assert.True(selectedTrial.Sampled);
        Assert.NotNull(selectedTrial.SampledQuery);
        Assert.Equal(AutoPlayInputTrialOutcome.Pending, selectedTrial.Outcome);
        Assert.True(selected.InputCheckpointPrepared);
        Assert.Equal(completedOptions, run.Controller.State.Coverage.Choices.Sum(choice => choice.CompletedOptions.Count));
        Assert.DoesNotContain(run.Queries, sample => (sample.Input.PhysicalHeld & 4096) != 0);
        QuerySample firstPhysical = run.Queries.First(sample => sample.Input.PhysicalHeld != 0);
        QuerySample[] neutralWitnesses = run.Queries.Where(sample => sample.Command.FieldId == 902 && sample.Input.PhysicalHeld == 0 && sample.Input.SampledFrame == firstPhysical.Input.SampledFrame).ToArray();
        Assert.NotEmpty(neutralWitnesses);
        Assert.All(scoped.Queries, query => Assert.Contains(neutralWitnesses, sample => Matches(query, sample.Command)));
        Assert.All(run.Queries.Where(sample => sample.Input.PhysicalHeld != 0).GroupBy(sample => (sample.Input.SampledFrame, sample.Input.PhysicalHeld)), callback =>
        {
            Assert.Contains(callback, sample => scoped.Queries.Any(query => Matches(query, sample.Command)) && IsPositiveQuery(sample));
        });
        int physical = scoped.Masks[selectedTrial.Selection];
        Assert.Equal(physical, selectedTrial.Mask);
        Assert.Contains(run.Queries, sample => sample.Input.PhysicalHeld == physical && scoped.Queries.Any(query => Matches(query, sample.Command)) && IsPositiveQuery(sample));
        string beforePath = Assert.Single(Directory.GetFiles(run.Output, selected.BeforeCheckpoint!, SearchOption.AllDirectories));
        GameState checkpoint = GameSession.Load(beforePath);
        long beforeFrame = checkpoint.EventState!.Value.GetProperty("Frame").GetInt64();
        Assert.True(AutoPlayController.HasNeutralScriptedInput(checkpoint.FieldButtonInput, 902, beforeFrame));
        Assert.Equal(beforeFrame + 1, firstPhysical.Input.SampledFrame);
        Assert.True(run.World.HasCurrentFieldAllocation);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        byte[] choiceBytes = File.ReadAllBytes(beforePath);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        AutoPlayChoiceCoverage restored = Assert.Single(cold.Controller.State.Coverage.Choices, choice => choice.Key == selected.Key);
        Assert.Null(restored.ActiveOption);
        AutoPlayInputSearchTrial coldTrial = Assert.Single(Assert.Single(cold.Controller.State.InputSearch!.Frames, frame => frame.Key == selected.Key).Trials);
        Assert.True(coldTrial.Sampled);
        Assert.Equal(selectedTrial.Mask, coldTrial.Mask);
        Assert.Equal(selectedTrial.SampledQuery, coldTrial.SampledQuery);
        Assert.Equal(selectedTrial.Outcome, coldTrial.Outcome);
        Assert.Equal(selected.FirstOption, restored.FirstOption);
        Assert.Equal(selected.OptionCount, restored.OptionCount);
        Assert.True(restored.InputCheckpointPrepared);
        Assert.Empty(restored.CompletedOptions);
        Assert.False(restored.Finished);
        Assert.Equal(JsonSerializer.Serialize(scoped), JsonSerializer.Serialize(cold.Controller.State.ScriptedChoiceInputs[selected.Key]));
        string imported = Assert.Single(Directory.GetFiles(cold.Output, restored.BeforeCheckpoint!, SearchOption.AllDirectories));
        Assert.Equal(choiceBytes, File.ReadAllBytes(imported));
        Assert.Empty(cold.Queries);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private static bool Matches(ScriptedInputQuery query, ScriptCommand command)
    {
        return query.ObjectId == command.ObjectId && query.ScriptId == command.ScriptId && query.Operation == command.Operation && query.Opcode == command.Name && command.Arguments.Count == 1 && query.Mask == command.Arguments[0];
    }

    private static bool IsPositiveQuery(QuerySample sample)
    {
        int value = sample.Command.Name == "KEYSCAN2" ? sample.Input.RawHeld : sample.Command.Name == "KEYON" ? sample.Input.Pressed ?? 0 : sample.Input.Held ?? 0;
        return sample.Command.Arguments.Count == 1 && (sample.Command.Arguments[0] & value) != 0;
    }

    private sealed record QuerySample(ScriptCommand Command, NativeFieldButtonInput Input);

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        internal string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-observed-input-" + Guid.NewGuid().ToString("N"));

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, Output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal AutoPlayController Controller { get; }
        internal List<QuerySample> Queries { get; } = [];
        internal List<int[]> PartySamples { get; } = [];
        internal bool Verified { get; set; }

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.CommandExecuted += command =>
                    {
                        PartySamples.Add(observed.Events.Scene.Party.ToArray());
                        if (command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON")
                        {
                            Queries.Add(new(command, Session.State.FieldButtonInput!));
                        }
                    };
                }
                return world;
            }
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
