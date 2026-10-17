using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedInputBatchTests
{
    [Fact]
    public void DormantOriginalQueriesAllowNeutralBatchingWithoutSkippingDialogueOrCreditingInput()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayScriptedInputBatchTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        run.World.EnsureField();
        Assert.Equal(896, run.Session.State.FieldId);
        Assert.False(run.World.Events.IsUserControlEnabled);
        Assert.NotNull(run.World.Events.DiscoverScriptedInput());
        string attempts = JsonSerializer.Serialize(run.Controller.State.ScriptedInputs);
        string choices = InputChoices(run.Controller.State);
        int inputSteps = run.Controller.State.ScriptedInputSteps;
        bool batched = false;

        for (int step = 0; step < 20 && !batched && run.Session.State.FieldId == 896 && run.Controller.Error is null; step++)
        {
            GameWorld world = run.World;
            world.EnsureField();
            long beforeFrame = world.Events.Frame;
            bool control = world.Events.IsUserControlEnabled;
            DialogueState? dialogue = world.CurrentDialogue;
            bool probing = !control && dialogue is not { CanConfirm: true } && world.Events.DiscoverScriptedInput() is { Masks.Count: > 0 } && AutoPlayController.HasNeutralScriptedInput(run.Session.State.FieldButtonInput, 896, beforeFrame);
            GameState? referenceState = probing ? run.Session.State with { EventState = world.Events.Capture() } : null;

            run.Controller.Advance(1);

            Assert.True(run.Controller.Error is null, $"{run.Controller.Error}; {run.Controller.ReproductionPath}");
            Assert.Null(run.World.Events.LastFault);
            if (referenceState is not null)
            {
                int callbacks = checked((int)(run.World.Events.Frame - beforeFrame));
                Assert.InRange(callbacks, 1, 300);
                GameSession separateSession = new(referenceState);
                using GameWorld separate = new(separateSession, TestResources.GameDirectory, validateWorldPresentation: false);
                separate.EnsureField();
                Assert.True(separate.Apply(new(PlayerActionKind.FieldButtons, 0)).Accepted);
                List<ScriptCommand> separateQueries = [];
                separate.Events.CommandExecuted += command =>
                {
                    if (IsQuery(command))
                    {
                        separateQueries.Add(command);
                    }
                };
                for (int callback = 0; callback < callbacks; callback++)
                {
                    // Each next callback requires the original owner and unchanged displayed dialogue.
                    Assert.Equal(896, separateSession.State.FieldId);
                    Assert.Equal(SceneKind.Field, separateSession.State.Scene);
                    Assert.Equal(control, separate.Events.IsUserControlEnabled);
                    Assert.Equal(dialogue, separate.CurrentDialogue);
                    separate.AdvanceAutoPlayInputFrame();
                    Assert.Null(separate.Events.LastFault);
                }
                Assert.Empty(separateQueries);
                Assert.Equal(separateSession.State.FieldId, run.Session.State.FieldId);
                Assert.Equal(separateSession.State.Scene, run.Session.State.Scene);
                Assert.True(JsonElement.DeepEquals(separate.Events.Capture(), run.World.Events.Capture()));
                Assert.Equal(separateSession.State.FieldButtonInput, run.Session.State.FieldButtonInput);
                bool boundary = separateSession.State.FieldId != 896 || separateSession.State.Scene != SceneKind.Field || !Equals(dialogue, separate.CurrentDialogue) || control != separate.Events.IsUserControlEnabled;
                Assert.True(callbacks == 300 || boundary, "A neutral batch must reach its budget or stop at its first actual field, scene, dialogue or control boundary.");
                batched = callbacks > 1;
            }
        }

        Assert.True(batched, "The original scene did not expose a multi-callback neutral interval within the bounded continuation.");
        Assert.Empty(run.Queries);
        Assert.Equal(attempts, JsonSerializer.Serialize(run.Controller.State.ScriptedInputs));
        Assert.Equal(choices, InputChoices(run.Controller.State));
        Assert.Equal(inputSteps, run.Controller.State.ScriptedInputSteps);
        // A final native callback may request the next field. Save after its actual allocation has completed.
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
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(run.Controller.ReproductionPath!);
        cold.World.EnsureField();
        Assert.True(JsonElement.DeepEquals(native, cold.World.Events.Capture()));
        Assert.Equal(run.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        Assert.Equal(attempts, JsonSerializer.Serialize(cold.Controller.State.ScriptedInputs));
        Assert.Equal(choices, InputChoices(cold.Controller.State));
        Assert.Equal(inputSteps, cold.Controller.State.ScriptedInputSteps);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
        run.Verified = true;
        cold.Verified = true;
    }

    private static string InputChoices(AutoPlayRunState state)
    {
        return JsonSerializer.Serialize(state.Coverage.Choices.Where(choice => choice.Key.StartsWith("input:", StringComparison.Ordinal)), AutoPlayController.JsonOptions);
    }

    private static bool IsQuery(ScriptCommand command)
    {
        return command.Name is "KEYSCAN" or "KEYSCAN2" or "KEYON" or "KEYOFF";
    }

    private sealed class InstalledRun : IDisposable
    {
        private GameWorld? world;
        private readonly string output = Path.Combine(Path.GetTempPath(), "ff8-scripted-input-batch-" + Guid.NewGuid().ToString("N"));

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
        internal List<ScriptCommand> Queries { get; } = [];
        internal bool Verified { get; set; }

        internal GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
                    world.Events.CommandExecuted += command =>
                    {
                        if (IsQuery(command))
                        {
                            Queries.Add(command);
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
