using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291AnimationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RewriteUsesEachCurrentRecordSourceAndOnlyStoresTheFinalWord(bool quarterTurn)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0xFF01);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0x1234);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 0xC001);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(20), 0x4321);
        NativeBattlePandemona291Animation source = new(0x13A0E58, bytes.ToImmutableArray());
        NativeBattlePandemona291Animation actual = quarterTurn ? NativeBattlePandemona291Animations.Rewrite6F2E40(source) : NativeBattlePandemona291Animations.Rewrite6F2E60(source);
        byte[] expected = bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(14), quarterTurn ? (ushort)0xF01 : (ushort)0x1234);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(22), quarterTurn ? (ushort)0xD001 : (ushort)0x4321);
        Assert.Equal(expected, actual.Bytes);
        Assert.Equal(bytes, source.Bytes);
        Assert.Equal(source.Address, actual.Address);
    }
}
