using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayLadderInputTests
{
    [Fact]
    public void AnAutomaticLadderOwnerCannotAcquireSyntheticHeldInput()
    {
        using InstalledRun run = new();
        run.Controller.ResumeFrom(TestResources.FixturePath("App.Tests.AutoPlayLadderInputTests.2"));
        run.World.EnsureField();
        ActorState player = Assert.IsType<ActorState>(run.World.Events.PlayerActor);
        // A negative ownership descriptor cannot replace the actual controlled owner.
        NativeLadderState automatic = player.NativeLadder! with { AutomaticCompletion = true };
        JsonElement before = run.World.Events.Capture();
        string input = JsonSerializer.Serialize(run.Session.State.FieldButtonInput);
        System.Reflection.MethodInfo drive = typeof(AutoPlayController).GetMethod("TryDriveOwnedLadderInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        Assert.False(Assert.IsType<bool>(drive.Invoke(run.Controller, [run.World, player, automatic])));
        Assert.True(JsonElement.DeepEquals(before, run.World.Events.Capture()));
        Assert.Equal(input, JsonSerializer.Serialize(run.Session.State.FieldButtonInput));
    }

    [Fact]
    public void ControlledNativeLadderUsesRealHeldInputAndColdResumePreservesSamplesAndContinuation()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayLadderInputTests.2");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun first = new();
        using InstalledRun uninterrupted = new();
        first.Controller.ResumeFrom(manifest);
        uninterrupted.Controller.ResumeFrom(manifest);
        first.World.EnsureField();
        uninterrupted.World.EnsureField();
        ActorState player = Assert.IsType<ActorState>(first.World.Events.PlayerActor);
        NativeLadderState initial = Assert.IsType<NativeLadderState>(player.NativeLadder);
        Assert.True(initial.Active);
        Assert.True(initial.Up);
        Assert.Equal((ushort)1, player.NativeMovementCompletion542);
        Assert.Equal((short)0, player.NativeTurnAccumulator474);
        long frame = first.World.Events.Frame;
        string choices = JsonSerializer.Serialize(first.Controller.State.Coverage.Choices);
        first.Controller.Advance(1);
        uninterrupted.Controller.Advance(1);
        Assert.Null(first.Controller.Error);
        Assert.Null(uninterrupted.Controller.Error);
        Assert.Equal(frame + 16, first.World.Events.Frame);
        Assert.Equal((short)16, player.NativeTurnAccumulator474);
        Assert.Equal(initial.FiberSequence, player.NativeLadder!.FiberSequence);
        Assert.True(player.NativeLadder.FixedZ > initial.FixedZ);
        Assert.Equal((ushort)0x1000, first.Session.State.FieldButtonInput!.Held);
        Assert.Equal((ushort)0, first.Session.State.FieldButtonInput.PhysicalHeld);
        Assert.Equal(choices, JsonSerializer.Serialize(first.Controller.State.Coverage.Choices));
        Assert.True(JsonElement.DeepEquals(first.World.Events.Capture(), uninterrupted.World.Events.Capture()));
        first.Controller.SaveAndStop();
        string resume = first.Controller.ReproductionPath!;
        byte[] resumeBytes = File.ReadAllBytes(resume);
        using InstalledRun cold = new();
        cold.Controller.ResumeFrom(resume);
        cold.Controller.Advance(1);
        uninterrupted.Controller.Advance(1);
        Assert.Null(cold.Controller.Error);
        Assert.Null(uninterrupted.Controller.Error);
        Assert.Equal((short)32, cold.World.Events.PlayerActor!.NativeTurnAccumulator474);
        Assert.Equal((ushort)0, cold.Session.State.FieldButtonInput!.PhysicalHeld);
        Assert.Equal(uninterrupted.Session.State.FieldButtonInput, cold.Session.State.FieldButtonInput);
        Assert.True(JsonElement.DeepEquals(uninterrupted.World.Events.Capture(), cold.World.Events.Capture()));
        cold.Controller.SaveAndStop();
        uninterrupted.Controller.SaveAndStop();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(cold.ReadSavedState()), JsonSerializer.SerializeToElement(uninterrupted.ReadSavedState())));
        Assert.Equal(resumeBytes, File.ReadAllBytes(resume));
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    [Fact]
    public void OriginalLadderChoiceRunsItsNativeMovementAndReturnsControlWithoutTeleportingTheOwnedBody()
    {
        const string choiceKey = "734:223:6:41:602:3:1:3";
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayLadderInputTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(734, run.Session.State.FieldId);
        for (int tick = 0; tick < 512 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
            if (run.LadderCompleted && run.Controller.State.Coverage.Choices.SingleOrDefault(choice => choice.Key == choiceKey)?.CompletedOptions.Contains(1) == true)
            {
                break;
            }
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Contains(run.Outcomes, outcome => outcome is { FieldId: 734, ObjectId: 6, ScriptId: 41, Operation: 602, Choice: 1 });
        Assert.True(run.LadderCompleted);
        Assert.Contains(1, run.Controller.State.Coverage.Choices.Single(choice => choice.Key == choiceKey).CompletedOptions);
        Assert.Contains(run.Actions, action => action.Action is { Kind: PlayerActionKind.FieldButtons, Value: 0x1000 });
        Assert.Equal((ushort)0, run.Session.State.FieldButtonInput!.PhysicalHeld);
        Assert.Equal(new[] { 8, 9, 10 }, run.World.Events.Scene.Party);
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private readonly List<GameWorld> observedWorlds = [];
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-ladder-input-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<DialogueOutcome> Outcomes { get; } = [];
        public IEnumerable<PlayerActionObservation> Actions => observedWorlds.SelectMany(item => item.Events.ActionLog);
        public bool LadderCompleted { get; private set; }

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    observedWorlds.Add(world);
                    GameWorld observed = world;
                    world.Events.DialogueCompleted += Outcomes.Add;
                    world.Events.CommandExecuted += command =>
                    {
                        if (command is { FieldId: 734, ObjectId: 10, ScriptId: 68, Operation: 828, Name: "LADDERUP2" } && observed.Events.Actors[10] is { NativeMovementCompletion542: 2, NativeLadder.Active: false })
                        {
                            LadderCompleted = true;
                        }
                    };
                }
                return world;
            }
        }

        public GameState ReadSavedState()
        {
            string manifest = Controller.ReproductionPath!;
            AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(File.ReadAllBytes(manifest), AutoPlayController.JsonOptions)!;
            return GameSession.Load(Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!));
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
