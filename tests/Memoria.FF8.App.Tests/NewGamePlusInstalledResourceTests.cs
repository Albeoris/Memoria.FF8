using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusInstalledResourceTests
{
    [Fact]
    public void OriginalTerminalContinuationCreatesAnIndependentCompletedSourceAndFreshRun()
    {
        string? snapshot = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_ENDING_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(snapshot) && File.Exists(snapshot), "Set MEMORIA_FF8_TEST_ENDING_SNAPSHOT to an authorized standalone ending checkpoint.");
        string gameDirectory = TestResources.GameDirectory;
        using GameAssets assets = new(gameDirectory);
        GameSession session = new(GameSession.Load(snapshot!, assets.ReadNewGameLimitProgress));
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(76, session.State.FieldId);
        Assert.Null(session.State.Ff8Completion);
        uint[] experience = session.State.CharacterVitals!.Records.Select(record => record.Experience).ToArray();
        NativeItemStock[] stock = session.State.Economy!.Items.ToArray();
        using (GameWorld world = new(session, gameDirectory, sharedAssets: assets))
        {
            world.EnsureField();
            Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Null(world.Events.LastFault);
            Ff8CompletionState completion = Assert.IsType<Ff8CompletionState>(session.State.Ff8Completion);
            completion.Validate();
            Assert.Equal(Ff8CompletionState.OriginalEndingProgramHash, completion.ProgramHash);
            Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Same(completion, session.State.Ff8Completion);
        }
        Assert.Equal(experience, session.State.CharacterVitals!.Records.Select(record => record.Experience));
        Assert.Equal(stock, session.State.Economy!.Items);
        GameplaySaveComponentDocument completed = CompletedGameData.Capture(session.State);
        string directory = Path.Combine(Path.GetTempPath(), "Memoria-installed-clear-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PlayerSaveStore store = new(Path.Combine(directory, "diagnostic.json"), directory, discoverOriginalSaves: false, loadMemoria: path => GameplaySaveSessionLoader.Load(path, gameDirectory, sharedAssets: assets));
            store.SaveCompleted(1, completed);
            Assert.Single(store.ReadCompletedEntries());
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_COMPLETED_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Assert.True(Path.IsPathFullyQualified(output), "The completed output must be an explicitly configured absolute path.");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                string staging = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.Copy(store.GetPath(1), staging);
                    using (FileStream validation = File.OpenRead(staging))
                    {
                        _ = GameplaySaveCodec.Read(validation);
                    }
                    File.Move(staging, output);
                }
                finally
                {
                    File.Delete(staging);
                }
            }
            GameState transfer = store.Load(1);
            Assert.NotNull(transfer.CompletedGame);
            Assert.Null(transfer.EventState);
            GameSession cold = GameWorld.CreateColdProcess(assets);
            cold.StartNewGame();
            using (GameWorld allocation = new(cold, gameDirectory, sharedAssets: assets))
            {
                // Allocate resource definitions without executing opening scripts.
            }
            NewGamePlusDraft draft = new(transfer, cold.State, new());
            GameState started = draft.ApplyTo(cold.State);
            Assert.Equal(cold.State.FieldId, started.FieldId);
            Assert.Equal(74, started.FieldId);
            Assert.Same(cold.State.EventMemory, started.EventMemory);
            Assert.Null(started.EventState);
            Assert.NotNull(started.NewGamePlus);
            Assert.True(started.NewGamePlus.StartingChoicesApplied);
            Assert.Equal(completed.Characters.GuardianForces[0].Experience, started.CharacterVitals!.GuardianForces[0].Experience);
            Assert.Equal(stock, started.Economy!.Items);
            _ = new GameSession(started);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
