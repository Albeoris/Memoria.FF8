using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStaticMeshBoundsTests
{
    [Fact]
    public void Bounds_use_signed_coordinates_and_eight_byte_stride_without_reading_unused_header_or_padding()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xDEADBEEF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3);
        NativeBattleVector[] vertices = [new(300, -100, 20), new(-400, 500, -600), new(50, -700, 800)];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), vertices[index].Z);
        }
        Assert.Equal(new NativeBattleEffectLocalBounds(new(-400, -700, -600), new(300, 500, 800)), NativeBattleStaticMeshBounds.Read6D6070(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Incomplete_or_nonterminating_native_vertex_directory_is_rejected(uint count)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), count);
        Assert.Throws<InvalidDataException>(() => NativeBattleStaticMeshBounds.Read6D6070(bytes));
    }
}
