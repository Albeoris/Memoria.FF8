using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayRemoteVisitControllerTests
{
    [Fact]
    public void LegacyRemoteCoverageIsRefreshedByAnOrdinaryReturnBeforeTheNativeDeparture()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayRemoteVisitControllerTests.1");
        byte[] originalManifest = File.ReadAllBytes(manifest);
        AutoPlayRunState saved = JsonSerializer.Deserialize<AutoPlayRunState>(originalManifest, AutoPlayController.JsonOptions)!;
        Assert.Empty(saved.VisitGuards);
        string snapshot = Path.Combine(Path.GetDirectoryName(manifest)!, saved.Snapshot!);
        byte[] originalSnapshot = File.ReadAllBytes(snapshot);
        using InstalledRun run = new();
        run.Controller.ResumeFrom(manifest);
        Assert.Equal(241, run.Session.State.FieldId);
        Assert.Equal(2, run.World.Events.EventMemory.KnownValues[720]);
        Assert.Empty(run.Controller.State.VisitGuards);
        List<int> visitedFields = [run.Session.State.FieldId];
        for (int tick = 0; tick < 128 && run.Session.State.FieldId != 221 && run.Controller.Error is null; tick++)
        {
            run.Controller.Advance(1);
            visitedFields.Add(run.Session.State.FieldId);
        }
        Assert.Null(run.Controller.Error);
        Assert.True(run.Controller.Running);
        Assert.Equal(221, run.Session.State.FieldId);
        Assert.Contains(run.Controller.State.VisitGuards.Keys, key => key.StartsWith("241:145:nav:", StringComparison.Ordinal));
        Assert.Contains(run.Controller.State.VisitGuards.Keys, key => key.StartsWith("243:145:nav:", StringComparison.Ordinal));
        Assert.Contains(243, visitedFields);
        Assert.Equal(243, visitedFields.First(field => field != 241));
        Assert.Contains(run.Commands, command => command is { FieldId: 243, ObjectId: 13, ScriptId: 63, Name: "REQEW" });
        Assert.Contains(run.Commands, command => command is { FieldId: 243, ObjectId: 7, ScriptId: 32, Name: "AMESW" } && command.Arguments.Count > 1 && command.Arguments[1] == 10);
        Assert.Contains(run.Commands, command => command is { FieldId: 243, ObjectId: 7, ScriptId: 32, Name: "AMESW" } && command.Arguments.Count > 1 && command.Arguments[1] == 11);
        Assert.Contains(run.Commands, command => command is { FieldId: 243, Name: "MAPJUMPO" } && command.Arguments[0] == 221);
        Assert.NotEmpty(run.UniformSamples);
        Assert.All(run.UniformSamples, value => Assert.Equal(2, value));
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
        public string Output { get; } = Path.Combine(Path.GetTempPath(), "ff8-autoplay-remote-visit-" + Guid.NewGuid().ToString("N"));
        public AutoPlayController Controller { get; }
        public List<ScriptCommand> Commands { get; } = [];
        public List<int> UniformSamples { get; } = [];

        public GameWorld World
        {
            get
            {
                if (world is null)
                {
                    world = new(Session, gameDirectory, validateWorldPresentation: false);
                    GameWorld observed = world;
                    world.Events.CommandExecuted += command =>
                    {
                        Commands.Add(command);
                        UniformSamples.Add(observed.Events.EventMemory.KnownValues[720]);
                    };
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
