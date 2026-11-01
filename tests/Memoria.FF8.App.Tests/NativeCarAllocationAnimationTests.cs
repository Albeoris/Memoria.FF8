using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarAllocationAnimationTests
{
    [Theory]
    [InlineData(0u, 0)]
    [InlineData(3u, 24)]
    [InlineData(8193u, 8)]
    [InlineData(uint.MaxValue, -8)]
    public void ModeFourRetainsYawAndWritesOriginalTiltAndSpinWords(uint counter, short spin)
    {
        NativeTrainWorldObject item = NativeCarWorldAllocationReader.FromPose(80, new(0, 0, 0), 0x12340056) with { Flags = 4 };
        Assert.Equal(new NativeWorldPathAngles(spin, 0x856, 786), WorldStaticModelTransform.AnimatedAllocationAngles(item, counter));
        Assert.Equal(0x12340056, item.PackedOrientation);
    }

    [Fact]
    public void FreshLoaderAndUnanimatedObjectRetainOriginalCoordinateAngles()
    {
        NativeTrainWorldObject item = NativeCarWorldAllocationReader.FromPose(80, new(0, 0, 0), 0x12340056) with { Flags = 4 };
        NativeWorldPathAngles original = new(0x1234, 0x856, 0);
        Assert.Equal(original, WorldStaticModelTransform.AnimatedAllocationAngles(item, null));
        Assert.Equal(original, WorldStaticModelTransform.AnimatedAllocationAngles(item with { Flags = 0 }, 123));
    }

    [Fact]
    public void ModeFourChangesPresentationWithoutChangingTheNativeYawContactContract()
    {
        NativeTrainWorldObject item = NativeCarWorldAllocationReader.FromPose(80, new(0, 0, 0), 0x00100000) with { Flags = 4 };
        NativeWorldPathAngles initial = WorldStaticModelTransform.AllocationAngles(item);
        NativeWorldPathAngles animated = WorldStaticModelTransform.AnimatedAllocationAngles(item, 7);
        Assert.Equal(initial.Yaw, animated.Yaw);
        Assert.NotEqual(initial.Roll, animated.Roll);
        Assert.NotEqual(initial.Pitch, animated.Pitch);
        NativeWorldContactShape shape = new(400, 100, 180, 1);
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(shape, initial.Yaw, new(0, 0, 0), new(0, 0, 0)));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(shape, animated.Yaw, new(0, 0, 0), new(0, 0, 0)));
    }
}
