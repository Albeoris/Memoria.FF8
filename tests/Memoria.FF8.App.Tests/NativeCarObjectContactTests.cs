using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarObjectContactTests
{
    [Theory]
    [InlineData(32, 61, 455, 0, true, true)]
    [InlineData(32, 61, 455, 300, false, true)]
    [InlineData(32, 61, 455, 600, false, false)]
    [InlineData(35, 3, 300, 0, true, true)]
    [InlineData(35, 3, 300, 300, false, true)]
    [InlineData(35, 3, 300, 600, false, false)]
    public void AllocatedStaticCarsBlockHardContactButOnlyReportPaddedProximity(byte entityType, short entry, ushort story, int offsetX, bool hardContact, bool broadContact)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original car/world resources are required.");
        using GameAssets assets = new(directory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
        session.SetEconomy(session.Economy!.AddItem(162, 1, out _));
        // Declared component inputs through canonical loader owners. These local
        // placements exercise original allocations and terrain, rather than a gameplay save.
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, story).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1));
        WorldTransition packet = new(entry, entityType, -1);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(session.State, directory, packet);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        using WorldAssets worldAssets = new(directory);
        WorldState initial = scene.State;
        NativeCarWorldRuntimeState initialCar = initial.NativeCarRuntime!;
        NativeTrainWorldAllocation allocation = initialCar.Allocation!;
        NativeTrainWorldObject target = allocation.Objects.First(item => item.ShapeId == 75);
        int contactIndex = allocation.Objects.IndexOf(target);
        Assert.NotEqual(allocation.ControlledObjectIndex, contactIndex);
        Assert.Null(initialCar.PrimaryContact);
        Assert.Null(initialCar.BroadContact);
        JsonElement oldSnapshot = JsonSerializer.SerializeToElement(initialCar);
        Assert.False(oldSnapshot.TryGetProperty(nameof(initialCar.PrimaryContact), out _));
        Assert.False(oldSnapshot.TryGetProperty(nameof(initialCar.BroadContact), out _));
        NativeCarWorldRuntimeState oldCar = oldSnapshot.Deserialize<NativeCarWorldRuntimeState>()!;
        oldCar.Validate();
        Assert.Null(oldCar.PrimaryContact);
        Assert.Null(oldCar.BroadContact);
        Assert.Throws<InvalidDataException>(() => (initialCar with { PrimaryContact = -1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (initialCar with { PrimaryContact = -2, BroadContact = -2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (initialCar with { PrimaryContact = -1, BroadContact = allocation.Objects.Length }).Validate());
        Assert.Throws<InvalidDataException>(() => (initialCar with { PrimaryContact = contactIndex, BroadContact = -1 }).Validate());
        (initialCar with { PrimaryContact = -1, BroadContact = -1 }).Validate();

        int x = target.Pose.X + offsetX;
        int z = target.Pose.RawZ;
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(initial.ReplacementMask);
        WorldGroundSelection ground = worldAssets.LookupNativeGround(x, z, replacements, [])!;
        Assert.NotNull(ground);
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        int height = NativeWorldTerrain.ResolveHeight(entityType, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, target.Pose.Height).Height;
        session.SetWorldState(initial with { X = x, Z = z, NativeHeight = height, BodyYaw = 1024, CameraYaw = 900, CameraTargetX = x + 128, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, GroundType = ground.Ground.Triangle.GroundType, NativeCarRuntime = initialCar with { GroundCache = cache, GroundYaw = 0, Speed = 16, CameraFollowSpeed = 0 } });
        WorldState before = scene.State;
        PlayerAction accelerateAndSteer = new(PlayerActionKind.Move, Horizontal: 1, Vertical: -1);
        Assert.Equal(1, scene.Apply(accelerateAndSteer).FramesAdvanced);
        NativeCarWorldRuntimeState car = scene.State.NativeCarRuntime!;
        Assert.Equal(hardContact ? contactIndex : -1, car.PrimaryContact);
        Assert.Equal(broadContact ? contactIndex : -1, car.BroadContact);
        Assert.Equal(18, car.Speed);
        Assert.Equal(1055, scene.State.BodyYaw);
        Assert.Equal(901, scene.State.CameraYaw);
        Assert.Equal(8, car.CameraFollowSpeed);
        Assert.Equal(x + 64, scene.State.CameraTargetX);
        Assert.Equal((before.X, before.Z, before.NativeHeight), (car.ProjectionCamera!.X, car.ProjectionCamera.RawZ, car.ProjectionCamera.NativeHeight));
        if (hardContact)
        {
            Assert.Equal((before.X, before.Z, before.NativeHeight), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
            Assert.Equal(0, car.MovementMagnitude20409F4);
        }
        else
        {
            Assert.NotEqual((before.X, before.Z), (scene.State.X, scene.State.Z));
            Assert.Equal(18, car.MovementMagnitude20409F4);
        }
        Assert.Equal(hardContact, scene.EvaluateWorldEventPredicate(scene.State, 0, session.State.SavedWorld!, 0xFF1A, 75));
        Assert.Equal(hardContact, scene.EvaluateWorldEventPredicate(scene.State, 0, session.State.SavedWorld!, 0xFF1B, 75));
        Assert.False(scene.EvaluateWorldEventPredicate(scene.State, 0, session.State.SavedWorld!, 0xFF1A, 82));
        int targetYaw = NativeWorldScriptedPathMath.NativeAngle(-(target.Position.Z + scene.State.Z), target.Position.X - scene.State.X);
        WorldState facingTarget = scene.State with { BodyYaw = (targetYaw % 4096 + 4096) % 4096 };
        Assert.Equal(broadContact, scene.EvaluateWorldEventPredicate(facingTarget, 0, session.State.SavedWorld!, 0xFF1C, 75));
        Assert.Equal(broadContact, scene.EvaluateWorldEventPredicate(facingTarget, 0, session.State.SavedWorld!, 0xFF1D, 75));
        Assert.False(scene.EvaluateWorldEventPredicate(facingTarget, 0, session.State.SavedWorld!, 0xFF1C, 82));
        car.Validate();

        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, directory);
        Assert.Equal(scene.Apply(accelerateAndSteer), replay.Apply(accelerateAndSteer));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));

        // Enter the full map through the ordinary Select edge after its loader delay.
        // Its callback advances the motor while retaining the preceding contact queries.
        session.SetWorldState(scene.State with { Frame = Math.Max(24, scene.State.Frame), NativeCarRuntime = scene.State.NativeCarRuntime! with { MapDisplayMode = 2 } });
        WorldState beforeMap = scene.State;
        Assert.Equal(1, scene.Apply(new PlayerAction(PlayerActionKind.WorldButtons, 0x180)).FramesAdvanced);
        Assert.Equal((byte)3, WorldMapScene.MapDisplayMode(scene.State));
        Assert.Equal((byte)12, scene.State.EventUi!.ScriptMode);
        Assert.Equal((beforeMap.X, beforeMap.Z, beforeMap.NativeHeight), (scene.State.X, scene.State.Z, scene.State.NativeHeight));
        Assert.Equal(beforeMap.NativeCarRuntime!.PrimaryContact, scene.State.NativeCarRuntime!.PrimaryContact);
        Assert.Equal(beforeMap.NativeCarRuntime.BroadContact, scene.State.NativeCarRuntime.BroadContact);
        Assert.Equal(beforeMap.NativeCarRuntime.MovementMagnitude20409F4, scene.State.NativeCarRuntime.MovementMagnitude20409F4);
        Assert.Equal(beforeMap.NativeCarRuntime.Speed + 2, scene.State.NativeCarRuntime.Speed);
    }
}
