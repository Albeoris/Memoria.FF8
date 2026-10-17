using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCanonicalConditionTests
{
    [Fact]
    public void EqualFrontierWardrobeAlternativesKeepTheActualPersistentConditionChangingOutcome()
    {
        const string choiceKey = "241:155:12:53:420:3:1:2";
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayCanonicalConditionTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(241, run.Session.State.FieldId);
        Assert.Equal(2, run.World.Events.EventMemory.KnownValues[720]);
        AutoPlayChoiceCoverage? choice = null;
        for (int tick = 0; tick < 128 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
            choice = run.Controller.State.Coverage.Choices.SingleOrDefault(item => item.Key == choiceKey);
            if (choice is { Finished: true } && run.Controller.State.PrimaryContinuation is null)
            {
                break;
            }
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.NotNull(choice);
        Assert.True(choice.Finished);
        Assert.Equal(new[] { 1, 2 }, choice.CompletedOptions.Order());
        Assert.Null(choice.ActiveOption);
        Assert.Null(run.Controller.State.PrimaryContinuation);
        Assert.Equal(241, run.Session.State.FieldId);
        run.World.EnsureField();
        Assert.Equal(0, run.World.Events.EventMemory.KnownValues[720]);
        byte squallRow = run.Session.CharacterVitals!.FindPhysicalRow(0);
        Assert.InRange(squallRow, (byte)0, (byte)7);
        Assert.Equal((byte)0, run.Session.CharacterAppearance!.Records[squallRow].Dress);
        ActorState player = Assert.IsType<ActorState>(run.World.Events.PlayerActor);
        Assert.True(player.Active);
        Assert.Equal(0, player.CharacterId);
        Assert.True(run.World.Events.IsUserControlEnabled);
        DialogueOutcome[] nativeChoices = run.DialogueOutcomes.Where(outcome => outcome is { FieldId: 241, ObjectId: 12, ScriptId: 53, Operation: 420, Choice: not null }).ToArray();
        Assert.Contains(nativeChoices, outcome => outcome.Choice == 1);
        Assert.Contains(nativeChoices, outcome => outcome.Choice == 2);
        Assert.Contains(run.Commands, command => command is { FieldId: 241, ObjectId: 12, ScriptId: 53, Name: "SETDRESS" } && command.Arguments.Count == 2 && command.Arguments[0] == 0 && command.Arguments[1] == 0);
        Assert.False(run.Controller.State.ReachedEnding);
        Assert.Equal(originalManifest, File.ReadAllBytes(manifest));
        Assert.Equal(originalSnapshot, File.ReadAllBytes(snapshot));
    }

    private sealed class InstalledRun : IDisposable
    {
        private readonly string gameDirectory = TestResources.GameDirectory;
        private GameWorld? world;

        public InstalledRun()
        {
            Controller = new(Session, () => World, Restore, Output);
        }

        public GameSession Session { get; } = GameSession.CreateColdProcess();
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-canonical-condition-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<DialogueOutcome> DialogueOutcomes { get; } = [];
        public List<ScriptCommand> Commands { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    world.Events.CommandExecuted += Commands.Add;
                    world.Events.DialogueCompleted += DialogueOutcomes.Add;
                }
                return world;
            }
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
