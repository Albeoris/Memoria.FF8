using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRagnarokSavedCarTests
{
    [Fact]
    public void LandingPreservesItsSavedCarAndCarFootTransfersRestoreTheSameNextCallback()
    {
        string directory = TestResources.GameDirectory;
        using WorldAssets assets = new(directory);
        GameSession session = NativeRagnarokFootTests.Fixture();
        const int carX = -21894;
        const int carZ = -41257;
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(0);
        WorldGroundSelection carGround = assets.LookupNativeGround(carX, carZ, replacements, [])!;
        short carHeight = checked((short)NativeWorldTerrain.ResolveHeight(38, -(int)carGround.Ground.Height, carGround.Ground.Triangle.GroundType, 0).Height);
        NativeSavedWorldVehicleSlot slot = new(85, new(carX, carZ, carHeight, 3008));
        //The declared transfer component has already shown the car tutorial,
        //as in the ordinary car transfer tests; the message is a separate interaction.
        session.Restore(session.State with { CharacterVitals = session.State.CharacterVitals!.AcquireGuardianForce(13), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(265, 1, 1), SavedWorld = session.State.SavedWorld! with { Slot4 = slot, Slot5 = new(86, new(12345, -23456, -300, 512)), WorldEventFlags116 = session.State.SavedWorld.WorldEventFlags116!.WithOperand(256) } });
        using WorldMapScene scene = new(session, directory);
        Assert.Equal(slot, scene.State.NativeRagnarokRuntime!.Allocation.SavedCarSlot);
        NativeRagnarokFootTests.Land(scene);
        NativeRagnarokWorldRuntimeState landed = scene.State.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject ship = landed.Objects[landed.Allocation.RagnarokIndex];
        NativeRagnarokWorldObject parked = landed.Objects[landed.Allocation.SavedCarIndex];
        Assert.Equal(new NativeWorldPathPosition(carX, carHeight, -carZ), parked.Position);
        // Declared roadside component inputs exercise the original transfer after
        //the real61-callback landing, without fabricating a travel checkpoint.
        const int footX = carX + 256;
        WorldGroundSelection selected = assets.LookupNativeGround(footX, carZ, WorldTerrainSelection.Replacements(scene.State.ReplacementMask), [])!;
        int height = NativeWorldTerrain.ResolveHeight(0, -(int)selected.Ground.Height, selected.Ground.Triangle.GroundType, 0).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(selected.Identity) with { Alternatives = [new(0, [], selected.Identity, selected.Identity) { FailedSelections = 0 }] };
        WorldState foot = scene.State;
        session.SetWorldState(foot with { X = footX, Z = carZ, NativeHeight = height, CameraYaw = 3046, CameraTargetX = footX, CameraTargetZ = carZ, CameraTargetNativeHeight = height - 256, GroundType = selected.Ground.Triangle.GroundType, NativeFootRuntime = foot.NativeFootRuntime! with { GroundCache = cache, GroundYaw = 0, CameraTransition = new(0, 1024) }, NativeRagnarokRuntime = landed with { GroundCache = cache, GroundYaw = 0, CurrentGroundHeight = checked((short)(height - (selected.Ground.Triangle.GroundType <= 5 ? 320 : 0))) } });
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        GameSession restored = new(Clone(session.State));
        using WorldMapScene replay = new(restored, directory);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Confirm)), replay.Apply(new(PlayerActionKind.Confirm)));
        NativeCarWorldRuntimeState car = Assert.IsType<NativeCarWorldRuntimeState>(scene.State.NativeCarRuntime);
        Assert.Equal((byte)38, car.EntityType);
        Assert.Null(scene.State.NativeRagnarokRuntime);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(ship, car.RagnarokObjects!.Objects[car.RagnarokObjects.Allocation.RagnarokIndex]);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        GameSession drivenRestore = new(Clone(session.State));
        using WorldMapScene drivenReplay = new(drivenRestore, directory);
        PlayerAction release = new(PlayerActionKind.Wait);
        Assert.Equal(scene.Apply(release), drivenReplay.Apply(release));
        for (int callback = 0; callback < 61 && session.State.World?.NativeCarRuntime?.CameraTransition is { Flags: not 0 }; callback++)
        {
            Assert.True(session.State.Scene == SceneKind.WorldMap && session.State.World is not null, TransferBoundary(session));
            Assert.Equal((byte)0, scene.State.EventUi!.ScriptMode);
            Assert.Equal(scene.Apply(release), drivenReplay.Apply(release));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(drivenRestore.State)));
        }
        Assert.True(session.State.Scene == SceneKind.WorldMap && session.State.World?.NativeCarRuntime is not null, TransferBoundary(session));
        Assert.True(scene.State.NativeCarRuntime!.CameraTransition is null or { Flags: 0 }, TransferBoundary(session));
        Assert.Equal((byte)0, scene.State.EventUi!.ScriptMode);
        PlayerAction exit = new(PlayerActionKind.WorldButtons, 0x40);
        Assert.Equal(scene.Apply(exit), drivenReplay.Apply(exit));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(drivenRestore.State)));
        Assert.True(session.State.Scene == SceneKind.WorldMap && session.State.World?.NativeFootRuntime is not null, TransferBoundary(session));
        NativeFootWorldRuntimeState exitedFoot = Assert.IsType<NativeFootWorldRuntimeState>(scene.State.NativeFootRuntime);
        NativeWorldParkedCar exitedCar = Assert.IsType<NativeWorldParkedCar>(exitedFoot.ParkedCar);
        Assert.NotNull(exitedCar.Car.RagnarokObjects);
        Assert.Null(scene.State.NativeCarRuntime);
        Assert.Equal(ship, exitedCar.Car.RagnarokObjects!.Objects[landed.Allocation.RagnarokIndex]);
        GameSession walkingRestore = new(Clone(session.State));
        using WorldMapScene walkingReplay = new(walkingRestore, directory);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), walkingReplay.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(walkingRestore.State)));
        Assert.Equal(scene.Render(320, 224), walkingReplay.Render(320, 224));
        WorldGroundSelection shipGround = assets.LookupNativeGround(ship.Position.X, -ship.Position.Z, WorldTerrainSelection.Replacements(scene.State.ReplacementMask), [])!;
        int shipFootHeight = NativeWorldTerrain.ResolveHeight(0, -(int)shipGround.Ground.Height, shipGround.Ground.Triangle.GroundType, 0).Height;
        NativeWorldGroundCacheState shipCache = NativeWorldGroundCacheState.CreateFresh(shipGround.Identity) with { Alternatives = [new(0, [], shipGround.Identity, shipGround.Identity) { FailedSelections = 0 }] };
        WorldState walking = scene.State;
        session.SetWorldState(walking with { X = ship.Position.X, Z = -ship.Position.Z, NativeHeight = shipFootHeight, CameraYaw = ship.Yaw & 4095, CameraTargetX = ship.Position.X, CameraTargetZ = -ship.Position.Z, CameraTargetNativeHeight = shipFootHeight - 256, GroundType = shipGround.Ground.Triangle.GroundType, NativeFootRuntime = walking.NativeFootRuntime! with { GroundCache = shipCache, GroundYaw = 0, CameraTransition = new(0, 1024) } });
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        GameSession shipRestore = new(Clone(session.State));
        using WorldMapScene shipReplay = new(shipRestore, directory);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Confirm)), shipReplay.Apply(new(PlayerActionKind.Confirm)));
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal(NativeRagnarokTransferKind.Takeoff, scene.State.NativeRagnarokRuntime!.Transfer!.Kind);
        Assert.Equal(1, scene.State.NativeRagnarokRuntime.Transfer.Counter);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait, Frames: 60)), shipReplay.Apply(new(PlayerActionKind.Wait, Frames: 60)));
        Assert.Null(scene.State.NativeRagnarokRuntime.Transfer);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(shipRestore.State)));
        Assert.Equal((byte)85, scene.State.NativeRagnarokRuntime.Objects[landed.Allocation.SavedCarIndex].ShapeId);
    }

    private static string TransferBoundary(GameSession session)
    {
        WorldState? world = session.State.World;
        return $"Scene={session.State.Scene}, field={session.State.FieldId}, frame={world?.Frame}, car={world?.NativeCarRuntime?.EntityType}, foot={world?.NativeFootRuntime?.EntityType}, cameraFlags={world?.NativeCarRuntime?.CameraTransition?.Flags}, scriptMode={world?.EventUi?.ScriptMode}, windows={world?.EventUi?.Windows.Length}.";
    }

    private static GameState Clone(GameState source)
    {
        return JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source))!;
    }
}
