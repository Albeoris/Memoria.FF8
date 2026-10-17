using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayScriptedInputPreparationTests
{
    [Fact]
    public void ScheduledNativeExitDuringReleaseDoesNotAttemptOrCreditAnotherInput()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayScriptedInputPreparationTests.1");
        byte[] original = File.ReadAllBytes(manifest);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(235, run.Session.State.FieldId);
        string choices = JsonSerializer.Serialize(run.Controller.State.Coverage.Choices);
        string attempts = JsonSerializer.Serialize(run.Controller.State.ScriptedInputs);
        int inputSteps = run.Controller.State.ScriptedInputSteps;
        string[] sceneChoices = run.Controller.State.SceneChoices.ToArray();
        run.Controller.Advance(1);
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(234, run.Session.State.FieldId);
        Assert.Equal(choices, JsonSerializer.Serialize(run.Controller.State.Coverage.Choices));
        Assert.Equal(attempts, JsonSerializer.Serialize(run.Controller.State.ScriptedInputs));
        Assert.Equal(inputSteps, run.Controller.State.ScriptedInputSteps);
        Assert.Equal(sceneChoices, run.Controller.State.SceneChoices);
        Assert.DoesNotContain(run.InputCallbacks, sample => sample.Mask != 0);
        Assert.Equal(0, run.Session.State.FieldButtonInput!.PhysicalHeld);
        Assert.Equal(original, File.ReadAllBytes(manifest));
    }

    [Fact]
    public void NewPreparedCheckpointReplaysWithoutAddingNeutralCallbacksBeforeTheFirstPress()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayAcceptanceTests.5");
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
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
        for (int step = 0; step < 128 && !run.InputCallbacks.Any(sample => sample.Field == 240 && sample.Mask == input.Masks[1]); step++)
        {
            run.Controller.Advance(1);
            Assert.Null(run.Controller.Error);
        }
        Assert.Contains(0, inputChoice.CompletedOptions);
        Assert.Equal(1, inputChoice.ActiveOption);
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

        private GameWorld GetWorld()
        {
            if (world is null)
            {
                world = new GameWorld(Session, gameDirectory, validateWorldPresentation: false);
                GameWorld observed = world;
                observed.Events.CommandExecuted += _ => InputCallbacks.Add((observed.Events.FieldId, observed.Events.Frame, Session.State.FieldButtonInput!.PhysicalHeld));
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
            if (Directory.Exists(Output))
            {
                Directory.Delete(Output, true);
            }
        }
    }
}
