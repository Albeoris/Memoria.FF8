using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareNormalViewportTests
{
    [Fact]
    public void ModeOneUsesActualViewportAndOffsetsWithoutChangingSoftwareCoordinates()
    {
        NativeSoftwareDrawingEnvironmentState environment = Environment(10, 20, 320, 200, 15, 27);
        string before = JsonSerializer.Serialize(environment);
        NativeSoftwareNormalViewportDerived output = NativeSoftwareNormalViewport.Calculate45B6F0(environment, new(640, 600, 4, -3));
        Assert.Equal(2f, output.ScaleX);
        Assert.Equal(3f, output.ScaleY);
        Assert.Equal(0.25f, output.LowCoordinateScaleX);
        Assert.Equal(0.375f, output.LowCoordinateScaleY);
        Assert.Equal(3u, output.ScaleFlags);
        Assert.Equal(13.5f, output.NormalOffsetX);
        Assert.Equal(17.5f, output.NormalOffsetY);
        Assert.Equal(before, JsonSerializer.Serialize(environment));
        Assert.Equal(15 << 16, environment.Derived!.DrawOffsetX);
        NativeSoftwareDrawingEnvironmentState shiftedOrigin = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(environment with { OriginX = 50, OriginY = -50, Debug = 1 });
        Assert.Equal(output, NativeSoftwareNormalViewport.Calculate45B6F0(shiftedOrigin, new(640, 600, 4, -3)));
        Assert.NotEqual(environment.Derived.DrawOffsetX, shiftedOrigin.Derived!.DrawOffsetX);
    }

    [Fact]
    public void ThresholdUsesStoredFloatXAndExtendedYIndependently()
    {
        NativeSoftwareDrawingEnvironmentState environment = Environment(0, 0, 1000, 1000, 0, 0);
        NativeSoftwareNormalViewportDerived output = NativeSoftwareNormalViewport.Calculate45B6F0(environment, new(1001, 1001, 0, 0));
        Assert.Equal(1.001f, output.ScaleX);
        Assert.Equal(output.ScaleX, output.ScaleY);
        Assert.Equal(1u, output.ScaleFlags);
    }

    [Theory]
    [InlineData(704, 256, 128, 128, 2)]
    [InlineData(576, 256, 320, 232, 3)]
    public void OffscreenModesIgnoreDrawingOffsetAndPreserveIntermediateYFloatStore(int x, int y, int width, int height, int mode)
    {
        NativeSoftwareDrawingEnvironmentState environment = Environment((short)x, (short)y, (short)width, (short)height, -123, 234);
        Assert.Equal((uint)mode, environment.Derived!.Mode);
        NativeSoftwareNormalViewportDerived output = NativeSoftwareNormalViewport.Calculate45B6F0(environment, new(901, 713, 16777219, 16777219));
        Assert.Equal(16777218f, output.NormalOffsetX);
        Assert.Equal(16777220f, output.NormalOffsetY);
        string json = JsonSerializer.Serialize(output);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeSoftwareNormalViewportDerived>(json)));
    }

    [Fact]
    public void ZeroDimensionsUseOneWhileNegativeSignedDimensionsRemainNegative()
    {
        NativeSoftwareNormalViewportDerived zero = NativeSoftwareNormalViewport.Calculate45B6F0(Environment(0, 0, 0, 0, 0, 0), new(320, 216, 0, 0));
        Assert.Equal(320f, zero.ScaleX);
        Assert.Equal(216f, zero.ScaleY);
        NativeSoftwareNormalViewportDerived negative = NativeSoftwareNormalViewport.Calculate45B6F0(Environment(0, 0, -160, -108, 0, 0), new(320, 216, 0, 0));
        Assert.Equal(-2f, negative.ScaleX);
        Assert.Equal(-2f, negative.ScaleY);
        Assert.Equal(0u, negative.ScaleFlags);
    }

    [Fact]
    public void MissingActualEnvironmentCannotProduceNormalViewportDefaults()
    {
        NativeSoftwareDrawingEnvironmentState cold = NativeSoftwareDrawingEnvironmentState.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => NativeSoftwareNormalViewport.Calculate45B6F0(cold, new(640, 480, 0, 0)));
        NativeSoftwareDrawingEnvironmentState known = Environment(0, 0, 320, 216, 0, 0);
        Assert.Throws<NotSupportedException>(() => NativeSoftwareNormalViewport.Calculate45B6F0(known with { OffsetX = null }, new(640, 480, 0, 0)));
    }

    private static NativeSoftwareDrawingEnvironmentState Environment(short left, short top, short width, short height, short offsetX, short offsetY)
    {
        NativeSoftwareDrawingEnvironmentState state = new(left, top, unchecked((uint)(left + width - 1)), unchecked((uint)(top + height - 1)), offsetX, offsetY, 0, 0, 0, null);
        return NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state);
    }
}
