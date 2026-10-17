using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayWorldTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(128)]
    [InlineData(65535)]
    public void Original_early_foot_contact_selectors_reject_absent_shapes_without_dense_geometry(int operand)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 17) });
        using WorldMapScene world = new(session, directory);
        GameState before = session.State;
        foreach (ushort opcode in new ushort[] { 0xFF1A, 0xFF1B, 0xFF1C, 0xFF1D })
        {
            Assert.False(world.EvaluateWorldEventPredicate(world.State, 0, session.State.SavedWorld!, opcode, (ushort)operand));
        }
        Assert.Same(before, session.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(94)]
    public void Present_or_unrepresented_appended_shapes_still_require_their_native_contact_owner(int operand)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 17) });
        using WorldMapScene world = new(session, directory);
        GameState before = session.State;
        foreach (ushort opcode in new ushort[] { 0xFF1A, 0xFF1B, 0xFF1C, 0xFF1D })
        {
            Assert.Throws<NotSupportedException>(() => world.EvaluateWorldEventPredicate(world.State, 0, session.State.SavedWorld!, opcode, (ushort)operand));
        }
        Assert.Same(before, session.State);
    }

    [Theory]
    [InlineData(106)]
    [InlineData(136)]
    [InlineData(159)]
    public void Original_early_world_discovers_reachable_native_entrances_and_executes_same_callback_after_snapshot(int destinationField)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, directory);
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 17) });
        using WorldMapScene world = new(session, directory);
        IReadOnlyList<WorldAutoPlayTarget> targets = world.DiscoverAutoPlayEntrances();
        WorldAutoPlayTarget city = Assert.Single(targets, item => item.DestinationField == 106);
        WorldAutoPlayTarget cavern = Assert.Single(targets, item => item.DestinationField == 136);
        WorldAutoPlayTarget destination = Assert.Single(targets, item => item.DestinationField == destinationField);
        Assert.True(city.Order < cavern.Order);
        Assert.Equal(targets, world.DiscoverAutoPlayEntrances());
        world.DebugTeleportToEntrance(destination);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        WorldFieldSelection? actual = null;
        WorldFieldSelection? repeated = null;
        world.PrepareFieldEntry = (entry, _, _, _) => () => actual = entry;
        replay.PrepareFieldEntry = (entry, _, _, _) => () => repeated = entry;
        PlayerAction wait = new(PlayerActionKind.Wait, Frames: 24);
        PlayerActionResult entered = world.Apply(wait);
        Assert.Equal(PlayerTriggerKind.FieldTransition, entered.Trigger.Kind);
        Assert.Equal(destinationField, entered.Trigger.FieldId);
        Assert.Equal(entered, replay.Apply(wait));
        Assert.Equal(actual!.Entry, repeated!.Entry);
        Assert.Equal(destinationField, actual.Entry.FieldId);
        WorldState departure = world.LastDeparture!;
        Assert.Equal((destination.X, destination.RawZ), (departure.X, departure.Z));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(departure), JsonSerializer.SerializeToElement(replay.LastDeparture)));
    }
}
