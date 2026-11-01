using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class WorldGardenCameraSceneTests
{
    [Fact]
    public void GardenFootBoardsSavedCarRestoresExitsAndReboardsTheSameGarden()
    {
        GameSession session = Fixture();
        using WorldAssets assets = new(TestResources.GameDirectory);
        const int carX = -21894;
        const int carZ = -41257;
        IReadOnlyDictionary<int, int> replacements = WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(0, 0));
        WorldGroundSelection carGround = assets.LookupNativeGround(carX, carZ, replacements, [])!;
        Assert.NotNull(carGround);
        int carHeight = NativeWorldTerrain.ResolveHeight(38, -(int)carGround.Ground.Height, carGround.Ground.Triangle.GroundType, 0).Height;
        //Declared parked-car component; the following boarding and exit use the actual native gates.
        session.Restore(session.State with { SavedWorld = session.State.SavedWorld! with { Slot4 = new(85, new(carX, carZ, checked((short)carHeight), 0)), CarReturn = null } });
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.NotNull(scene.State.NativeFootRuntime);
        WorldState originalFoot = scene.State;
        NativeGardenWorldRuntimeState originalGarden = scene.State.NativeGardenRuntime!;
        PlaceGardenFoot(session, assets, carX + 200, carZ);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        AssertGardenCarBoardingGates(session, assets);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeCarRuntime);
        Assert.Null(scene.State.NativeGardenRuntime);
        Assert.Equal((byte)38, scene.State.NativeCarRuntime!.EntityType);
        Assert.Equal(originalGarden.Objects[originalGarden.Allocation.Garden64Index], scene.State.NativeCarRuntime.GardenObjects!.Objects[originalGarden.Allocation.Garden64Index]);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene replay = new(restored, TestResources.GameDirectory);
        PlayerAction settle = new(PlayerActionKind.Wait, Frames: 48);
        Assert.Equal(scene.Apply(settle), replay.Apply(settle));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(scene.Render(160, 120), replay.Render(160, 120));
        Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 0, Vertical: -1, Frames: 4)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 16)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 2)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        Assert.Equal(new NativeSavedWorldPose(scene.State.X, scene.State.Z, checked((short)scene.State.NativeHeight), checked((ushort)scene.State.BodyYaw)), session.State.SavedWorld!.Slot4.Pose);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.NotNull(scene.State.NativeFootRuntime?.ParkedCar);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 48)).Accepted);
        PlaceGardenFoot(session, assets, originalFoot.X, originalFoot.Z);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x40)).Accepted);
        Assert.Null(scene.State.NativeFootRuntime);
        Assert.Null(scene.State.NativeCarRuntime);
        Assert.NotNull(scene.State.NativeGardenRuntime);
        Assert.Equal(originalGarden.Objects[originalGarden.Allocation.Garden64Index].Position, scene.State.NativeGardenRuntime!.Objects[originalGarden.Allocation.Garden64Index].Position);
        Assert.Equal((byte)85, session.State.SavedWorld!.Slot4.ShapeId);
        GameSession gardenRestore = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene gardenReplay = new(gardenRestore, TestResources.GameDirectory);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), gardenReplay.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(gardenRestore.State)));
    }

    private static void AssertGardenCarBoardingGates(GameSession session, WorldAssets assets)
    {
        WorldState state = session.State.World!;
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime!;
        NativeGardenWorldObject car = garden.Objects[garden.Allocation.SavedCarIndex];
        Assert.Equal((byte)0, state.EventUi!.ScriptMode);
        Assert.True(state.Frame >= 24, "Entry hold must have completed.");
        Assert.True(foot.CameraTransition is null or { Flags: 0 }, $"Foot camera is still transferring: {foot.CameraTransition}.");
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(NativeWorldFootCameraState.Create(state));
        NativeWorldPathWords player = context.RelativeWords(new(state.X, state.NativeHeight, -state.Z));
        NativeWorldPathWords anchor = context.ProjectNode(car.Position)!.Words;
        NativeWorldContactShape shape = assets.ReadNativeContactShape(car.ShapeId);
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(shape, unchecked((short)(car.RotationY - 1024)), player, anchor, 200, 32), $"Car proximity failed: shape={shape}, player={player}, car={anchor}.");
        foreach (NativeWorldGroundAlternative alternative in foot.GroundCache!.Alternatives)
        {
            WorldTriangle polygon = assets.ResolveNativeGroundIdentity(alternative.CurrentGround, WorldTerrainSelection.Replacements(state.ReplacementMask));
            uint flags = polygon.GroundType | ((uint)polygon.TextureFlags << 8) | ((uint)polygon.VertexFlags << 16);
            int groundHeight = state.NativeHeight - (polygon.GroundType <= 5 ? 320 : 0);
            Assert.True(Math.Abs(car.Position.Y - groundHeight) < 200, $"Boarding height failed: car={car.Position.Y}, ground={groundHeight}.");
            Assert.True(NativeWorldTerrain.CanDisembark(flags, 0), $"Foot permission failed: {flags:X8}.");
            Assert.True(NativeWorldTerrain.CanDisembark(flags, 38), $"Car permission failed: {flags:X8}, ground={alternative.CurrentGround}.");
        }
        IReadOnlyList<WorldNativeGardenProbe> probes = assets.CreateNativeGardenShapeFootprint(car.Position, 38, car.RotationY);
        int blocking = WorldGardenContactCertificate.Evaluate(assets, garden.Allocation, garden.Objects, 0, 38, probes, context, 0, true, true);
        Assert.True(blocking < 0, $"Forced boarding circle blocked by object {blocking}: {(blocking >= 0 ? garden.Objects[blocking] : null)}.");
    }

    private static void PlaceGardenFoot(GameSession session, WorldAssets assets, int x, int z)
    {
        WorldState state = session.State.World!;
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        NativeGardenWorldRuntimeState garden = state.NativeGardenRuntime ?? foot.ParkedCar!.Car.GardenObjects!;
        WorldGroundSelection ground = assets.LookupNativeGround(x, z, WorldTerrainSelection.Replacements(state.ReplacementMask), [])!;
        Assert.NotNull(ground);
        int height = NativeWorldTerrain.ResolveHeight(0, -(int)ground.Ground.Height, ground.Ground.Triangle.GroundType, state.NativeHeight).Height;
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(ground.Identity) with { Alternatives = [new(0, [], ground.Identity, ground.Identity) { FailedSelections = 0 }] };
        foot = foot with { GroundCache = cache, GroundYaw = 0 };
        WorldState placed = state with { X = x, Z = z, NativeHeight = height, GroundType = ground.Ground.Triangle.GroundType, CameraTargetX = x, CameraTargetZ = z, CameraTargetNativeHeight = height - 256, NativeFootRuntime = foot };
        garden = (garden with { GroundCache = cache, GroundYaw = 0, CurrentGroundHeight = unchecked((short)-(int)ground.Ground.Height), ProjectionCamera = NativeWorldFootCameraState.Create(placed) }).PublishBody(new(x, height, -z), 0, checked((short)state.BodyYaw), 0, false);
        placed = state.NativeGardenRuntime is not null ? placed with { NativeGardenRuntime = garden } : placed with { NativeFootRuntime = foot with { ParkedCar = foot.ParkedCar! with { Car = foot.ParkedCar.Car with { GardenObjects = garden } } } };
        session.SetWorldState(placed);
    }
}
