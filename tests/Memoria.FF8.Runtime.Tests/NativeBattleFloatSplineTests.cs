using System.Buffers.Binary;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFloatSplineTests
{
    [Fact]
    public void ThreePointNaturalCurveRetainsItsSinglePrecisionCoefficients()
    {
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear();
        work = Float(Float(Float(work, 0, 0), 4, 1), 8, 2);
        work = Float(Float(Float(work, 68, 0), 72, 10), 76, 0);
        (work, _) = NativeBattleFloatSpline.Prepare5EA4B0(3, work, NativeBattleEffectArena.Clear());
        Assert.Equal(-5f, ReadFloat(work, 140));
        work = NativeBattleFloatSpline.Evaluate5EA630(work, 0, 5, 204);
        Assert.Equal(0f, ReadFloat(work, 204));
        Assert.Equal(6.875f, ReadFloat(work, 212));
        Assert.Equal(10f, ReadFloat(work, 220));
    }

    [Fact]
    public void LinearInterpolationTruncatesWordsAndPreservesPerVertexPadding()
    {
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear();
        points = Point(Point(Point(points, 0, 0, 0, 1), 8, 4, -4, 5), 16, 8, -8, 9);
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 64).ToArray());
        NativeBattleFloatSplineResult result = NativeBattleFloatSpline.Interpolate5EA210(points, 0, 3, 8, output, 0, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(index, Word(result.Output, index * 8));
            Assert.Equal(-index, Word(result.Output, index * 8 + 2));
            Assert.Equal(index + 1, Word(result.Output, index * 8 + 4));
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Output.Read(index * 8 + 6, 2));
        }
    }

    [Fact]
    public void RemainderSampleUsesActualRetainedScratchTail()
    {
        NativeBattleEffectArena points = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 4; index++)
        {
            points = Point(points, index * 8, (short)index, (short)index, (short)index);
        }
        NativeBattleEffectArena work = Float(NativeBattleEffectArena.Clear(), 204 + 7 * 4, 123.75f);
        NativeBattleFloatSplineResult result = NativeBattleFloatSpline.Interpolate5EA210(points, 0, 4, 8, NativeBattleEffectArena.Clear(), 0, work, NativeBattleEffectArena.Clear());
        Assert.Equal(123, Word(result.Output, 7 * 8));
        Assert.Equal(123, Word(result.Output, 7 * 8 + 2));
        Assert.Equal(123, Word(result.Output, 7 * 8 + 4));
        Assert.Equal(123.75f, ReadFloat(result.Work400, 204 + 7 * 4));
    }

    [Fact]
    public void TwoPointCoefficientsConsumeRetainedUnwrittenSlope()
    {
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear();
        work = Float(Float(Float(work, 0, 0), 4, 1), 8, 2);
        work = Float(Float(work, 68, 0), 72, 10);
        NativeBattleEffectArena prior = Float(NativeBattleEffectArena.Clear(), 16, 27);
        (NativeBattleEffectArena coefficients, _) = NativeBattleFloatSpline.Prepare5EA4B0(2, work, prior);
        Assert.Equal(17f, ReadFloat(coefficients, 140));
        Assert.Equal(-17f, ReadFloat(coefficients, 136));
    }

    [Fact]
    public void MissingReachedScratchIsUnsupportedRatherThanInitialized()
    {
        NativeBattleEffectArena work = NativeBattleEffectArena.Clear();
        work = Float(Float(Float(work, 0, 0), 4, 1), 8, 2);
        work = Float(Float(work, 68, 0), 72, 10);
        Assert.ThrowsAny<Exception>(() => NativeBattleFloatSpline.Prepare5EA4B0(2, work, NativeBattleEffectArena.Clear().MarkUnknown(16, 4)));
    }

    private static NativeBattleEffectArena Float(NativeBattleEffectArena arena, int offset, float value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(value));
        return arena.Write(offset, bytes);
    }
    private static float ReadFloat(NativeBattleEffectArena arena, int offset) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4)));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Point(NativeBattleEffectArena arena, int offset, short x, short y, short z)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        return arena.Write(offset, bytes);
    }
}
