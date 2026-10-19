using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291OverlayTests
{
    [Fact]
    public void Gouraud_triangle_and_quad_retain_separate_ZSF_scales_and_real_native_packet_layouts()
    {
        NativeBattleGfGeometryBatch batch = Draw(0x200, 0x300);
        Assert.Equal(64, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.Equal(28, batch.Attempts[0].PacketLength);
        Assert.Equal(36, batch.Attempts[1].PacketLength);
        Assert.Equal(0x06000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[0].Data.AsSpan()));
        Assert.Equal(0x08000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan()));
        Assert.Equal(110, batch.Attempts[0].Primitive!.Bucket);
        Assert.Equal(204, batch.Attempts[1].Primitive!.Bucket);
        Assert.Equal(new[] { 1000, 1000, 1000, 0 }, batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(new[] { 1000, 1000, 1000, 1000 }, batch.Attempts[1].Primitive!.VertexPayload4);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, 0x1000, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(2, written.Ordering.Proxies.Length);
    }

    [Fact]
    public void Reached_triangle_requires_authentic_ZSF3_and_projection_rejected_faces_keep_partial_stores()
    {
        Assert.Throws<NotSupportedException>(() => Draw(null, 0x100));
        NativeBattleGfGeometryBatch batch = Draw(0x155, 0x100, -1000);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        });
    }

    [Fact]
    public void Native_overlay_fade_at_first_age_produces_black_RGB_and_preserves_G3_G4_commands()
    {
        NativeBattleGfGeometryBatch batch = Draw(0x155, 0x100, 1000, true);
        Assert.Equal(0x30000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[0].Data.AsSpan().Slice(4)));
        Assert.Equal(0x38000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan().Slice(4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[0].Data.AsSpan().Slice(12)) & 0xFFFFFF);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan().Slice(28)) & 0xFFFFFF);
    }

    private static NativeBattleGfGeometryBatch Draw(short? zsf3, short zsf4, int depth = 1000, bool fade = false)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, depth, 4096);
        NativeBattleTextureWindow window = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(fade ? 0xF0u : 0x20u, fade ? 0u : null, fade ? 4096 : null, 0, 0, 0, window, window, 17, 2);
        ImmutableArray<byte> geometry = Geometry();
        return NativeBattlePandemona291Surface.EmitGeometry701DD0(geometry, matrix, context, new(matrix with { X = 0, Z = 0 }, 100, 160, 100), new(0, 0, 1f / 4096f, 4503601774854144d), zsf4, offset =>
        {
            ReadOnlySpan<byte> xyz = geometry.AsSpan().Slice(8 + offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
        }, zsf3: zsf3);
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[116];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] points = [new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0), new(100, 100, 0)];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), points[index].Y);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x30808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(66), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x00706050);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0x00403020);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(80), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x38808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(90), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(92), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(94), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(96), 0x00706050);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(100), 0x00403020);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(104), 0x00100804);
        return bytes.ToImmutableArray();
    }
}
