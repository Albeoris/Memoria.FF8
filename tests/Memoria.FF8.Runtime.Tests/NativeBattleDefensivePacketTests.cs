using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDefensivePacketTests
{
    [Fact]
    public void G3_uses_original_AVSZ3_three_depths_and_cyclic_color_FIFO_command()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x30804020);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x07112233);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x09445566);
        NativeBattleProjectedVertex[] vertices = [new(Screen(100, 100), 1024), new(Screen(200, 100), 1024), new(Screen(100, 200), 1024)];

        NativeBattleGeometryPrimitive packet = NativeBattleModulePackets.EmitGouraudTriangle572BE0(face, vertices, 0xF3, 2048, 17, 2)!;

        Assert.Equal(80, packet.Bucket);
        Assert.Equal(new uint[] { 0x6000000, 0x32402010, Screen(100, 100), 0x32081119, Screen(200, 100), 0x32222A33, Screen(100, 200) }, packet.Words.ToArray());
        Assert.Equal(new[] { 1024, 1024, 1024, 0 }, packet.VertexPayload4.ToArray());
        NativeBattleGeometryPrimitive opaque = NativeBattleModulePackets.EmitGouraudTriangle572BE0(face, vertices, 0xA, 0, 17, 2)!;
        Assert.Equal(0x30804020u, opaque.Words[1]);
        Assert.Equal(0x07112233u, opaque.Words[3]);
        Assert.Equal(0x09445566u, opaque.Words[5]);
        Assert.Null(NativeBattleModulePackets.EmitGouraudTriangle572BE0(face, [vertices[0], vertices[2], vertices[1]], 0, 0, 17, 2));
        Assert.NotNull(NativeBattleModulePackets.EmitGouraudTriangle572BE0(face, [vertices[0], vertices[2], vertices[1]], 0x20, 0, 17, 2));
        vertices[1] = vertices[1] with { DepthAndClip = 0x20000 | 1024 };
        Assert.Null(NativeBattleModulePackets.EmitGouraudTriangle572BE0(face, vertices, 0x20, 0, 17, 2));
    }

    private static uint Screen(int x, int y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
