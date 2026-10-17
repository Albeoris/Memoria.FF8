using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfAlternateEulerTests
{
    [Fact]
    public void AlternateEulerUsesYTimesXTimesZWithOriginalAxisSigns()
    {
        NativeBattleActionMatrix alternate = NativeBattleGfMatrices.Euler7015B0(new(1024, 1024, 1024));
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, 4096, 0, -4096, 0 }, alternate.Rotation.ToArray());
        Assert.NotEqual(NativeBattleGfMatrices.Euler701310(new(1024, 1024, 1024)).Rotation.ToArray(), alternate.Rotation.ToArray());
    }

    [Theory]
    [InlineData(-32768, 32767, -32767)]
    [InlineData(-1, 8191, -8192)]
    [InlineData(32767, -32768, 16385)]
    public void ReadsFullSignedWordsAndUsesTheSameRoundedMatrixProducts(short x, short y, short z)
    {
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Euler701310(new(x, 0, 0));
        NativeBattleActionMatrix second = NativeBattleGfMatrices.Euler701310(new(0, y, 0));
        NativeBattleActionMatrix third = NativeBattleGfMatrices.Euler701310(new(0, 0, z));
        NativeBattleActionMatrix expected = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(second, first), third);
        Assert.Equal(expected.Rotation.ToArray(), NativeBattleGfMatrices.Euler7015B0(new(x, y, z)).Rotation.ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BothNativeCallbacksSelectRotationByHeaderAndIgnoreFourthVectorWord(bool secondCallback, bool alternate)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), alternate ? 0x40000u : 0);
        for (int offset = 16; offset < 22; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 1024);
        }
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 30);
        NativeBattleActionMatrix basis = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096);
        NativeBattleActionMatrix first = Compose();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), 0xDEAD);
        NativeBattleActionMatrix changedPadding = Compose();
        short[] expected = alternate ? [4096, 0, 0, 0, 0, 4096, 0, -4096, 0] : [-4096, 0, 0, 0, 0, -4096, 0, -4096, 0];
        Assert.Equal(expected, first.Rotation.ToArray());
        Assert.Equal(first.Rotation.ToArray(), changedPadding.Rotation.ToArray());
        Assert.Equal((110, 220, 330), (first.X, first.Y, first.Z));
        Assert.Equal((first.X, first.Y, first.Z), (changedPadding.X, changedPadding.Y, changedPadding.Z));

        NativeBattleActionMatrix Compose()
        {
            NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
            return secondCallback ? NativeBattleGfMatrices.Compose6C53A0(node, basis, null) : NativeBattleGfMatrices.Compose6C6060(node, basis, null, default);
        }
    }
}
