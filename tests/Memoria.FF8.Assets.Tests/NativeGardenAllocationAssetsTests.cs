using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenAllocationAssetsTests
{
    [Fact]
    public void ActualPriorCarPreviousPolygonAuthenticatesThePreQueryCameraHeight()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldGroundIdentity previous = new(327, 829, 1, 68);
        WorldGroundSelection selected = assets.LookupNativeGround(-71662, -14849, WorldTerrainSelection.Replacements(349), [previous])!;
        Assert.Equal(previous, selected.Identity);
        Assert.Equal(-340, -(int)selected.Ground.Height);
    }

    [Fact]
    public void ActualSavedGuardianForceFlagControlsShape94BeforeReplacementPairs()
    {
        const string gameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        using WorldAssets assets = new(gameDirectory);
        using GameAssets game = new(gameDirectory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750).WriteBytes(264, 1, 1);
        NativeGardenWorldAllocation present = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), vitals);
        Assert.Equal(new byte[] { 0, 2, 64, 65, 94, 79, 80 }, present.Objects.Select(item => item.ShapeId));
        Assert.Equal(vitals.GuardianForces[13].Flags, present.GuardianForce13Flags);
        Assert.Equal(new NativeWorldPathPosition(53625, -314, -59399), present.Objects[present.CactuarIndex].Position);
        NativeGardenWorldAllocation absent = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), vitals.AcquireGuardianForce(13));
        Assert.Equal(new byte[] { 0, 2, 64, 65, 79, 80 }, absent.Objects.Select(item => item.ShapeId));
        Assert.Equal(-1, absent.CactuarIndex);
    }

    [Fact]
    public void ParkedFootEntry62KeepsSavedPose3DistinctFromControlledFootPose0()
    {
        const string gameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        using WorldAssets assets = new(gameDirectory);
        using GameAssets game = new(gameDirectory);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750);
        NativeSavedWorldPose parked = new(30000, -7000, -120, 64);
        WorldState foot = new() { Frame = 71, X = 31500, Z = -6200, NativeHeight = -32, BodyYaw = 128, CameraYaw = 256 };
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame().WithFootReturn(NativeSavedWorldFootReturn.Create(foot, 257, 750, 3, 0, 0)) with { GardenPose3 = parked };
        NativeGardenWorldAllocation allocation = assets.ReadParkedGardenAllocation(new(62, 0, -1), entry, NativeEventMemory.Empty, saved, game.ReadNewGameCharacterVitals());
        Assert.Equal(new WorldTransition(62, 0, -1), allocation.Packet);
        Assert.Equal(parked, allocation.GardenPose);
        Assert.Equal(new NativeWorldPathPosition(30000, -120, 7000), allocation.Objects[allocation.Garden64Index].Position);
        Assert.Equal(new NativeWorldPathPosition(31500, -32, 6200), allocation.Objects[allocation.FootObjectIndex].Position);
        Assert.Equal((ushort)256, allocation.CameraYaw);
    }
}
