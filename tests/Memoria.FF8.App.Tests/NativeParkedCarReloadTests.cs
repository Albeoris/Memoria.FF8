using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeParkedCarReloadTests
{
    [Theory]
    [InlineData(35)]
    [InlineData(38)]
    public void SavedFootEntryRestoresParkedCarPoseAndReboardsAfterIndependentRestore(int entity)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using GameAssets assets = new(directory);
        using WorldAssets worldAssets = new(directory);
        GameSession cold = GameSession.CreateColdProcess();
        cold.StartNewGame();
        cold.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        cold.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        cold.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        cold.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        cold.SetWorldEntryMemory(cold.WorldEntryMemory!.WriteBytes(256, 2, 300).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1));
        cold.SetSavedWorldState(cold.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256) });
        WorldTransition packet = new(3, checked((short)entity), -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(cold.State, directory, packet);
        cold.CommitPreparedWorldEntry(packet, ticket.InitialState, cold.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene driving = ticket.Adopt(cold);
        Assert.True(driving.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        const int x = -21894;
        const int z = -41257;
        WorldGroundSelection ground = worldAssets.LookupNativeGround(x, z, WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(8, 0)), [])!;
        Assert.NotNull(ground);
        int height = NativeWorldTerrain.ResolveHeight(entity, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, 0).Height;
        NativeSavedWorldPose carPose = new(x, z, checked((short)height), 3008);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        //Declared roadside component position; actual54A7F0 validates and places the foot body.
        WorldState vehicle = driving.State;
        cold.SetWorldState(vehicle with { X = x, Z = z, NativeHeight = height, BodyYaw = 3008, CameraYaw = 3046, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = ground.Ground.Triangle.GroundType, NativeCarRuntime = vehicle.NativeCarRuntime! with { GroundCache = cache, GroundYaw = 0 } });
        Assert.True(driving.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        NativeWorldParkedCar departureCar = Assert.IsType<NativeWorldParkedCar>(driving.State.NativeFootRuntime!.ParkedCar);
        NativeSavedWorldFootReturn footReturn = NativeSavedWorldFootReturn.RecordFootDeparture(driving.State, 317, 300, 0);
        NativeSavedWorldState saved = cold.State.SavedWorld!.WithCarParking(departureCar).WithFootReturn(footReturn);
        //The field serializes these saved owners; a new entry62 executes fresh world loading.
        GameSession session = new(cold.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(62, 0, -1), World = null, SavedWorld = saved });
        using WorldMapScene scene = new(session, directory);
        NativeWorldParkedCar parked = Assert.IsType<NativeWorldParkedCar>(scene.State.NativeFootRuntime!.ParkedCar);
        Assert.Equal(NativeFootWorldRuntimeState.SavedReloadRule, scene.State.NativeFootRuntime.Rule);
        Assert.True(parked.LoadedFromSavedSlot);
        Assert.Equal(new NativeWorldPathPosition(x, height, -z), parked.Position);
        Assert.Equal((short)3008, parked.BodyYaw);
        Assert.Equal(carPose, session.State.SavedWorld!.Slot4.Pose);
        Assert.NotEmpty(scene.Render(160, 120));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, Frames: 24), new(PlayerActionKind.WorldButtons, 0x40) })
        {
            Assert.Equal(scene.Apply(action), replay.Apply(action));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        }
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((byte)entity, scene.State.NativeCarRuntime!.EntityType);
        Assert.Equal((x, z), (scene.State.X, scene.State.Z));
        Assert.Equal(new WorldTransition(62, 0, -1), session.State.WorldArrival);
        NativeWorldCarBoardingReceipt boarding = Assert.IsType<NativeWorldCarBoardingReceipt>(scene.State.NativeCarRuntime.Boarding);
        Assert.Equal(session.State.WorldArrival, boarding.OriginalPacket);
        GameState boarded = session.State;
        Assert.Throws<InvalidDataException>(() => new GameSession(boarded with { World = scene.State with { NativeCarRuntime = scene.State.NativeCarRuntime with { Boarding = null } } }));
        Assert.Throws<InvalidDataException>(() => new GameSession(boarded with { World = scene.State with { NativeCarRuntime = scene.State.NativeCarRuntime with { Boarding = boarding with { OriginalPacket = new(62, 1, -1) } } } }));
        Assert.Throws<InvalidDataException>(() => new GameSession(boarded with { World = scene.State with { NativeCarRuntime = scene.State.NativeCarRuntime with { Boarding = boarding with { SourceWorldFrame = scene.State.Frame + 1 } } } }));
        GameSession boardedRestore = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(boarded))!);
        using WorldMapScene boardedReplay = new(boardedRestore, directory);
        PlayerAction coast = new(PlayerActionKind.Wait, Frames: 8);
        Assert.Equal(scene.Apply(coast), boardedReplay.Apply(coast));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(boardedRestore.State)));
        Assert.Equal(scene.Render(160, 120), boardedReplay.Render(160, 120));
    }
}
