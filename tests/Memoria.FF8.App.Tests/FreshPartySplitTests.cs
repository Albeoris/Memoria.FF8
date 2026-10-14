using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FreshPartySplitTests
{
    [Fact]
    public void GameRecordedNewGamePrefixUsesNativePartySplitGeometryAndRestoresEveryStep()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SPLIT_PREFIX") ?? Path.Combine(FindRepository(), "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        Assert.True(File.Exists(source), "The tracked game-recorded New Game prefix is required.");
        GameState state = GameWorld.RecreateCommittedPrefix(GameSession.LoadReplaySource(source), directory);
        var session = new GameSession(state);
        using var world = new GameWorld(session, directory, persistReplayJournal: false);
        world.EnsureField();
        JsonElement begun = world.Events.Capture();
        Assert.True(begun.GetProperty("PartySplit").GetProperty("UsesNativeMovement").GetBoolean());
        Assert.Equal(2, begun.GetProperty("PartySplit").GetProperty("Targets").GetArrayLength());
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Equal(new[] { 0, 5, 255 }, world.Events.Scene.Party);
        using var restored = new GameWorld(new GameSession(state with { EventState = begun, Variables = new Dictionary<int, int>(state.Variables), NativeVariableValues = state.NativeVariableValues is null ? null : new Dictionary<int, NativeMemoryValue>(state.NativeVariableValues) }), directory, persistReplayJournal: false);
        restored.EnsureField();
        long firstFrame = world.Events.Frame;
        for (int tick = 0; tick < 100; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
            var wait = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(wait), restored.Apply(wait));
            Assert.Null(world.Events.LastFault);
            JsonElement current = world.Events.Capture();
            Assert.True(JsonElement.DeepEquals(current, restored.Events.Capture()));
            if (current.GetProperty("PartySplit").ValueKind == JsonValueKind.Null)
            {
                Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
                Console.WriteLine($"Fresh native two-party SPLIT {world.Events.FieldId}/{firstFrame}->{world.Events.Frame}: all raw targets, native wall/contact fans, full VM and CPU pixels restore independently.");
                return;
            }
        }
        Assert.Fail("The original two-party SPLIT must finish under its native geometry contract.");
    }

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("FF8 research repository root was not found.");
    }
}
