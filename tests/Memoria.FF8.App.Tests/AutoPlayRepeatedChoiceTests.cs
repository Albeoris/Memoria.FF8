using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayRepeatedChoiceTests
{
    private const string OuterKey = "893:235:2:27:380:15:2:3";
    private const string InnerKey = "893:235:2:27:400:16:2:5";

    [Fact]
    public void RepeatedNativeExplanationsAdvanceToTheNextVisibleChoiceAndReturnControl()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayRepeatedChoiceTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(896, run.Session.State.FieldId);
        Assert.Equal(418, run.World.Events.Frame);
        Assert.Equal(new[] { 0, 255, 255 }, run.World.Events.Scene.Party);
        Assert.Equal(3, run.Controller.State.DialogueSelections[OuterKey]);
        Assert.Equal(5, run.Controller.State.DialogueSelections[InnerKey]);
        Assert.Equal(3, Choice(run, OuterKey).ActiveOption);
        Assert.Equal(2, Choice(run, InnerKey).ActiveOption);
        int prefixLength = run.Controller.State.ActiveSelections!.Count;

        for (int step = 0; step < 200 && !CompletedAtControlReturn(run) && run.Controller.Error is null; step++)
        {
            run.Controller.Advance(1);
        }

        Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
        Assert.Null(run.World.Events.LastFault);
        Assert.True(run.SawOuterChoiceTwo);
        Assert.True(run.SawControlReturn);
        Assert.Contains(run.Outcomes, outcome => outcome is { FieldId: 893, ObjectId: 2, ScriptId: 27, Operation: 380, MessageId: 15, Choice: 2 });
        Assert.NotEmpty(run.SelectionsAtOuterChoiceTwo);
        Assert.Contains(new AutoPlayChoiceSelection(OuterKey, 2), run.SelectionsAtOuterChoiceTwo.Skip(prefixLength));
        Assert.DoesNotContain(run.Outcomes, outcome => outcome is { FieldId: 893, Operation: 380, Choice: 3 });
        Assert.Contains(3, Choice(run, OuterKey).CompletedOptions);
        Assert.Contains(2, Choice(run, InnerKey).CompletedOptions);
        Assert.NotEmpty(run.InitialBriefingPartySamples);
        Assert.All(run.InitialBriefingPartySamples, party => Assert.Equal(new[] { 0, 255, 255 }, party));

        // Choice exploration can load its next authentic checkpoint immediately after control returns.
        for (int callback = 0; callback < 8; callback++)
        {
            run.World.EnsureField();
            if (run.World.HasCurrentFieldAllocation && run.World.Events.Scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed })
            {
                break;
            }
            Assert.True(run.World.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(run.World.Events.LastFault);
        }
        Assert.True(run.World.HasCurrentFieldAllocation);
        Assert.Equal(run.Session.State.FieldId, run.World.Events.FieldId);
        Assert.Equal(NativeFieldInitializationStage.Completed, run.World.Events.Scene.NativeFieldInitialization!.Stage);
        run.Controller.SaveAndStop();
        JsonElement native = run.World.Events.Capture();
        List<AutoPlayChoiceCoverage> expectedChoices = JsonSerializer.Deserialize<List<AutoPlayChoiceCoverage>>(JsonSerializer.Serialize(run.Controller.State.Coverage.Choices, AutoPlayController.JsonOptions), AutoPlayController.JsonOptions)!;
        string checkpointDirectory = Path.GetDirectoryName(run.Controller.ReproductionPath!)!;
        Dictionary<string, byte[]> imports = new(StringComparer.OrdinalIgnoreCase);
        string? ImportedCheckpoint(string? checkpoint)
        {
            if (checkpoint is null)
            {
                return null;
            }
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(checkpoint, checkpointDirectory));
            string filename = "import-" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".json";
            imports.TryAdd(filename, bytes);
            return filename;
        }
        // Resume imports policy and unfinished checkpoints by content, retiring finished ordinary references.
        foreach (AutoPlayChoiceCoverage choice in expectedChoices)
        {
            foreach (string scope in choice.PolicyCheckpoints.Keys.ToArray())
            {
                AutoPlayPolicyChoiceCheckpoint checkpoint = choice.PolicyCheckpoints[scope];
                choice.PolicyCheckpoints[scope] = checkpoint with { BeforeCheckpoint = ImportedCheckpoint(checkpoint.BeforeCheckpoint), ContinuationCheckpoint = ImportedCheckpoint(checkpoint.ContinuationCheckpoint) };
            }
            if (choice.Finished)
            {
                choice.BeforeCheckpoint = null;
                choice.ContinuationCheckpoint = null;
            }
            else
            {
                choice.BeforeCheckpoint = ImportedCheckpoint(choice.BeforeCheckpoint);
                choice.ContinuationCheckpoint = ImportedCheckpoint(choice.ContinuationCheckpoint);
            }
        }
        string choices = JsonSerializer.Serialize(expectedChoices, AutoPlayController.JsonOptions);
        string selections = JsonSerializer.Serialize(run.Controller.State.ActiveSelections, AutoPlayController.JsonOptions);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        foreach ((string filename, byte[] bytes) in imports)
        {
            string imported = Assert.Single(Directory.GetFiles(cold.Output, filename, SearchOption.AllDirectories));
            Assert.Equal(bytes, File.ReadAllBytes(imported));
        }
        Assert.Equal(choices, JsonSerializer.Serialize(cold.Controller.State.Coverage.Choices, AutoPlayController.JsonOptions));
        Assert.Equal(selections, JsonSerializer.Serialize(cold.Controller.State.ActiveSelections, AutoPlayController.JsonOptions));
        Assert.Equal(run.Controller.State.DialogueSelections, cold.Controller.State.DialogueSelections);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private static bool CompletedAtControlReturn(InstalledRun run)
    {
        return run.SawControlReturn && Choice(run, OuterKey).CompletedOptions.Contains(3) && Choice(run, InnerKey).CompletedOptions.Contains(2);
    }

    private static AutoPlayChoiceCoverage Choice(InstalledRun run, string key)
    {
        return run.Controller.State.Coverage.Choices.Single(choice => choice.Key == key);
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private bool leftInitialBriefing;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-repeated-choice-" + Guid.NewGuid().ToString("N"));

        internal InstalledRun()
        {
            Controller = new(Session, () => World, state =>
            {
                world?.Dispose();
                world = null;
                Session.Restore(state);
            }, output);
        }

        internal GameSession Session { get; } = GameSession.CreateColdProcess();
        internal AutoPlayController Controller { get; }
        internal string Output => output;
        internal List<DialogueOutcome> Outcomes { get; } = [];
        internal List<int[]> InitialBriefingPartySamples { get; } = [];
        internal AutoPlayChoiceSelection[] SelectionsAtOuterChoiceTwo { get; private set; } = [];
        internal bool SawOuterChoiceTwo { get; private set; }
        internal bool SawControlReturn { get; private set; }
        internal bool Verified { get; set; }

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    observed.Events.DialogueCompleted += outcome =>
                    {
                        Outcomes.Add(outcome);
                        if (outcome is { FieldId: 893, ObjectId: 2, ScriptId: 27, Operation: 380, MessageId: 15, Choice: 2 })
                        {
                            SawOuterChoiceTwo = true;
                            SelectionsAtOuterChoiceTwo = Controller.State.ActiveSelections!.ToArray();
                        }
                    };
                    observed.Events.OperationCompleted += () =>
                    {
                        if (SawOuterChoiceTwo && observed.HasCurrentFieldAllocation && observed.Events.FieldId == 893 && observed.Events.IsUserControlEnabled)
                        {
                            SawControlReturn = true;
                        }
                    };
                    observed.Events.CommandExecuted += command =>
                    {
                        leftInitialBriefing |= command.FieldId != 896 || Session.State.FieldId != 896;
                        if (!leftInitialBriefing)
                        {
                            InitialBriefingPartySamples.Add(observed.Events.Scene.Party.ToArray());
                        }
                    };
                }
                return world;
            }
        }

        public void Dispose()
        {
            world?.Dispose();
            if (Verified && Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
