using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BalambDoorCameraTests
{
    [Fact]
    public void SuppliedBalambSaveRendersOrdinaryMovementAndRestoresNextInput()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        Assert.NotNull(repository);
        string source = Path.Combine(repository.FullName, "../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(102, world.Events.FieldId);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        int acceptedMoves = 0;
        FieldFixedPosition? initialPosition = world.Events.Scene.NativePlayerPosition;
        bool moved = false;
        for (int tick = 0; tick < 40; tick++)
        {
            PlayerAction action = new(PlayerActionKind.Move, Horizontal: tick < 20 ? -1 : 1, Vertical: tick < 20 ? -1 : 1);
            if (world.Apply(action).Accepted)
            {
                acceptedMoves++;
            }
            world.EnsureField();
            moved |= world.Events.Scene.NativePlayerPosition != initialPosition;
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
            Assert.Null(world.Events.LastFault);
        }
        Assert.True(acceptedMoves > 0, "The supplied save must continue through ordinary movement.");
        Assert.True(moved, "At least one ordinary input must change the native player position.");
        using GameWorld restored = new(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        restored.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }
}
