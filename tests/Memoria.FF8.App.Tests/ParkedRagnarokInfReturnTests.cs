using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ParkedRagnarokInfReturnTests
{
    [Theory]
    [InlineData(0, -1)]
    [InlineData(18, 0)]
    [InlineData(32, -1)]
    [InlineData(62, -1)]
    public void OriginalFootPacketSelectsSpawnOrSavedPoseWithoutMovingParkedShip(short entry, short extra)
    {
        GameState state = GameSession.Load(RootFile("../fixtures/external-fixture.json"));
        using WorldAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeRagnarokWorldAllocation allocation = assets.ReadParkedRagnarokAllocation(new(entry, 0, extra), state.WorldEntryMemory!, state.EventMemory, state.SavedWorld!, state.CharacterVitals!);
        allocation.Validate();
        Assert.Equal(new WorldTransition(entry, 0, extra), allocation.Packet);
        Assert.Equal(state.SavedWorld!.RagnarokPose2, allocation.ParkedRagnarokPose2);
        NativeSavedWorldPose expected;
        ushort camera;
        if (entry == 62)
        {
            expected = state.SavedWorld.FootReturn!.Pose0;
            camera = state.SavedWorld.FootReturn.CameraYaw96;
        }
        else
        {
            WorldSpawn spawn = assets.Spawns[entry];
            expected = new(spawn.X, spawn.Z, spawn.NativeHeight, (ushort)((spawn.Unknown >> 8) * 16));
            camera = (ushort)((spawn.Unknown & 255) * 16);
        }
        Assert.Equal(expected, allocation.LoadPose);
        Assert.Equal(camera, allocation.CameraYaw);
        NativeRagnarokWorldAllocation restored = JsonSerializer.Deserialize<NativeRagnarokWorldAllocation>(JsonSerializer.Serialize(allocation))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(allocation), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Actual505ExitUsesOriginalSpawnAndIndependentParkedShipThenColdRestores()
    {
        GameSession session = new(GameSession.Load(RootFile("../fixtures/external-fixture.json")));
        NativeSavedWorldPose ship = session.State.SavedWorld!.RagnarokPose2!;
        NativeSavedWorldPose garden = session.State.SavedWorld.GardenPose3!;
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory, validateWorldPresentation: false);
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 0.02942f, Vertical: 0.99957f, Frames: 6)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(new WorldTransition(18, 0, 0), session.State.WorldArrival);
        WorldState current = session.State.World!;
        NativeRagnarokWorldRuntimeState runtime = Assert.IsType<NativeRagnarokWorldRuntimeState>(current.NativeRagnarokRuntime);
        Assert.Null(current.NativeGardenRuntime);
        Assert.NotNull(current.EventUi);
        Assert.Equal(NativeFootWorldRuntimeState.FirstLoadRule, current.NativeFootRuntime!.Rule);
        Assert.Equal(ship, runtime.Allocation.ParkedRagnarokPose2);
        Assert.Equal(new NativeWorldPathPosition(ship.X, ship.Height, -ship.RawZ), runtime.Objects[runtime.Allocation.RagnarokIndex].Position);
        Assert.Equal(new NativeWorldPathPosition(garden.X, garden.Height, -garden.RawZ), runtime.Objects.Single(item => item.ShapeId == 64).Position);
        using WorldAssets assets = new(LaunchOptions.DefaultGameDirectory);
        WorldSpawn spawn = assets.Spawns[18];
        Assert.Equal(new NativeSavedWorldPose(spawn.X, spawn.Z, spawn.NativeHeight, (ushort)((spawn.Unknown >> 8) * 16)), runtime.Allocation.LoadPose);
        Assert.Equal((ushort)((spawn.Unknown & 255) * 16), runtime.Allocation.CameraYaw);
        Assert.NotEqual(ship, runtime.Allocation.LoadPose);
        session.SetDepartingFieldState(world.Events.Capture());
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, LaunchOptions.DefaultGameDirectory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    private static string RootFile(string relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Original standalone field505 boundary is absent.", relative);
    }
}
