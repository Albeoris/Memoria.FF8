using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GardenConsumedRequestRestoreTests
{
    [Fact]
    public void ActualConsumedRequestRestoresItsOriginalAllocationAndContinuesLowering()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed world corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        GameState state = GameSession.Load(source);
        Assert.Equal(72, state.World!.NativeGardenRuntime!.Allocation.ReplacementFlags264);
        Assert.Equal(8, state.WorldEntryMemory!.ReplacementFlags264);
        GameSession session = new(state);
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.World), JsonSerializer.SerializeToElement(session.State.World)));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(34, session.State.World!.Frame);
        Assert.Equal(11, session.State.World.NativeGardenRuntime!.Transfer!.Counter);
        Assert.Equal(72, session.State.World.NativeGardenRuntime.Allocation.ReplacementFlags264);
        Assert.Equal(8, session.State.WorldEntryMemory!.ReplacementFlags264);
        using GameWorld restored = new(new GameSession(session.State), directory);
        restored.EnsureField();
    }
}
