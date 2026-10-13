using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldScriptedPathMathTests
{
    [Fact]
    public void NativeRailDistanceUsesSignedLowWordsAndTruncatesSquareRoot()
    {
        Assert.Equal(5, NativeWorldScriptedPathMath.SegmentLength(new(0, 0, 0, 0), new(3, 4, 0, 99)));
        Assert.Equal(5, NativeWorldScriptedPathMath.SegmentLength(new(0, 0, 0, 0), new(65539, -65532, 1, 99)));
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathMath.SegmentLength(new(0, 0, 0, 0), new(short.MinValue, short.MinValue, short.MinValue, 0)));
    }

    [Fact]
    public void OriginalLinkedBodyAnglesUseLinkDirectionAndPitchInsteadOfFootYaw()
    {
        Assert.Equal(new NativeWorldPathAngles(0, 2048, 0), NativeWorldScriptedPathMath.LinkedOrientation(new(0, 0, 0), new(100, 0, 0)));
        Assert.Equal(new NativeWorldPathAngles(0, 1024, 0), NativeWorldScriptedPathMath.LinkedOrientation(new(0, 0, 0), new(0, 0, 100)));
        Assert.Equal(new NativeWorldPathAngles(-512, 2048, 0), NativeWorldScriptedPathMath.LinkedOrientation(new(0, 0, 0), new(100, 100, 0)));
        Assert.Equal(NativeWorldScriptedPathMath.LinkedOrientation(new(0, 0, 0), new(3, 4, 5)), NativeWorldScriptedPathMath.LinkedOrientation(new(0, 0, 0), new(65539, -65532, 65541)));
    }
}
