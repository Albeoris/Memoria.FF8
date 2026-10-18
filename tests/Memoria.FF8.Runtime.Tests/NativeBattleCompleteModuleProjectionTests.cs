using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCompleteModuleProjectionTests
{
    [Theory]
    [InlineData(0, -32769, 0x81000000u)]
    [InlineData(0, -32768, 0u)]
    [InlineData(0, 32767, 0u)]
    [InlineData(0, 32768, 0x81000000u)]
    [InlineData(1, -32769, 0x80800000u)]
    [InlineData(1, -32768, 0u)]
    [InlineData(1, 32767, 0u)]
    [InlineData(1, 32768, 0x80800000u)]
    [InlineData(2, -32769, 0x00400000u)]
    [InlineData(2, -32768, 0u)]
    [InlineData(2, 32767, 0u)]
    [InlineData(2, 32768, 0x00400000u)]
    [InlineData(2, 65536, 0x00400000u)]
    public void IrLimitsAreIndependentAndZDoesNotInventSummary(int axis, int value, uint additional)
    {
        NativeBattleActionMatrix matrix = Identity() with { X = axis == 0 ? value : 0, Y = axis == 1 ? value : 0, Z = axis == 2 ? value : 100 };
        NativeBattleCameraProjection projection = new(null, axis == 2 ? (ushort)2 : (ushort)0, 0, 0);
        NativeBattleModuleProjectedVertex partial = NativeBattleModuleProjection.Project45FE10(matrix, new(0, 0, 0), projection);
        NativeBattleModuleProjectedVertex full = NativeBattleModuleProjection.Project45FAA0(matrix, new(0, 0, 0), projection, 0, 0);
        Assert.Equal(partial.Screen, full.Screen);
        Assert.Equal(partial.Depth, full.Depth);
        Assert.Equal(partial.Flags | additional, full.Flags);
        if (axis == 2 && value == 32768)
        {
            Assert.Equal(0x00400000u, full.Flags);
        }
    }

    [Theory]
    [InlineData(-2049, 0x1000u)]
    [InlineData(-2048, 0u)]
    [InlineData(0, 0u)]
    [InlineData(16771072, 0u)]
    [InlineData(16773120, 0u)]
    [InlineData(16775167, 0u)]
    [InlineData(16775168, 0x1000u)]
    [InlineData(int.MaxValue, 0x1000u)]
    [InlineData(int.MinValue, 0x1000u)]
    public void CueFlagUsesNearestEvenBeforeRangeTestAndNeverAddsSummary(int dqb, uint expected)
    {
        NativeBattleModuleProjectedVertex result = NativeBattleModuleProjection.Project45FAA0(Identity() with { Z = 100 }, new(0, 0, 0), new(null, 100, 0, 0), 0, dqb);
        Assert.Equal(expected, result.Flags);
    }

    [Theory]
    [InlineData(-1, 65536)]
    [InlineData(short.MinValue, int.MaxValue)]
    public void DqaIsSignExtendedWordRatherThanUnsignedRegister(short dqa, int dqb)
    {
        NativeBattleModuleProjectedVertex result = NativeBattleModuleProjection.Project45FAA0(Identity() with { Z = 100 }, new(0, 0, 0), new(null, 100, 0, 0), dqa, dqb);
        Assert.Equal(0u, result.Flags);
    }

    [Fact]
    public void ScreenProjectionUsesFullMacRatherThanSaturatedIr()
    {
        NativeBattleActionMatrix matrix = Identity() with { X = 40000, Z = 100 };
        NativeBattleCameraProjection projection = new(null, 100, short.MinValue, 0);
        NativeBattleModuleProjectedVertex result = NativeBattleModuleProjection.Project45FAA0(matrix, new(0, 0, 0), projection, 0, 0);
        Assert.Equal((ushort)8184, unchecked((ushort)result.Screen));
        Assert.Equal((ushort)100, result.Depth);
        Assert.Equal(0x81004000u, result.Flags);
    }

    [Fact]
    public void ExistingNearDepthAndScreenFlagsRemainAndSharedScreenDepthAreIdentical()
    {
        NativeBattleCameraProjection projection = new(null, 256, 160, 116);
        NativeBattleActionMatrix matrix = new(new short[] { 4095, 17, -23, -17, 4095, 9, 23, -9, 4096 }.ToImmutableArray(), 40000, -40000, 100, 4096);
        NativeBattleVector[] points = [new(0, 0, 0), new(-32768, 1234, -32768), new(32767, -25000, 32767), new(200, 300, -100), new(-100, -200, 1000)];
        foreach (NativeBattleVector point in points)
        {
            NativeBattleModuleProjectedVertex partial = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
            NativeBattleModuleProjectedVertex full = NativeBattleModuleProjection.Project45FAA0(matrix, point, projection, -31, 500);
            Assert.Equal(partial.Screen, full.Screen);
            Assert.Equal(partial.Depth, full.Depth);
            Assert.Equal(partial.Flags, full.Flags & partial.Flags);
        }
        NativeBattleModuleProjectedVertex zeroDepth = NativeBattleModuleProjection.Project45FAA0(Identity(), new(0, 0, 0), projection, 0, 0);
        Assert.Equal(0x80060000u, zeroDepth.Flags);
        Assert.Throws<InvalidDataException>(() => NativeBattleModuleProjection.Project45FAA0(Identity(), new(0, 0, 0), new(null, 0, 0, 0), 0, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleProjection.Project45FAA0(Identity(), new(0, 0, 1), new(null, null, 0, 0), 0, 0));
    }

    [Fact]
    public void JsonCurrentInputsProduceSameNextFullFlagResult()
    {
        NativeBattleActionMatrix matrix = Identity() with { X = -32769, Y = 32768, Z = 40000 };
        NativeBattleCameraProjection projection = new(null, 256, 160, 116);
        NativeBattleActionMatrix restoredMatrix = JsonSerializer.Deserialize<NativeBattleActionMatrix>(JsonSerializer.Serialize(matrix))!;
        NativeBattleCameraProjection restoredProjection = JsonSerializer.Deserialize<NativeBattleCameraProjection>(JsonSerializer.Serialize(projection))!;
        NativeBattleModuleProjectedVertex first = NativeBattleModuleProjection.Project45FAA0(matrix, new(12, -34, 56), projection, -123, 0x1234567);
        Assert.Equal(first, NativeBattleModuleProjection.Project45FAA0(restoredMatrix, new(12, -34, 56), restoredProjection, -123, 0x1234567));
        Assert.Equal(first, JsonSerializer.Deserialize<NativeBattleModuleProjectedVertex>(JsonSerializer.Serialize(first)));
    }

    private static NativeBattleActionMatrix Identity() => new(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }.ToImmutableArray(), 0, 0, 0, 4096);
}
