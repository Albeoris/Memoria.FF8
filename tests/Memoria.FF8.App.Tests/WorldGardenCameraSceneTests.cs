using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class WorldGardenCameraSceneTests
{
    [Fact]
    public void DormantChocoboAllocationsDoNotInterruptGardenRendering()
    {
        GameSession session = Fixture(46);
        session.SetEventMemory(session.State.EventMemory.Write(1536, 1, NativeScalarValue.Exact(1)));
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        NativeGardenWorldRuntimeState garden = scene.State.NativeGardenRuntime!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(garden.ProjectionCamera!);
        Assert.Equal(new byte[] { 2, 3 }, garden.Objects.Where(item => item.EntityType == 49).Select(item => item.ShapeId));
        Assert.True(projection.IsNear(default));
        Assert.True(projection.ProjectNode(default) is { Visible: true });

        Assert.True(scene.IsPresentationDeferred);
        Assert.Equal(0, garden.BackgroundRails!.Callback);
        Assert.Throws<NativeWorldPresentationDeferredException>(() => scene.Render(160, 112));
        PlayerActionResult first = scene.Apply(new(PlayerActionKind.Wait));
        Assert.True(first.Accepted);
        Assert.Equal(1, first.FramesAdvanced);
        Assert.Equal(1, scene.State.NativeGardenRuntime!.BackgroundRails!.Callback);
        Assert.False(scene.IsPresentationDeferred);
        Assert.NotEmpty(scene.Render(160, 112));
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 5)).Accepted);
        Assert.NotEmpty(scene.Render(160, 112, interpolation: 0.5f));
    }

    [Fact]
    public void FreshOriginalGardenToggleManualYawAndSaveReceiptRestoreTheSameNextCallback()
    {
        GameSession session = Fixture();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.True(scene.State.NativeGardenRuntime!.AnimationVariant == 1, $"Garden script{scene.State.EventUi!.ScriptMode} flags{session.State.SavedWorld!.WorldEventFlags116!.Value:X16}");
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

    [Fact]
    public void GardenDismountAfterBattleFootReloadKeepsTheOriginalEntryPacket()
    {
        GameSession source = Fixture();
        using WorldMapScene initial = new(source, TestResources.GameDirectory);
        Assert.True(initial.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(initial.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.True(initial.Apply(new(PlayerActionKind.Wait, Frames: 31)).Accepted);
        Assert.NotNull(initial.State.NativeFootRuntime);

        WorldState departure = initial.State;
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordBattleDeparture(departure, checked((ushort)source.State.FieldId), 750, initial.PlaceId);
        NativeSavedWorldState saved = GameSession.PrepareGardenParkingForFootReturn(source.State.SavedWorld!, departure).WithFootReturn(receipt);
        WorldTransition entry = new(61, 32, -1);
        GameSession session = new(source.State with { WorldArrival = entry, World = null, DepartingWorldState = departure, SavedWorld = saved });
        using WorldMapScene scene = new(session, TestResources.GameDirectory, battleReload: receipt);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        for (int callback = 0; callback < 48 && scene.State.NativeGardenRuntime!.BroadContact != scene.State.NativeGardenRuntime.Allocation.Garden64Index; callback++)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Move, Vertical: 1)).Accepted);
        }
        Assert.Equal(scene.State.NativeGardenRuntime!.Allocation.Garden64Index, scene.State.NativeGardenRuntime.BroadContact);
        Assert.True(scene.Apply(new(PlayerActionKind.Confirm)).Accepted);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 64)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeGardenRuntime.Transfer);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 31)).Accepted);
        Assert.Equal(NativeFootWorldRuntimeState.GardenDismountRule, scene.State.NativeFootRuntime!.Rule);
        Assert.Equal(entry, session.State.WorldArrival);
        Assert.Equal(receipt, scene.State.NativeGardenRuntime.Allocation.BattleReload);

        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.True(scene.Apply(next).Accepted);
        Assert.True(replay.Apply(next).Accepted);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    internal static GameSession Fixture(short gardenSpawn = 41)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld initialization = new(session, TestResources.GameDirectory);
        NativeWorldEntryMemory entry = session.State.WorldEntryMemory!.WriteBytes(256, 2, 455).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1);
        session.SetWorldEntryMemory(entry);
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256).WithOperand(258).WithOperand(261) } });
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
            session.Restore(session.State with { SavedWorld = saved, World = null, WorldArrival = new(gardenSpawn, 48, -1), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 750).WriteBytes(264, 1, 0).WriteBytes(265, 1, 0), DepartingWorldState = precedingTerrainOwner });
        }
        return session;
    }
}
