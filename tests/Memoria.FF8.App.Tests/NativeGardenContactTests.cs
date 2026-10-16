using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGardenContactTests
{
    private static readonly NativeWorldContactShape Foot = new(96, 96, 180, 1);
    private static readonly NativeWorldContactShape Garden = new(1023, 1023, 704, 4);

    [Theory]
    [InlineData(558, true)]
    [InlineData(559, false)]
    [InlineData(560, false)]
    public void ForcedTransferCirclesUseStrictCombinedNativeRadius(int distance, bool expected)
    {
        NativeWorldPathWords[] points = Diamond(distance, -10000, 0, 32);
        Assert.Equal(expected, WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, points, new(0, 10000, 0), 0, true));
    }

    [Fact]
    public void ForcedCirclePaddingIsAddedAtBothNativeCallSites()
    {
        Assert.True(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(958, 0, 0, 32), new(0, 0, 0), 200, true));
        Assert.False(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(959, 0, 0, 32), new(0, 0, 0), 200, true));
    }

    [Fact]
    public void OrdinaryFootContactUsesAllFivePointsAndTheNativeInclusiveCircleBoundary()
    {
        Assert.True(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(543, 0, 0, 32), new(0, 0, 0), 0, false));
        Assert.False(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(544, 0, 0, 32), new(0, 0, 0), 0, false));
        Assert.False(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(0, -1000, 0, 32), new(0, 0, 0), 0, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void CircleInsideTheSelectedDiamondUsesTheOriginalPlanarReverseContainment(int height)
    {
        Assert.True(WorldGardenContactCertificate.EvaluateObject(new(20, 20, 100, 4), 0, 64, Garden, 48, Diamond(20, height, 0, 64), new(0, 0, 0), 0, false));
    }

    [Fact]
    public void ForcedContactRetainsTheNativeSignedDwordSumAfterWordSubtraction()
    {
        Assert.True(WorldGardenContactCertificate.EvaluateObject(Garden, 0, 64, Foot, 0, Diamond(short.MinValue, 0, short.MinValue, 32), new(0, 0, 0), 0, true));
    }

    [Fact]
    public void ActualAllocationScanKeepsOrderedDistinctGardenContactsAndCurrentVehicleExclusions()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750);
        using GameAssets game = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(12, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals());
        NativeSavedWorldPose pose = allocation.LoadPose;
        int x = pose.X + 500;
        NativeWorldFootCameraState camera = new(x, pose.RawZ, pose.Height, x, pose.RawZ, pose.Height - 256, 0, -112, -341, -7168);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        IReadOnlyList<WorldNativeGardenProbe> probes = WorldAssets.CreateNativeGroundFootprint(x, pose.RawZ, pose.Height, 32);
        Assert.Equal(allocation.Garden64Index, WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 0, 0, probes, projection, trainContactsExcluded: true));
        Assert.Equal(-1, WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 48, 48, probes, projection));
        NativeGardenWorldObject[] objects = allocation.Objects.ToArray();
        objects[allocation.Garden64Index] = objects[allocation.Garden64Index] with { Position = new(60000, pose.Height, 4600) };
        NativeGardenWorldObject[] restored = JsonSerializer.Deserialize<NativeGardenWorldObject[]>(JsonSerializer.Serialize(objects))!;
        Assert.Equal(allocation.Garden65Index, WorldGardenContactCertificate.Evaluate(assets, allocation, restored, 0, 0, probes, projection, trainContactsExcluded: true));
        IReadOnlyList<WorldNativeGardenProbe> clear = WorldAssets.CreateNativeGroundFootprint(pose.X + 3000, pose.RawZ, pose.Height, 32);
        Assert.Throws<NotSupportedException>(() => WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 0, 0, clear, projection));
    }

    private static NativeWorldPathWords[] Diamond(int x, int y, int z, int radius)
    {
        return [new((short)x, (short)y, (short)z), new((short)(x - radius), (short)y, (short)z), new((short)x, (short)y, (short)(z + radius)), new((short)(x + radius), (short)y, (short)z), new((short)x, (short)y, (short)(z - radius))];
    }
}
