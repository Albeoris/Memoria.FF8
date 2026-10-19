using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleProjectionIr2Tests
{
    [Theory]
    [InlineData(-40000, -32768, true)]
    [InlineData(-32768, -32768, false)]
    [InlineData(12345, 12345, false)]
    [InlineData(32767, 32767, false)]
    [InlineData(40000, 32767, true)]
    public void Ir2IsSignedLmZeroCameraYBeforeScreenScaling(int y, int expected, bool saturated)
    {
        NativeBattleActionMatrix matrix = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, y, 4000, 4096);
        NativeBattleCameraProjection projection = new(null, 256, 160, 104);
        NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, new(0, 0, 0), projection, 0, 0);
        Assert.Equal(expected, result.Ir2);
        Assert.Equal(saturated, (result.Projected.Flags & 0x00800000) != 0);
        Assert.Equal((ushort)4000, result.Projected.Depth);
        Assert.NotEqual(result.Ir2, unchecked((short)(result.Projected.Screen >> 16)));
        Assert.Equal(NativeBattleModuleProjection.Project45FAA0(matrix, new(0, 0, 0), projection, 0, 0), result.Projected);
    }
}
