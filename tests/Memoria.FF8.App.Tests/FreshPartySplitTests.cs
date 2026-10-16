using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FreshPartySplitTests
{
    [Fact]
    public void GameRecordedNewGamePrefixUsesNativePartySplitGeometryAndRestoresEveryStep()
    {
        string directory = TestResources.GameDirectory;
        string source = Environment.GetEnvironmentVariable("MEMORIA_FF8_SPLIT_PREFIX") ?? Path.Combine(FindRepository(), "../fixtures/external-fixture.json");
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        Assert.True(File.Exists(source), "The tracked game-recorded New Game prefix is required.");
        GameState state = GameSession.Load(source);
        var session = new GameSession(state);
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        JsonElement begun = world.Events.Capture();
        Assert.Equal(2, begun.GetProperty("PartySplit").GetProperty("Targets").GetArrayLength());
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Equal(new[] { 0, 5, 255 }, world.Events.Scene.Party);
        using var restored = new GameWorld(new GameSession(state with { EventState = begun, EventMemory = state.EventMemory}), directory);
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
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("FF8 research repository root was not found.");
    }
}
