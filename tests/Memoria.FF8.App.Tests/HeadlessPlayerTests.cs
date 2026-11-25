using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class HeadlessPlayerTests
{
    [Fact]
    public void ExplicitMovieSkipUsesTheInstalledTerminalAndPreservesTheOrdinaryVm()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed movie and field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8-headless-movie-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string source = Path.Combine(temporary, "source.json");
            string actions = Path.Combine(temporary, "actions.json");
            string output = Path.Combine(temporary, "output");
            File.WriteAllText(actions, "[{\"Kind\":\"SkipMovie\",\"Frames\":1}]");
            var session = GameSession.CreateColdProcess();
            session.StartNewGame();
            using var expected = new GameWorld(session, gameDirectory);
            StorySafeMovieSkipTests.ReachCorridorMovie(session, expected);
            session.SetEventState(expected.Events.Capture());
            session.Save(source);
            Assert.True(expected.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
            Assert.Equal(0, HeadlessPlayer.Run(["--state", source, "--game-dir", gameDirectory, "--actions", actions, "--output", output, "--resolution", "320x240"]));
            GameState saved = GameSession.Load(Path.Combine(output, "checkpoint.json"));
            Assert.Equal(SceneKind.Field, saved.Scene);
            Assert.True(saved.MoviePlayback!.Skipped);
            Assert.Equal((325, 325), (saved.MoviePlayback.FrameIndex, saved.MoviePlayback.TerminalFrame));
            Assert.Equal(session.State.MoviePlayback, saved.MoviePlayback);
            Assert.True(JsonElement.DeepEquals(expected.Events.Capture(), saved.EventState!.Value));
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "report.json")));
            Assert.Null(report.RootElement.GetProperty("Failure").GetString());
            Assert.Equal("SkipMovie", report.RootElement.GetProperty("Actions")[0].GetProperty("Kind").GetString());
        }
        finally
        {
            DirectoryInfo resolved = new(temporary);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved.FullName, StringComparison.OrdinalIgnoreCase);
            resolved.Delete(true);
        }
    }

    [Fact]
    public void SavedHardcoreWorldPreparesItsModProfileBeforePlaying()
    {
        string source = TestResources.FixturePath("App.Tests.HeadlessPlayerTests.2");
        byte[] original = File.ReadAllBytes(source);
        GameState initial = GameSession.Load(source);
        Assert.Equal(BattleDifficulty.Hardcore, initial.GameplayPreferences.Difficulty);
        Assert.NotNull(initial.World);

        string actions = TestResources.ArtifactFile("hardcore-world-wait.json");
        string output = TestResources.ArtifactFile("hardcore-world-output");
        File.WriteAllText(actions, """[{"Kind":"Wait","Frames":1}]""");
        Assert.Equal(0, HeadlessPlayer.Run(["--state", source, "--game-dir", TestResources.GameDirectory, "--actions", actions, "--output", output, "--resolution", "320x240"]));

        GameState saved = GameSession.Load(Path.Combine(output, "checkpoint.json"));
        Assert.Equal(BattleDifficulty.Hardcore, saved.GameplayPreferences.Difficulty);
        Assert.Equal(SceneKind.WorldMap, saved.Scene);
        Assert.Equal(initial.FieldId, saved.FieldId);
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "report.json")));
        JsonElement action = Assert.Single(report.RootElement.GetProperty("Results").EnumerateArray());
        Assert.True(action.GetProperty("Accepted").GetBoolean());
        Assert.Equal(1, action.GetProperty("FramesAdvanced").GetInt32());
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("Failure").ValueKind);
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    [Fact]
    public void NaturalFieldTransitionKeepsTheDepartingVmSeparateFromDestinationRestore()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.FF8-headless-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string source = TestResources.FixturePath("App.Tests.HeadlessPlayerTests.1");
            string actions = Path.Combine(temporary, "actions.json");
            string output = Path.Combine(temporary, "output");
            File.WriteAllText(actions, "[{\"Kind\":\"Move\",\"Vertical\":-1,\"Frames\":1}]");
            Assert.Equal(0, HeadlessPlayer.Run(["--state", source, "--game-dir", gameDirectory, "--actions", actions, "--output", output, "--resolution", "320x240"]));
            GameState saved = GameSession.Load(Path.Combine(output, "checkpoint.json"));
            Assert.Equal(139, saved.FieldId);
            Assert.Null(saved.EventState);
            Assert.Equal(232, saved.DepartingFieldState!.Value.GetProperty("FieldId").GetInt32());
            var session = new GameSession(saved);
            using var resumed = new GameWorld(session, gameDirectory);
            Assert.True(resumed.EnsureField());
            Assert.Equal(139, resumed.Events.FieldId);
            Assert.Null(resumed.Events.LastFault);
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "report.json")));
            Assert.Equal(232, report.RootElement.GetProperty("RenderedFieldId").GetInt32());
        }
        finally
        {
            DirectoryInfo resolved = new(temporary);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved.FullName, StringComparison.OrdinalIgnoreCase);
            resolved.Delete(true);
        }
    }
}
