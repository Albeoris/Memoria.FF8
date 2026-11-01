using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGardenSavedCarTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    public void GardenAllocationPreservesSavedCarOrderPoseContactsModelsAndReturn(int entity)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world resources are required.");
        using WorldAssets assets = new(directory);
        using GameAssets game = new(directory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750).WriteBytes(264, 1, 1);
        NativeGardenWorldAllocation baseline = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals());
        NativeSavedWorldPose gardenPose = baseline.LoadPose;
        NativeSavedWorldPose carPose = new(gardenPose.X + 1800, gardenPose.RawZ, gardenPose.Height, 256);
        byte shape = NativeCarWorldRuntimeState.ShapeForEntity(checked((byte)entity));
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with { Slot4 = new(shape, carPose) };
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, saved, game.ReadNewGameCharacterVitals());
        Assert.Equal(new byte[] { 0, 2, 64, 65, shape, 94, 79, 80 }, allocation.Objects.Select(item => item.ShapeId));
        Assert.Equal(saved.Slot4, allocation.SavedCarSlot);
        NativeGardenWorldObject parked = allocation.Objects[allocation.SavedCarIndex];
        Assert.Equal(new NativeWorldPathPosition(carPose.X, carPose.Height, -carPose.RawZ), parked.Position);
        Assert.Equal((short)256, parked.RotationY);
        Assert.Equal(shape, assets.ReadScriptedStaticModel(shape).ShapeId);
        Assert.NotEmpty(assets.ReadScriptedStaticModel(shape).Faces);
        NativeWorldFootCameraState camera = new(carPose.X, carPose.RawZ, carPose.Height, carPose.X, carPose.RawZ, carPose.Height - 256, 0, -112, -341, -7168);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        IReadOnlyList<WorldNativeGardenProbe> footProbes = WorldAssets.CreateNativeGroundFootprint(carPose.X, carPose.RawZ, carPose.Height, 32);
        Assert.Equal(allocation.SavedCarIndex, WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 0, 0, footProbes, projection, trainContactsExcluded: true));
        IReadOnlyList<WorldNativeGardenProbe> gardenProbes = WorldAssets.CreateNativeGroundFootprint(carPose.X, carPose.RawZ, carPose.Height, 64);
        Assert.Equal(-1, WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 48, 48, gardenProbes, projection));
        Assert.Equal(allocation.SavedCarIndex, WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 0, 48, gardenProbes, projection, forceCircle: true, trainContactsExcluded: true));
        WorldGroundSelection ground = assets.LookupNativeGround(gardenPose.X, gardenPose.RawZ, WorldTerrainSelection.Replacements(WorldTerrainSelection.ReplacementMask(1, 0)), [])!;
        Assert.NotNull(ground);
        NativeGardenWorldRuntimeState runtime = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, NativeWorldGroundCacheState.CreateFresh(ground.Identity), NativeWorldProcess.FromColdProcess(), assets.ReadGardenCameraDefaults(), camera, unchecked((short)-(int)ground.Ground.Height), false);
        NativeGardenWorldRuntimeState moved = runtime.PublishBody(new(gardenPose.X + 64, gardenPose.Height, -gardenPose.RawZ), 0, 32, 0);
        Assert.Equal(parked, moved.Objects[allocation.SavedCarIndex]);
        NativeGardenWorldRuntimeState restored = JsonSerializer.Deserialize<NativeGardenWorldRuntimeState>(JsonSerializer.Serialize(moved))!;
        restored.Validate();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(moved), JsonSerializer.SerializeToElement(restored)));
        Assert.Throws<InvalidDataException>(() => (restored with { Objects = restored.Objects.SetItem(allocation.SavedCarIndex, parked with { Position = parked.Position with { X = parked.Position.X + 1 } }) }).Validate());
        WorldState endpoint = new() { Frame = 1, X = gardenPose.X + 64, Z = gardenPose.RawZ, NativeHeight = gardenPose.Height, BodyYaw = 32, CameraYaw = 0, NativeGardenRuntime = restored };
        NativeSavedWorldState returned = saved.WithGardenReturn(NativeSavedWorldGardenReturn.Create(endpoint, 257, 750, 0));
        Assert.Equal(saved.Slot4, returned.Slot4);
        Assert.Equal(saved.Slot5, returned.Slot5);
        NativeSavedWorldState footSaved = saved.WithFootReturn(NativeSavedWorldFootReturn.Create(endpoint with { NativeGardenRuntime = null }, 257, 750, 0, 0, 0)) with { GardenPose3 = gardenPose };
        NativeGardenWorldAllocation footReload = assets.ReadParkedGardenAllocation(new(62, 0, -1), entry, NativeEventMemory.Empty, footSaved, game.ReadNewGameCharacterVitals());
        Assert.Equal(parked, footReload.Objects[footReload.SavedCarIndex]);
    }
}
