using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldGardenCameraSceneTests
{
    [Fact]
    public void FreshOriginalGardenToggleManualYawAndSaveReceiptRestoreTheSameNextCallback()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeGardenRuntime!.AnimationVariant);
        Assert.Equal((byte)1, session.State.SavedWorld!.GardenReturn!.AnimationVariant108);
        int yaw = scene.State.CameraYaw;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 8, CameraHorizontal: -1, Frames: 4)).Accepted);
        Assert.NotEqual(yaw, scene.State.CameraYaw);
        Assert.Equal(32, scene.State.NativeGardenRuntime.ManualCameraVelocity);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Wait, CameraHorizontal: -1);
        Assert.True(scene.Apply(next).Accepted);
        Assert.True(replay.Apply(next).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.Equal((byte)0, scene.State.NativeGardenRuntime.AnimationVariant);
    }

    [Fact]
    public void ActualGardenDismountFootViewTransitionsAndRestoresBesideItsParkedAllocation()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeGardenRuntime!.Transfer);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 31)).Accepted);
        Assert.NotNull(scene.State.NativeFootRuntime);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 2)).Accepted);
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.AnimationVariant);
        Assert.True(scene.State.NativeFootRuntime!.CameraTransition!.ProjectionDistance < 1024);
        Assert.Equal(scene.State.NativeFootRuntime!.Forest!.CameraPitch, scene.State.NativeGardenRuntime.CameraPitch);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Wait, CameraHorizontal: 1);
        Assert.True(scene.Apply(next).Accepted);
        Assert.True(replay.Apply(next).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    private static GameSession Fixture()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, TestResources.GameDirectory);
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory!.WriteBytes(256, 2, 455).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1);
        session.SetWorldEntryMemory(entry);
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256).WithOperand(260) } });
        WorldTransition packet = new(61, 32, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, TestResources.GameDirectory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using (WorldMapScene precedingCar = ticket.Adopt(session))
        {
            Assert.True(precedingCar.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
            Assert.NotEmpty(precedingCar.Render(320, 224));
            WorldState precedingTerrainOwner = precedingCar.State;
            NativeSavedWorldCarReturn retiredCar = NativeSavedWorldCarReturn.Create(precedingTerrainOwner, checked((ushort)session.State.FieldId), 455, 0);
            NativeSavedWorldState saved = session.State.SavedWorld!.WithCarReturn(retiredCar);
            session.Restore(session.State with { SavedWorld = saved, World = null, WorldArrival = new(12, 48, -1), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(264, 1, 0).WriteBytes(265, 1, 0), DepartingWorldState = precedingTerrainOwner });
        }
        return session;
    }
}
