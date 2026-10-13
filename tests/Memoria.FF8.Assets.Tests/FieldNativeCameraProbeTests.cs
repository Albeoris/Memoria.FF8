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

    [Fact]
    public void MatrixMagicAddTieCanBeCertifiedByFinalLogicalDivision()
    {
        FieldCamera matrixTie = Camera() with { AxisX = new Vector3(0.5f, 0, 0) };
        FieldNativeCameraResult matrix = FieldNativeCameraProbe.Resolve(matrixTie, Information(), 1 << 12, 0, 0);
        Assert.True(matrix.Supported, matrix.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), matrix.Center);
        Assert.Null(matrix.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraPoint(0, 0, 1000), matrix.Diagnostics.TransformedLow);
        Assert.Equal(new FieldNativeCameraPoint(1, 0, 1000), matrix.Diagnostics.TransformedHigh);
        Assert.Equal(1000, matrix.Diagnostics.ProjectionDepth);
    }

    [Fact]
    public void AllEightMatrixCornerOutcomesCanHaveOneFinalCenter()
    {
        FieldCamera camera = Camera() with { AxisX = new Vector3(0.5f, 0, 0), AxisY = new Vector3(0.5f, 0, 0), AxisZ = new Vector3(0.5f, 0, 0) };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), 1 << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Null(result.Diagnostics!.Transformed);
        Assert.Null(result.Diagnostics.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraPoint(0, 0, 1000), result.Diagnostics.TransformedLow);
        Assert.Equal(new FieldNativeCameraPoint(1, 1, 1001), result.Diagnostics.TransformedHigh);
        Assert.Equal(1000, result.Diagnostics.ProjectionDepthLow);
        Assert.Equal(1001, result.Diagnostics.ProjectionDepthHigh);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(1, 1), result.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Center);
    }

    [Theory]
    [InlineData(1, 14, 14, 15, 7, 8)]
    [InlineData(-1, -14, -15, -14, -8, -7)]
    public void MatrixIntervalRefusesDistinctCentersAndAdmitsForcedClamp(int x, int translation, int low, int high, int projectedLow, int projectedHigh)
    {
        FieldCamera camera = Camera() with { AxisX = new Vector3(0.5f, 0, 0), Translation = new Vector3(translation, 0, 1600) };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, Information(), x << 12, 0, 0);
        Assert.False(result.Supported);
        Assert.Contains("different final", result.UnsupportedReason);
        Assert.Null(result.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraPoint(low, 0, 1600), result.Diagnostics.TransformedLow);
        Assert.Equal(new FieldNativeCameraPoint(high, 0, 1600), result.Diagnostics.TransformedHigh);
        Assert.Equal(new FieldNativeCameraCenter(projectedLow, 0), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(projectedHigh, 0), result.Diagnostics.ProjectedEighthHigh);
        FieldNativeCameraResult clamped = FieldNativeCameraProbe.Resolve(camera, Information() with { CameraBounds = [new FieldBounds(-10, 10, 10, -10)] }, x << 12, 0, 0);
        Assert.True(clamped.Supported, clamped.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), clamped.Center);
    }

    [Fact]
    public void MatrixIntervalSupportsProjectedSaturationAndNegativeDepthButRefusesTransformSaturationOrZeroDenominator()
    {
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-10, 10, 10, -10)] };
        FieldNativeCameraResult saturation = FieldNativeCameraProbe.Resolve(Camera() with { AxisX = new Vector3(0.5f, 0, 0), Translation = new Vector3(32767, 0, 1000) }, information, 1 << 12, 0, 0);
        Assert.False(saturation.Supported);
        Assert.Contains("signed-WORD saturation", saturation.UnsupportedReason);
        FieldNativeCameraResult projected = FieldNativeCameraProbe.Resolve(Camera() with { AxisX = new Vector3(0.5f, 0, 0), Translation = new Vector3(16368, 0, 1600) }, information, 1 << 12, 0, 0);
        Assert.True(projected.Supported, projected.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), projected.Center);
        FieldNativeCameraResult negative = FieldNativeCameraProbe.Resolve(Camera() with { AxisZ = new Vector3(-0.5f, 0, 0), Translation = Vector3.Zero }, information, 1 << 12, 0, 0);
        Assert.True(negative.Supported, negative.UnsupportedReason);
        Assert.Equal(50, negative.Diagnostics!.ProjectionDepth);
        FieldNativeCameraResult zero = FieldNativeCameraProbe.Resolve(Camera() with { AxisZ = new Vector3(0.5f, 0, 0), Translation = Vector3.Zero, FocalLength = 1 }, information, 1 << 12, 0, 0);
        Assert.False(zero.Supported);
        Assert.Contains("denominator", zero.UnsupportedReason);
    }

    [Fact]
    public void ReachedQuad18539MatrixIntervalHasOneFinalNativeCameraCenter()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(185);
        // Genuine field185/frame39 SHA e25236336118d79e7b287ade72c72c9a36ce714fde938d5117b40ea00a25aed6.
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(field.Camera, field.Information!, 6230412, 564653, 2289664);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Null(result.Diagnostics!.Transformed);
        Assert.Equal(new FieldNativeCameraPoint(1521, 137, 759), result.Diagnostics.Anchor);
        Assert.Equal(new FieldNativeCameraPoint(2562, -2331, 9625), result.Diagnostics.TransformedLow);
        Assert.Equal(new FieldNativeCameraPoint(2562, -2330, 9625), result.Diagnostics.TransformedHigh);
        Assert.Equal(9625, result.Diagnostics.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(1365, -1242), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(1365, -1241), result.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(170, -155), result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(80, -72), result.Center);
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(-1, -1, 0)]
    public void PerspectiveAmbiguityCanBeCertifiedBySignedDivision(int x, int low, int high)
    {
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 1600) }, Information(), x << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Null(result.Diagnostics!.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(low, 0), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(high, 0), result.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), result.Center);
    }

    [Theory]
    [InlineData(31, 15, 16, 1)]
    [InlineData(-31, -16, -15, -1)]
    public void PerspectiveAmbiguityCanBeCertifiedByNativeClamp(int x, int low, int high, int clamped)
    {
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-100, 100, 11, -11)] };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 1600) }, information, x << 12, 0, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Null(result.Diagnostics!.ProjectedEighth);
        Assert.Null(result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(low, 0), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(high, 0), result.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(clamped, 0), result.Center);
    }

    [Theory]
    [InlineData(15, 7, 8)]
    [InlineData(-15, -8, -7)]
    public void PerspectiveAmbiguityStillRefusesWhenFinalLogicalCenterChanges(int x, int low, int high)
    {
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 1600) }, Information(), x << 12, 0, 0);
        Assert.False(result.Supported);
        Assert.Null(result.Center);
        Assert.Null(result.Diagnostics!.ProjectedEighth);
        Assert.Null(result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(low, 0), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(high, 0), result.Diagnostics.ProjectedEighthHigh);
        Assert.Contains("different final", result.UnsupportedReason);
    }

    [Fact]
    public void ReachedFirstFloor245ProjectionIntervalHasOneNativeCameraCenter()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(165);
        // Exact raw position from genuine245 SHA c5bb787330e25eeabfec9cfb925ef576b8dc9fbc5f802b39bce1f7ec7b62f4d3.
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(field.Camera, field.Information!, -9329120, -22734368, 2322432);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraPoint(-2278, -5551, 767), result.Diagnostics!.Anchor);
        Assert.Equal(new FieldNativeCameraPoint(-2121, -911, 9661), result.Diagnostics.Transformed);
        Assert.Equal(9661, result.Diagnostics.ProjectionDepth);
        Assert.Null(result.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(-1205, -518), result.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(-1205, -517), result.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(-150, -64), result.Diagnostics.UnclampedCenter);
        Assert.Equal(new FieldNativeCameraCenter(-24, -64), result.Center);
    }

    [Fact]
    public void ProjectedSaturationAndNegativeDepthUseNativeArithmeticEvenWithForcedZeroBounds()
    {
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-10, 10, 10, -10)] };
        FieldNativeCameraResult transformed = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(32768, 0, 1000) }, information, 0, 0, 0);
        Assert.False(transformed.Supported);
        Assert.Contains("signed-WORD saturation", transformed.UnsupportedReason);
        FieldNativeCameraResult projected = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 100) }, information, 2000 << 12, 0, 0);
        Assert.True(projected.Supported, projected.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(8184, 0), projected.Diagnostics!.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), projected.Center);
        FieldNativeCameraResult ambiguousSaturation = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, 1600) }, information, 16369 << 12, 0, 0);
        Assert.True(ambiguousSaturation.Supported, ambiguousSaturation.UnsupportedReason);
        Assert.Equal(new FieldNativeCameraCenter(8184, 0), ambiguousSaturation.Diagnostics!.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(8184, 0), ambiguousSaturation.Diagnostics.ProjectedEighthLow);
        Assert.Equal(new FieldNativeCameraCenter(8184, 0), ambiguousSaturation.Diagnostics.ProjectedEighthHigh);
        Assert.Equal(new FieldNativeCameraCenter(0, 0), ambiguousSaturation.Center);
        FieldNativeCameraResult negative = FieldNativeCameraProbe.Resolve(Camera() with { Translation = new Vector3(0, 0, -1) }, information, 0, 0, 0);
        Assert.True(negative.Supported, negative.UnsupportedReason);
        Assert.Equal(50, negative.Diagnostics!.ProjectionDepth);
    }

    [Fact]
    public void NegativeDepthUsesZeroThenNearClampAndSaturatesBothProjectedAxes()
    {
        FieldCamera camera = Camera() with { Translation = new Vector3(0, 0, -1), FocalLength = 101 };
        FieldInformation information = Information() with { CameraBounds = [new FieldBounds(-2000, 2000, 2000, -2000)] };
        FieldNativeCameraResult result = FieldNativeCameraProbe.Resolve(camera, information, 2000 << 12, -2000 << 12, 0);
        Assert.True(result.Supported, result.UnsupportedReason);
        Assert.Equal(-1, result.Diagnostics!.Transformed!.Value.Z);
        Assert.Equal(50, result.Diagnostics.ProjectionDepth);
        Assert.Equal(new FieldNativeCameraCenter(8184, -8192), result.Diagnostics.ProjectedEighth);
        Assert.Equal(new FieldNativeCameraCenter(1023, -1024), result.Center);
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
