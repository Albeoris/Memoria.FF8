namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect107MatrixTests
{
    [Theory]
    [InlineData(1024, 0, 0, 0, -4096, 0)]
    [InlineData(0, 1024, 0, 4096, 0, 0)]
    [InlineData(0, 0, 1024, 0, 4096, 0)]
    public void Native56CE30UsesTheActualSignedAxes(short x, short y, short z, short expectedX, short expectedY, short expectedZ)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(x, y, z));
        NativeBattleVector input = x != 0 ? new(0, 0, 4096) : y != 0 ? new(0, 0, 4096) : new(4096, 0, 0);
        NativeBattleVector actual = NativeBattleGfMatrices.RotateShort56C4F0(matrix, input);
        Assert.Equal(new NativeBattleVector(expectedX, expectedY, expectedZ), actual);
    }

    [Fact]
    public void Native56CE30AppliesZThenXThenYWithIntermediateNativeRounding()
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(1024, 1024, 0));
        Assert.Equal(new short[] { 0, 4096, 0, 0, 0, -4096, -4096, 0, 0 }, matrix.Rotation.ToArray());
        Assert.NotEqual(NativeBattleGfMatrices.Euler56CD50(new(1024, 1024, 0)).Rotation.ToArray(), matrix.Rotation.ToArray());
    }
    [Fact]
    public void Native56C2F0ComposesActualDwordTranslationAndCameraOffsetAlongWithRotation()
    {
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Euler56CE30(new(0, 1024, 0)) with { X = 146, Y = 1259, Z = 9124 };
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(1024, 0, 0)) with { X = 40000, Y = -500, Z = 1500 };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera, local);
        Assert.Equal(1646, composed.X);
        Assert.Equal(759, composed.Y);
        Assert.Equal(-30876, composed.Z);
        Assert.Equal(new short[] { 0, 4096, 0, 0, 0, -4096, -4096, 0, 0 }, composed.Rotation.ToArray());
        Assert.Equal(40000, local.X);
    }
}
