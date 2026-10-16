using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionMatrixTests
{
    [Theory]
    [InlineData(0, 0, 5000, 110, 220, 2830)]
    [InlineData(5000, 0, 0, 2630, 220, 290)]
    [InlineData(0, 0, -5000, 90, 220, -2230)]
    public void CapturedDirectionTransformsTheLocalAxesAroundTheNativeMidpoint(short dx, short dy, short dz, short x, short y, short z)
    {
        NativeBattleActionMatrix matrix = NativeBattleActionGeometryRules.CaptureMatrix(new(100, 200, 300), new((short)(100 + dx), (short)(200 + dy), (short)(300 + dz)), new(0, 0, 0));
        Assert.Equal((short)4096, matrix.Scale);
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), false));
        NativeBattleActionMatrix restored = JsonSerializer.Deserialize<NativeBattleActionMatrix>(JsonSerializer.Serialize(matrix))!;
        Assert.Equal(NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), true), NativeBattlePoseMatrices.TransformCameraPoint(restored, new(10, 20, 30), true));
    }

    [Fact]
    public void GroupTargetUsesBoundsRatherThanTheMeanAndClearsHeight()
    {
        Assert.Equal(new NativeBattleVector(-1, 0, 3), NativeBattleActionGeometryRules.TargetCenter([new(-9, 90, -3), new(-8, 80, 2), new(6, -50, 10)]));
        Assert.Throws<InvalidDataException>(() => NativeBattleActionGeometryRules.TargetCenter([]));
    }

    [Fact]
    public void CoincidentTargetsUseTheCapturedActorRotationAndZeroScaleKeepsNativeMirrorFallback()
    {
        NativeBattleActionMatrix matrix = NativeBattleActionGeometryRules.CaptureMatrix(new(100, 200, 300), new(100, 200, 300), new(0, 2048, 0));
        Assert.Equal(new NativeBattleVector(90, 220, 270), NativeBattlePoseMatrices.TransformCameraPoint(matrix, new(10, 20, 30), false));
        Assert.Equal(new NativeBattleVector(10, 20, -30), NativeBattlePoseMatrices.TransformCameraPoint(matrix with { Scale = 0 }, new(10, 20, 30), true));
        Assert.Throws<InvalidDataException>(() => (matrix with { Rotation = ImmutableArray<short>.Empty }).Validate());
    }
}
