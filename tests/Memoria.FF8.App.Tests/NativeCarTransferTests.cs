using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarTransferTests
{
    [Fact]
    public void CarDisembarkRetainsItsParkedBodyAcrossIndependentRestoreAndReboarding()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original car/world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, 300).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1));
        // Declare the native car tutorial as already shown, keeping ordinary control active.
        session.SetSavedWorldState(session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256) });
        WorldTransition packet = new(3, 35, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        // Declared terrain component position with a permitted roadside foot placement.
        // The entry point itself is a driving spawn and need not permit disembark.
        using WorldAssets worldAssets = new(directory);
        const int x = -21894;
        const int z = -41257;
        WorldState spawn = scene.State;
        WorldGroundSelection ground = worldAssets.LookupNativeGround(x, z, WorldTerrainSelection.Replacements(spawn.ReplacementMask), [])!;
        Assert.NotNull(ground);
        uint flags = ground.Ground.Triangle.GroundType | ((uint)ground.Ground.Triangle.TextureFlags << 8) | ((uint)ground.Ground.Triangle.VertexFlags << 16);
        Assert.True(NativeWorldTerrain.CanDisembark(flags, 35));
        int height = NativeWorldTerrain.ResolveHeight(35, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, spawn.NativeHeight).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        session.SetWorldState(spawn with { X = x, Z = z, NativeHeight = height, BodyYaw = 3008, CameraYaw = 3046, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = ground.Ground.Triangle.GroundType, NativeCarRuntime = spawn.NativeCarRuntime! with { GroundCache = cache, GroundYaw = 0 } });
        WorldState vehicle = scene.State;
        Assert.Equal((byte)0, vehicle.EventUi!.ScriptMode);
        Assert.True(vehicle.Frame >= 24, $"Frame={vehicle.Frame}, script={vehicle.EventUi?.ScriptMode}, input={session.WorldProcess?.InputHistory}");
        WorldState historical = vehicle with { NativeCarRuntime = vehicle.NativeCarRuntime! with { Forest = null } };
        session.SetWorldState(historical);
        Assert.Throws<NotSupportedException>(() => scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)));
        Assert.Same(historical, session.State.World);
        session.SetWorldState(vehicle);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.Null(scene.State.NativeCarRuntime);
        NativeWorldParkedCar parked = Assert.IsType<NativeWorldParkedCar>(scene.State.NativeFootRuntime!.ParkedCar);
        Assert.Equal(new NativeWorldPathPosition(vehicle.X, vehicle.NativeHeight, -vehicle.Z), parked.Position);
        Assert.Equal((byte)35, parked.Car.EntityType);
        Assert.NotEqual((vehicle.X, vehicle.Z), (scene.State.X, scene.State.Z));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, WorldButtons: 0x40), new(PlayerActionKind.Wait), new(PlayerActionKind.Wait, WorldButtons: 0x40) })
        {
            Assert.Equal(scene.Apply(action), replay.Apply(action));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        }
        Assert.NotNull(scene.State.NativeCarRuntime);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((parked.Position.X, -parked.Position.Z), (scene.State.X, scene.State.Z));
        NativeWorldForestState retained = scene.State.NativeCarRuntime.Forest!;
        Assert.NotNull(retained);
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.WorldButtons, 0), new(PlayerActionKind.WorldButtons, 0x40) })
        {
            WorldState before = scene.State;
            retained = retained.AdvanceCar(before.GroundType!.Value, 35, new(before.X, before.NativeHeight, -before.Z), 0, checked((short)before.BodyYaw), 0, session.State.VibrationProcess!.GraphicsSpeed20409F0, worldAssets.ForestTables.Random, worldAssets.ForestTables.AllEffects);
            Assert.Equal(scene.Apply(action), replay.Apply(action));
        }
        Assert.NotNull(scene.State.NativeFootRuntime);
        Assert.True(NativeWorldForestState.ContentEquals(retained, scene.State.NativeFootRuntime.Forest));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait, WorldButtons: 2), new(PlayerActionKind.Wait, Frames: 48) })
        {
            Assert.Equal(scene.Apply(action), replay.Apply(action));
        }
        Assert.Equal((byte)1, scene.State.NativeFootRuntime!.AnimationVariant);
        Assert.Equal(640, scene.State.NativeFootRuntime.CameraTransition!.ProjectionDistance);
        Assert.Equal((short)-512, scene.State.NativeFootRuntime.Forest!.CameraPitch);
        Assert.Equal(new NativeSavedWorldPose(parked.Position.X, -parked.Position.Z, checked((short)parked.Position.Y), checked((ushort)parked.BodyYaw)), session.State.SavedWorld!.Slot4.Pose);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x40)), replay.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x40)));
        Assert.NotNull(scene.State.NativeCarRuntime);
        Assert.Equal(648, scene.State.NativeCarRuntime.CameraTransition!.ProjectionDistance);
        Assert.Equal((short)-496, scene.State.NativeCarRuntime.Forest!.CameraPitch);
        Assert.Equal(640, scene.State.NativeCarRuntime.ProjectionCamera!.ProjectionDistance);
        GameSession transientSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene transientReplay = new(transientSession, directory);
        PlayerAction settle = new(PlayerActionKind.Wait, Frames: 48);
        Assert.Equal(scene.Apply(settle), transientReplay.Apply(settle));
        Assert.Equal(1024, scene.State.NativeCarRuntime!.CameraTransition!.ProjectionDistance);
        Assert.Equal(0, scene.State.NativeCarRuntime.CameraTransition.Flags);
        Assert.Equal((short)-112, scene.State.NativeCarRuntime.Forest!.CameraPitch);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(transientSession.State)));
    }
}
