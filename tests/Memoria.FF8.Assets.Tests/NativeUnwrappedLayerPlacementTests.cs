using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeUnwrappedLayerPlacementTests
{
    [Fact]
    public void SignedRatioKernelKeepsNativeTruncationAndRefusesUnprovedPacking()
    {
        foreach ((int center, short ratio, int expected) in new (int, short, int)[] { (400, -256, 893), (-400, -256, -707), (400, 512, -307), (-3, 128, 91), (0, short.MinValue, 93), (0, short.MaxValue, 93) })
        {
            Assert.True(NativeBackgroundLayerPlacement.TryTranslation(center, 123, ratio, 30, out int actual));
            Assert.Equal(expected, actual);
        }
        Assert.False(NativeBackgroundLayerPlacement.TryTranslation(4096, 0, short.MaxValue, 0, out int rejected));
        Assert.Equal(0, rejected);
    }

    [Fact]
    public void MaskClearLayerPreservesWordTranslationOutsideTheWrappedOriginWindow()
    {
        FieldNativeBackgroundWrapFrame frame = new(600, "component", -192, 176, -144, 128, ImmutableArray<FieldNativeBackgroundWrapLayer>.Empty);
        FieldNativeBackgroundWrapLayer layer = new(1, -240, 0, 0, 0);
        Assert.Equal(new FieldNativeBackgroundTileOrigin(-432, -144), FieldNativeBackgroundTileProbe.Relocate(-192, -144, layer, frame));
        Assert.Equal(new FieldNativeBackgroundTileOrigin(760, 1000), FieldNativeBackgroundTileProbe.Relocate(1000, 1000, layer, frame));
        Assert.Equal(new FieldNativeBackgroundTileOrigin(32616, 0), FieldNativeBackgroundTileProbe.Relocate(-32680, 0, layer, frame));
        Assert.Null(FieldNativeBackgroundTileProbe.Relocate(1000, 1000, layer with { PeriodX = 352, PeriodY = 96 }, frame));
    }
}
