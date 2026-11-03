using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeFixedCameraCenterTests
{
    [Theory]
    [InlineData(-112, 112, 160, -160, 0, 0)]
    [InlineData(-100, 124, 190, -130, 30, 12)]
    [InlineData(-1, 1, 1, -1, 159, 111)]
    [InlineData(10000, 20000, 20000, 10000, 10160, 10112)]
    public void CollapsedClampUsesNativeCapThenFloor(short top, short bottom, short right, short left, int x, int y)
    {
        FieldInformation information = Information(new FieldBounds(top, bottom, right, left));
        Assert.True(FieldNativeCameraProbe.TryResolveFixedCenter(information, 0, out FieldNativeCameraCenter center));
        Assert.Equal(new FieldNativeCameraCenter(x, y), center);
    }

    [Fact]
    public void SelectedBoundsUseScreenZeroRegardlessOfWrapping()
    {
        FieldInformation information = Information(new FieldBounds(-184, 184, 184, -184)) with
        {
            CameraBounds = [new FieldBounds(-184, 184, 184, -184), new FieldBounds(-100, 124, 190, -130)],
            ScreenBounds = [new FieldBounds(0, 224, 320, 0), new FieldBounds(0, 2, 2, 0)],
            BackgroundWrapMask = 255
        };
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information, 0, out _));
        Assert.True(FieldNativeCameraProbe.TryResolveFixedCenter(information, 1, out FieldNativeCameraCenter center));
        Assert.Equal(new FieldNativeCameraCenter(30, 12), center);
    }

    [Theory]
    [InlineData(-113, 112, 160, -160)]
    [InlineData(-112, 112, 161, -160)]
    [InlineData(-112, 112, 2161, 1840)]
    [InlineData(-184, 184, 184, -184)]
    public void EitherMovingAxisRefusesFixedCenter(short top, short bottom, short right, short left)
    {
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(Information(new FieldBounds(top, bottom, right, left)), 0, out _));
    }

    [Fact]
    public void InvalidMetadataAndOverflowingClampEndpointsRefuse()
    {
        FieldInformation information = Information(new FieldBounds(-112, 112, 160, -160));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(null!, 0, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information, -1, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information, 1, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information, 2, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information with { CameraBounds = null! }, 0, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information with { ScreenBounds = [] }, 0, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information with { ScreenBounds = [new FieldBounds(1, 0, 320, 0)] }, 0, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information with { CameraBounds = [new FieldBounds(0, 0, short.MinValue, short.MinValue)] }, 0, out _));
        Assert.False(FieldNativeCameraProbe.TryResolveFixedCenter(information with { CameraBounds = [new FieldBounds(short.MaxValue, short.MaxValue, 0, 0)] }, 0, out _));
    }

    private static FieldInformation Information(FieldBounds cameraBounds)
    {
        return new FieldInformation(0, 200, [cameraBounds], [new FieldBounds(0, 224, 320, 0)], [], []);
    }
}
