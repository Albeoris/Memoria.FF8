using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90MeshPacketsTests
{
    private static NativeBattleEsunaMeshContext Context => new(0x30, null, null, -100, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);

    [Theory]
    [InlineData(2, 32)]
    [InlineData(3, 40)]
    [InlineData(6, 40)]
    [InlineData(7, 52)]
    public void Textured_acceptance_allocates_polygon_then_mode_and_links_mode_then_polygon(int group, int polygonBytes)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        NativeBattleGfGeometryBatch batch = NativeBattleTonberry90MeshPackets.EmitFace764540(group, Face(group), index => corners[index], Context);
        Assert.Equal(polygonBytes + 12, batch.AllocationBytes);
        Assert.Equal(new[] { 0, polygonBytes, 0 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(new uint[] { 0x02000000, 0xE1000020, 0 }, batch.Attempts[1].Primitive!.Words);
        Assert.All(batch.Attempts[2].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(89);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, batch.AllocationBytes).ToArray()).MarkUnknown(128 + polygonBytes + 8, 4) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(128 + polygonBytes + 12, written.Cursor);
        Assert.Equal(0xE1000020u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(128 + polygonBytes + 4, 4)));
        Assert.Equal(new byte[4], written.Memory.Arena.Read(128 + polygonBytes + 8, 4));
        byte polygonOpcode = (byte)(batch.Attempts[2].Primitive!.Words[1] >> 24);
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        lengths[polygonOpcode] = (byte)(polygonBytes / 4 - 1);
        NativeBattleLogicalOrdering restored = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(written.Ordering))!;
        Assert.Equal(new[] { polygonOpcode, (byte)0xE1 }, restored.Traverse(lengths.ToImmutableArray()).Select(command => command.Opcode));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void Rejected_texture_prefix_never_allocates_mode_or_consumes_fourth_vertex(int group)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch batch = NativeBattleTonberry90MeshPackets.EmitFace764540(group, Face(group), index => index == 3 ? throw new InvalidOperationException("Fourth projection is unreachable.") : corners[index], Context);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(Assert.Single(batch.Attempts).Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, batch.Attempts[0].WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Attempts[0].WriteMask[8]);
    }

    [Fact]
    public void Flat_quad_keeps_native_signed_depth_bias_and_prepared_context_rejects_unproved_UV_mode()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        NativeBattleGfGeometryBatch normal = NativeBattleTonberry90MeshPackets.EmitFace764540(1, Face(1), index => corners[index], Context with { DepthBias = 0 });
        NativeBattleGfGeometryBatch biased = NativeBattleTonberry90MeshPackets.EmitFace764540(1, Face(1), index => corners[index], Context with { DepthBias = -5000 });
        int avsz4 = (4 * 4096 * 0x100) >> 12;
        Assert.Equal(17 + (avsz4 >> 2), normal.Attempts.Single().Primitive!.Bucket);
        Assert.Equal(17, biased.Attempts.Single().Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => NativeBattleTonberry90MeshPackets.EmitFace764540(2, Face(2), index => corners[index], Context with { VOffset = 1 }));
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
    private static byte[] Face(int group)
    {
        byte[] face = new byte[group switch { 1 => 12, 2 => 20, 3 => 24, 6 => 28, _ => 36 }];
        uint opcode = group switch { 1 => 0x28u, 2 => 0x24u, 3 => 0x2Cu, 6 => 0x34u, _ => 0x3Cu };
        BinaryPrimitives.WriteUInt32LittleEndian(face, opcode << 24 | 0x808080);
        if (group != 1)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12340201);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD0403);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
            }
            if (group >= 6)
            {
                int first = group == 6 ? 20 : 24;
                for (int offset = first; offset < face.Length; offset += 4)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset), 0x004080C0);
                }
            }
        }
        return face;
    }
}
