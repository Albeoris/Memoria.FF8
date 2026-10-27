using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleMeshDispatcherTests
{
    private static readonly NativeBattleModuleMeshContext Context = new(0, null, null, null, null, null, 17, 2);
    private static readonly NativeBattleActionMatrix Matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(Matrix, 216, 160, 108);

    [Theory]
    [InlineData(0, 20, 0x20)]
    [InlineData(1, 24, 0x28)]
    [InlineData(2, 32, 0x24)]
    [InlineData(3, 40, 0x2C)]
    [InlineData(4, 28, 0x30)]
    [InlineData(5, 36, 0x38)]
    [InlineData(6, 40, 0x34)]
    [InlineData(7, 52, 0x3C)]
    public void All_eight_original_groups_keep_their_native_packet_extent_command_and_AVSZ(int group, int extent, int opcode)
    {
        NativeBattleGfGeometryAttempt attempt = NativeBattleModulePackets.EmitFace572200(Face(group), Corners(group), group, Context with { Flags = 15 });
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(extent, attempt.PacketLength);
        Assert.Equal((uint)(extent / 4 - 1) << 24, primitive.Words[0]);
        Assert.Equal((uint)opcode, primitive.Words[1] >> 24);
        Assert.Equal((group & 1) == 0 ? 272 : 273, primitive.Bucket);
        Assert.Equal(new[] { 4096, 4096, 4096, (group & 1) == 0 ? 0 : 4096 }, primitive.VertexPayload4.ToArray());
    }

    [Fact]
    public void One_mesh_walk_keeps_all_eight_groups_in_native_order_and_allocates_exactly272_bytes()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(Mesh(), 0, default, Matrix, Projection, Context);
        Assert.Equal(272, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 20, 44, 76, 116, 144, 180, 220 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        Assert.Equal(new uint[] { 0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C }, batch.Attempts.Select(attempt => attempt.Primitive!.Words[1] >> 24).ToArray());
        NativeBattleGfGeometryBatch gf = NativeBattleGfGeometryPackets.EmitBatch7043B0(Mesh(), 0, default, Matrix, Projection, new(0, 0, 0, null, null, 0, 17, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(gf), NativeBattleTestSnapshots.Json(batch));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void Preserved_UV_offset_uses_DWORD_carries_and_packs_the_fourth_UV_from_the_modified_pair(int group)
    {
        byte[] face = Face(group);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x1234FFFE);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x5678FFFF);
        if ((group & 1) == 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0xFFFF);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0xFFFEFFFF);
        }
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitFace572200(face, Corners(group), group, Context with { Flags = 0x1000, UvOffset = 0x00010002 }).Primitive!;
        int stride = (group & 4) == 0 ? 2 : 3;
        Assert.Equal(0x12360000u, primitive.Words[3]);
        Assert.Equal(0x567A0001u, primitive.Words[3 + stride]);
        Assert.Equal(0x00020001u, primitive.Words[3 + 2 * stride]);
        if ((group & 1) != 0)
        {
            Assert.Equal(2u, primitive.Words[3 + 3 * stride]);
        }
        NativeBattleGeometryPrimitive reset = NativeBattleModulePackets.EmitFace572200(face, Corners(group), group, Context with { UvOffset = 0x00010002 }).Primitive!;
        Assert.Equal(0x1234FFFEu, reset.Words[3]);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(7)]
    public void Material_add_has_priority_over_replace_and_is_a_WORD_operation(int group)
    {
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitFace572200(Face(group), Corners(group), group, Context with { Flags = 0xF00, TexturePage = 0xFFFF, Clut = 2 }).Primitive!;
        int secondUv = (group & 4) == 0 ? 5 : 6;
        Assert.Equal(0x10020201u, primitive.Words[3]);
        Assert.Equal(0x00030302u, primitive.Words[secondUv]);
    }

    [Fact]
    public void Near_rejection_preserves_UV_prefix_without_consuming_material_fade_or_fourth_vertex()
    {
        NativeBattleModuleProjectedVertex[] first = Corners(7);
        first[0] = first[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt rejected = NativeBattleModulePackets.EmitFace572200(Face(7), new NoFourth(first), 7, Context with { Flags = 0xFC0 });
        Assert.Null(rejected.Primitive);
        Assert.Equal(0x10000201u, BinaryPrimitives.ReadUInt32LittleEndian(rejected.Data.AsSpan().Slice(12)));
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[48]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[16]);
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitFace572200(Face(7), first, 7, Context with { Flags = 0x1000 }));
        Assert.NotNull(NativeBattleModulePackets.EmitFace572200(Face(0), Corners(0), 0, Context with { Flags = 0x1000 }).Primitive);
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitFace572200(Face(7), Corners(7), 7, Context with { Flags = 0x100 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitFace572200(Face(0), Corners(0), 0, Context with { Flags = 0x40 }, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    [Fact]
    public void Module_UV_and_GF_depth_bias_remain_distinct_shared_core_operands()
    {
        NativeBattleGeometryPrimitive module = NativeBattleModulePackets.EmitFace572200(Face(2), Corners(2), 2, Context with { Flags = 0x1000, UvOffset = 16 }).Primitive!;
        NativeBattleGeometryPrimitive gf = NativeBattleGfGeometryPackets.EmitPolygon7043B0(Face(2), Corners(2), 2, new(0, 0, 0, null, null, 16, 17, 2))!;
        Assert.Equal(272, module.Bucket);
        Assert.Equal(276, gf.Bucket);
        Assert.Equal(0x10000211u, module.Words[3]);
        Assert.Equal(0x10000201u, gf.Words[3]);
    }

    [Fact]
    public void Rejected_prefix_and_known_or_unknown_tail_survive_cursor_reuse_and_independent_JSON_in_both_arenas()
    {
        NativeBattleModuleProjectedVertex[] near = Corners(7);
        near[0] = near[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt rejected = NativeBattleModulePackets.EmitFace572200(Face(7), near, 7, Context);
        NativeBattleGfGeometryAttempt accepted = NativeBattleModulePackets.EmitFace572200(Face(0), Corners(0), 0, Context);
        NativeBattleGfGeometryBatch batch = new([rejected, accepted], 20);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process.ApplyCommonWrite(0, Enumerable.Repeat((byte)0xA5, 64).ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 64).ToImmutableArray());
        process = process.ApplyCommonWrite(42, [0], [NativeBattleGfPacketByteState.Unknown]);
        NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), batch, moduleDraw: 7);
        Assert.Equal(20, common.Process.ByteCursor);
        Assert.Equal(7, Assert.Single(common.Ordering.Proxies).ModuleDraw);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, common.Process.ReadCommonPacket(40, 2));
        Assert.Throws<NotSupportedException>(() => common.Process.ReadCommonPacket(42, 1));
        Assert.Equal(new byte[] { 7, 8, 0, 0 }, common.Process.ReadCommonPacket(48, 4));
        NativeBattleCommonPacketWrite clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleCommonPacketWrite>(NativeBattleTestSnapshots.Json(common))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(common), NativeBattleTestSnapshots.Json(clone));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, 64).ToArray()).MarkUnknown(170, 1) };
        NativeBattleEffectPacketWrite effect = NativeBattleEffectPacketArena.Append(NativeBattleTestSnapshots.Independent(memory), 128, NativeBattleLogicalOrdering.Clear(), batch, moduleDraw: 7);
        Assert.Equal(148, effect.Cursor);
        Assert.Equal(7, Assert.Single(effect.Ordering.Proxies).ModuleDraw);
        Assert.Equal(common.Process.ReadCommonPacket(48, 4), effect.Memory.Arena!.Read(176, 4));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, effect.Memory.Arena.Read(168, 2));
        Assert.Throws<NotSupportedException>(() => effect.Memory.Arena.Read(170, 1));
        NativeBattleEffectPacketWrite restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectPacketWrite>(NativeBattleTestSnapshots.Json(effect))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(effect), NativeBattleTestSnapshots.Json(restored));
        NativeBattleModulePacketBatch transport = new([], 20) { CapturedWrites = batch };
        NativeBattleEffectPacketWrite transported = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), transport, NativeBattleEffectGouraudWrites.FullDword, 7);
        Assert.Equal(NativeBattleTestSnapshots.Json(effect), NativeBattleTestSnapshots.Json(transported));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), transport, NativeBattleEffectGouraudWrites.RgbBytes));
    }

    private static NativeBattleModuleProjectedVertex[] Corners(int group)
    {
        NativeBattleModuleProjectedVertex[] first = [new(100u | 100u << 16, 4096, 0), new(200u | 100u << 16, 4096, 0), new(100u | 200u << 16, 4096, 0)];
        return (group & 1) == 0 ? first : [.. first, new(200u | 200u << 16, 4096, 0)];
    }

    private static byte[] Face(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] commands = [0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C];
        byte[] face = new byte[lengths[group]];
        BinaryPrimitives.WriteUInt32LittleEndian(face, (uint)commands[group] << 24 | 0x404040);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        if ((group & 1) != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 6);
        }
        if ((group & 2) != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x10000201);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00040302);
            if ((group & 1) == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
            }
        }
        if ((group & 4) != 0)
        {
            int offset = (group & 2) == 0 ? 12 : (group & 1) == 0 ? 20 : 24;
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset), 0x11402010);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 4), 0x22804020);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 8), 0x33C06030);
            }
        }
        return face;
    }

    private static ImmutableArray<byte> Mesh()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(40);
        writer.Write(4);
        foreach ((short x, short y) in new (short, short)[] { (-64, -64), (64, -64), (-64, 64), (64, 64) })
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        for (int group = 0; group < 8; group++)
        {
            writer.Write(1);
            writer.Write(Face(group));
        }
        return stream.ToArray().ToImmutableArray();
    }

    private sealed class NoFourth(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index] => index == 3 ? throw new InvalidDataException("The rejected fourth source must not be read.") : vertices[index];
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => throw new InvalidOperationException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
