using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeCameraProbeTests
{
    [Fact]
    public void OriginalFirstFloorArrivalsUseNativeHeightAnchorAndMovingClamp()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(165);
        Assert.Single(field.Cameras);
        Assert.Equal(200, field.Information!.CameraHeight);
        Assert.Equal(new FieldBounds(-184, 184, 184, -184), field.Information.CameraBounds[0]);
        Assert.Equal(new FieldBounds(0, 224, 320, 0), field.Information.ScreenBounds[0]);
        Assert.Equal(1323, FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, 244, -126, -3803));
        Assert.Equal(567, FieldPartyTraceGeometry.ResolveTriangleHeight(field.Walkmesh, 372, 157, -7985));

        FieldNativeCameraResult entry = FieldNativeCameraProbe.Resolve(field.Camera, field.Information, -126 << 12, -3803 << 12, 1323 << 12);
        Assert.True(entry.Supported, entry.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(15, -72), entry.Center);
        Assert.Equal(new FieldNativeCameraPoint(-126, -3803, 1523), entry.Diagnostics!.Anchor);
        Assert.Equal(new FieldNativeCameraPoint(246, -2105, 10769), entry.Diagnostics.Transformed);
        Assert.Equal(10769, entry.Diagnostics.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(125, -1073), entry.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(15, -134), entry.Diagnostics.UnclampedCenter);

        FieldNativeCameraResult returned = FieldNativeCameraProbe.Resolve(field.Camera, field.Information, 157 << 12, -7985 << 12, 567 << 12);
        Assert.True(returned.Supported, returned.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(-3, 0), returned.Center);
        Assert.Equal(new FieldNativeCameraPoint(-36, 0, 7077), returned.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraCenter(-28, 0), returned.Diagnostics.ProjectedEighth);

        FieldAssets elevator = assets.LoadField(172);
        FieldNativeCameraResult fixedCenter = FieldNativeCameraProbe.Resolve(elevator.Camera, elevator.Information!, -14 << 12, -3100 << 12, 1344 << 12);
        Assert.True(fixedCenter.Supported, fixedCenter.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), fixedCenter.Center);
    }

    [Fact]
    public void FollowAnchorUsesArithmeticShiftAndSignedWordWrapping()
    {
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera() with { AxisX = Vector3.Zero, AxisY = Vector3.Zero }, Information() with { CameraHeight = 32767 }, (32768 << 12) - 1, -4097, 1 << 12);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(32767, -2, -32768), result.Diagnostics!.Anchor);
        FieldNativeCameraResult wrapped = FieldNativeCameraProbe.Resolve(Camera() with { AxisX = Vector3.Zero, AxisY = Vector3.Zero }, Information(), 32768 << 12, -1, 0);
        Assert.True(wrapped.Supported, wrapped.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(-32768, -1, 0), wrapped.Diagnostics!.Anchor);
    }

    [Fact]
    public void MatrixRoundsBeforeTranslationAndEighthDivisionTruncatesNegativeValues()
    {
        FieldCamera camera = Camera() with { AxisX = new Vector3(1 / 4096f, 0, 0), Translation = new Vector3(1, 0, 1000), Pan = new Vector2(50, -60) };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), 3000 << 12, -4 << 12, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(2, -4, 1000), result.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraCenter(2, -3), result.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Center);
    }

    [Fact]
    public void NonnegativeDepthUsesUnsignedFocalHalfNearClamp()
    {
        FieldCamera camera = Camera() with { Translation = Vector3.Zero, FocalLength = 101 };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), 1 << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(50, result.Diagnostics!.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(16, 0), result.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(2, 0), result.Center);
        FieldNativeCameraResult zeroDenominator = FieldNativeCameraProbe.Resolve(camera with { FocalLength = 1 }, Information(), 0, 0, 0);
        Assert.False(zeroDenominator.Supported);
        Assert.Contains("denominator", zeroDenominator.UnsupportedReason);
    }

    [Fact]
    public void NativeBoundsCapThenFloorRatherThanAssumingOrderedClampInterval()
    {
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-1, 1, 1, -1)] };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera(), information, 0, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(9, 9), result.Center);
        information = information with { CameraBounds = [new FieldBounds(0, 0, short.MinValue, short.MinValue)], ScreenBounds = [new FieldBounds(0, 0, short.MaxValue, short.MinValue)] };
        FieldNativeCameraResult wordAssignment = FieldNativeCameraProbe.Resolve(Camera(), information, 0, 0, 0);
        Assert.True(wordAssignment.Supported, wordAssignment.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(1, 0), wordAssignment.Center);
    }

    [Theory]
    [InlineData(1, 14, 14, 7, 0)]
    [InlineData(-1, -14, -14, -7, 0)]
    [InlineData(3, 14, 16, 8, 1)]
    [InlineData(-3, -14, -16, -8, -1)]
    public void MatrixHalfTiesUseNativeNearestEvenBeforeTranslation(int x, int translation, int transformed, int projected, int center)
    {
        FieldCamera camera = Camera() with { AxisX = new Vector3(0.5f, 0, 0), Translation = new Vector3(translation, 0, 1600) };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), x << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(transformed, 0, 1600), result.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraCenter(projected, 0), result.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(center, 0), result.Center);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(15, 8, 1)]
    [InlineData(-15, -8, -1)]
    [InlineData(31, 16, 2)]
    [InlineData(-31, -16, -2)]
    public void PerspectiveHalfTiesHaveOneDeterministicCenter(int x, int projected, int center)
    {
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 1600) }, Information(), x << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(projected, 0), result.Diagnostics!.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(center, 0), result.Center);
    }

    [Fact]
    public void NativeRawTransformRegistersSurviveDiagnosticWordSaturationAndDepthClampsToUnsignedWord()
    {
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-2000, 2000, 2000, -2000)] };
        FieldNativeCameraResult wide = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(40000, -40000, 100000) }, information, 0, 0, 0);
        Assert.True(wide.Supported, wide.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(40000, -40000, 100000), wide.Diagnostics!.Transformed);
        Assert.Equal(65535, wide.Diagnostics.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(488, -488), wide.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(61, -61), wide.Center);
        FieldNativeCameraResult negative = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, -1), FocalLength = 101 }, information, 2000 << 12, -2000 << 12, 0);
        Assert.True(negative.Supported, negative.UnsupportedReason);
        Assert.Equal(50, negative.Diagnostics!.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(8184, -8192), negative.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(1023, -1024), negative.Center);
    }

    [Theory]
    [InlineData(102, -2715648, -1605632, 0)]
    [InlineData(102, -2818048, -1495040, 0)]
    [InlineData(102, -2572288, -1720320, 0)]
    [InlineData(185, 6230412, 564653, 2289664)]
    [InlineData(165, -9329120, -22734368, 2322432)]
    public void ReachedAndNearbyDoorRoundingBoundariesAlwaysHaveOneCenter(int fieldId, int x, int y, int z)
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(fieldId);
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(field.Camera, field.Information!, x, y, z);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.NotNull(result.Diagnostics!.Transformed);
        Assert.NotNull(result.Diagnostics.ProjectedEighth);
        if (fieldId == 102 && x == -2715648)
        {
            Assert.Equal(new FieldNativeCameraPoint(-591, 77, 1154), result.Diagnostics.Transformed);
            Assert.Equal(new FieldNativeCameraCenter(-1540, 201), result.Diagnostics.ProjectedEighth);
            Assert.Equal(new FieldNativeCameraCenter(-80, 25), result.Center);
        }
    }

    [Fact]
    public void BalambDoorNeighborhoodHasNoPositionDependentProjectionRefusal()
    {
        using GameAssets assets = InstalledAssets();
        FieldAssets field = assets.LoadField(102);
        for (int x = -906; x <= -394; x++)
        {
            for (int y = -634; y <= -122; y++)
            {
                FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(field.Camera, field.Information!, x << 12, y << 12, 0);
                Assert.True(result.Supported, $"Anchor({x},{y},0): {result.UnsupportedReason}");
            }
        }
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        return new GameAssets(directory);
    }
    [Fact]
    public void UnsupportedCameraMetadataAndNonzeroSelectedSlotHaveNoInventedCenter()
    {
        FieldCamera[] invalid = [Camera() with { AxisX = new Vector3(0.1f, 0, 0) }, Camera() with { Translation = new Vector3(float.NaN, 0, 1000) }, Camera() with { Translation = new Vector3(16777218, 0, 1000) }, Camera() with { FocalLength = 0 }, Camera() with { FocalLength = 1.5f }];
        foreach (FieldCamera camera in invalid)
        {
            FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), 0, 0, 0);
            Assert.False(result.Supported);
            Assert.Null(result.Center);
            Assert.NotNull(result.UnsupportedReason);
        }
        Assert.False(FieldNativeCameraProbe.Resolve(Camera(), Information(), 0, 0, 0, selectedCameraIndex: 1).Supported);
        Assert.False(FieldNativeCameraProbe.Resolve(Camera(), Information() with { CameraBounds = [] }, 0, 0, 0).Supported);
        Assert.False(FieldNativeCameraProbe.Resolve(Camera(), Information() with { ScreenBounds = [] }, 0, 0, 0).Supported);
    }

    private static FieldCamera Camera()
    {
        return new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.Zero, new Vector3(0, 0, 1000), Vector2.Zero, 100);
    }

    private static FieldInformation Information()
    {
        return new FieldInformation(0, 0, [new FieldBounds(-100, 100, 100, -100)], [new FieldBounds(0, 20, 20, 0)], [], []);
    }
}
