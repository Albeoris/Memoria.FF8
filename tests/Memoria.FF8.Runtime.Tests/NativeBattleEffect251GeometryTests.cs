using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251GeometryTests
{
    [Fact]
    public void Normalization_excludes_the_last_row_from_extrema_and_writes_only_vertex_W()
    {
        byte[] bytes = Enumerable.Range(0, 36).Select(index => unchecked((byte)(index + 80))).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(30), 100);
        byte[] expected = bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(18), -4096);
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(26), 0);
        BinaryPrimitives.WriteInt16LittleEndian(expected.AsSpan(34), short.MinValue);
        NativeBattleEffectArena result = NativeBattleEffect251Lower.NormalizeObject4FA0(NativeBattleEffectArena.Clear().Write(0, bytes), 3);
        Assert.Equal(expected, result.Read(0, 36));
    }

    [Fact]
    public void Normalization_does_not_replace_a_zero_native_denominator()
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(30), 100);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect251Lower.NormalizeObject4FA0(NativeBattleEffectArena.Clear().Write(0, bytes), 3));
    }
}
