using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84ShaderMeshTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 256, 160, 108);

    [Fact]
    public void Posed_vertex_publication_restarts_each_object_and_preserves_unknown_stack_padding()
    {
        NativeBattleRawMesh first = Mesh(-50);
        NativeBattleRawMesh second = Mesh(-25);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(40, new byte[] { 0xA5, 0x5A });
        NativeBattleAngelWing84ShaderDraw draw = NativeBattleAngelWing84ShaderMesh.Draw([first, second], [Identity with { Y = 12 }], Identity with { Z = 1024 }, Projection, uint.MaxValue, 128, 12, scratch);
        Assert.Equal(36, draw.VertexBytesWritten);
        Assert.Equal(80, draw.Packets.AllocationBytes);
        Assert.Equal((short)-25, BinaryPrimitives.ReadInt16LittleEndian(draw.Vertices.Read(0, 2)));
        Assert.Equal((short)-38, BinaryPrimitives.ReadInt16LittleEndian(draw.Vertices.Read(2, 2)));
        Assert.Throws<NotSupportedException>(() => draw.Vertices.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => draw.Vertices.Read(18, 2));
        Assert.Equal(new byte[] { 0xA5, 0x5A }, draw.Vertices.Read(40, 2));
        Assert.Equal(new[] { 0, 40 }, draw.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(NativeBattleAngelWing84ShaderPackets.Shade(new(-25, -38, 0), 12, 128), draw.Vertices.Read(8, 1)[0]);
    }

    [Fact]
    public void Selected_object_mask_and_independent_restore_keep_next_work_and_packets()
    {
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(6, 2);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(scratch))!;
        NativeBattleAngelWing84ShaderDraw first = Draw(scratch);
        NativeBattleAngelWing84ShaderDraw second = Draw(restored);
        Assert.Equal(40, first.Packets.AllocationBytes);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal((short)-25, BinaryPrimitives.ReadInt16LittleEndian(first.Vertices.Read(0, 2)));

        NativeBattleAngelWing84ShaderDraw Draw(NativeBattleEffectArena work) => NativeBattleAngelWing84ShaderMesh.Draw([Mesh(-50), Mesh(-25)], [Identity], Identity with { Z = 1024 }, Projection, 2, 64, 0, work);
    }

    private static NativeBattleRawMesh Mesh(short x)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0xF000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        return new([new(new(x, -50, 0), 0), new(new(50, -50, 0), 0), new(new(0, 50, 0), 0)], [new(false, polygon.ToImmutableArray())]);
    }
}
