using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTransportActivityTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(38)]
    [InlineData(40)]
    public void NativeCarExitLockRetainsTheCarAndPublishesCompletedFalseActivity(byte entity)
    {
        GameSession session = CreateCar(entity, 129);
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldState before = scene.State;
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeCarRuntime);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Equal((before.X, before.Z), (scene.State.X, scene.State.Z));
        NativeWorldSalaryState salary = NativeWorldSalaryState.Begin(session.State);
        Assert.Equal(false, scene.CompleteNativeTransportActivity(scene.State, salary, true, false));
        Assert.Null(scene.CompleteNativeTransportActivity(scene.State, salary, false, false));
        Assert.Equal(true, scene.CompleteNativeTransportActivity(scene.State, salary, true, true));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        foreach (PlayerAction action in new PlayerAction[] { new(PlayerActionKind.Wait), new(PlayerActionKind.WorldButtons, 0x40) })
        {
            Assert.Equal(scene.Apply(action), replay.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    [Fact]
    public void RefusedCarReboardingUsesItsActualBroadContactAndKeepsFootControl()
    {
        GameSession session = CreateCar(38, 1);
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        using WorldAssets assets = new(TestResources.GameDirectory);
        const int x = -21894;
        const int z = -41257;
        WorldGroundSelection ground = assets.LookupNativeGround(x, z, WorldTerrainSelection.Replacements(scene.State.ReplacementMask), [])!;
        Assert.NotNull(ground);
        int height = NativeWorldTerrain.ResolveHeight(38, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, scene.State.NativeHeight).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        WorldState vehicle = scene.State;
        session.SetWorldState(vehicle with { X = x, Z = z, NativeHeight = height, BodyYaw = 3008, CameraYaw = 3046, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = ground.Ground.Triangle.GroundType, NativeCarRuntime = vehicle.NativeCarRuntime! with { GroundCache = cache, GroundYaw = 0 } });
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        NativeFootWorldRuntimeState foot = scene.State.NativeFootRuntime!;
        NativeWorldParkedCar parked = foot.ParkedCar!;
        int currentGround = scene.State.NativeHeight - (scene.State.GroundType <= 5 ? 320 : 0);
        //Declared refusal component: outside54B860's strict height gate but
        //inside548CD0's padded vertical interaction range.
        parked = parked with { Position = parked.Position with { Y = currentGround + 250 } };
        session.SetWorldState(scene.State with { NativeFootRuntime = foot with { ParkedCar = parked } });
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeFootRuntime);
        Assert.Null(scene.State.NativeCarRuntime);
        Assert.Equal(parked.Position, scene.State.NativeFootRuntime!.ParkedCar!.Position);
        Assert.Equal(false, scene.CompleteNativeTransportActivity(scene.State, NativeWorldSalaryState.Begin(session.State), true, false));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        Assert.Null(replay.CompleteNativeTransportActivity(replay.State, NativeWorldSalaryState.Begin(restored.State), true, false));
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), replay.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
    }

    private static GameSession CreateCar(byte entity, byte flags265)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, 300).WriteBytes(264, 1, 8).WriteBytes(265, 1, flags265));
        session.SetSavedWorldState(session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(256) });
        WorldTransition packet = new(3, entity, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        return session;
    }
}
