using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlPathModelPacketsTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 128, 160, 108);

    [Fact]
    public void All_four_original_polygon_groups_use_the_same_raw_core_and_explicit_zero_cue()
    {
        NativeBattleQuezacotlPathModelBatch batch = Emit(Geometry());
        Assert.Equal(4, batch.Primitives.Length);
        Assert.Equal(new uint[] { 0x24123456, 0x2C123456, 0x26ABCDEF, 0x2E010203 }, batch.Primitives.Select(packet => packet.Words[1]));
        Assert.Equal(new[] { 8, 10, 8, 10 }, batch.Primitives.Select(packet => packet.Words.Length));
        Assert.All(batch.Primitives, packet => Assert.Equal(81, packet.Bucket));
        Assert.All(batch.Primitives, packet => Assert.Equal(0x12340201u, packet.Words[3]));
        Assert.All(batch.Primitives, packet => Assert.Equal(0x00AB0403u, packet.Words[5]));
        Assert.All(batch.Primitives, packet => Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.PhysicalWriteMask!.Value[30]));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Primitives[1].PhysicalWriteMask!.Value[38]);
    }

    [Fact]
    public void Projected_scratch_uses_eight_bytes_per_vertex_including_actual_clipping_DWORD()
    {
        NativeBattleQuezacotlPathModelBatch batch = Emit(Geometry());
        Assert.Equal(new[] { 0, 8, 16, 24 }, batch.VertexScratchWrites.Select(write => write.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt first = batch.VertexScratchWrites[0];
        Assert.Equal(XY(1180, 764), U32(first.Data, 0));
        Assert.Equal(1024u, U32(first.Data, 4));
        Assert.All(batch.VertexScratchWrites, write =>
        {
            Assert.Null(write.Primitive);
            Assert.Equal(8, write.Data.Length);
            Assert.All(write.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        });
    }

    [Fact]
    public void Rejected_geometry_keeps_all_projection_stores_but_writes_no_common_packets()
    {
        NativeBattleQuezacotlPathModelBatch batch = Emit(Geometry(), Identity with { Z = -1024 });
        Assert.Equal(4, batch.VertexScratchWrites.Length);
        Assert.Empty(batch.Primitives);
        //Near projection places this first vertex left/above the zero-origin viewport: fullDWORD includes both screen clip bits.
        Assert.Equal(0x03020000u, U32(batch.VertexScratchWrites[0].Data, 4));
    }

    [Fact]
    public void Masked_directory_entry_does_not_read_its_body_or_any_bone()
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), int.MaxValue);
        NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(bytes.ToImmutableArray(), _ => throw new InvalidOperationException("Masked bone read."), Projection, 0x808080, 0);
        Assert.Empty(batch.VertexScratchWrites);
        Assert.Empty(batch.Primitives);
    }

    [Fact]
    public void Bone_selector_is_signed_and_selected_group_is_projected_once()
    {
        byte[] bytes = Geometry().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), -1);
        List<short> selectors = [];
        NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(bytes.ToImmutableArray(), selector =>
        {
            selectors.Add(selector);
            return Identity;
        }, Projection, 0x808080, uint.MaxValue);
        Assert.Equal(new short[] { -1 }, selectors);
        Assert.Equal(4, batch.Primitives.Length);
    }

    [Fact]
    public void Every_mesh_restarts_the_same_physical_vertex_scratch_cursor()
    {
        byte[] single = Geometry().ToArray();
        byte[] bytes = new byte[single.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
        single.AsSpan(8).CopyTo(bytes.AsSpan(12));
        NativeBattleQuezacotlPathModelBatch batch = Emit(bytes.ToImmutableArray());
        Assert.Equal(new[] { 0, 8, 16, 24, 0, 8, 16, 24 }, batch.VertexScratchWrites.Select(write => write.ByteOffsetRelative));
        Assert.Equal(8, batch.Primitives.Length);
    }

    [Fact]
    public void Composition_uses_prior_view_then_world_then_every_retained_bone_without_overwriting_world()
    {
        const int skeleton = 0x1000;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        byte[] pointer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(pointer, 0x20DFAB8 + skeleton);
        arena = arena.Write(NativeBattleQuezacotlRig86.Directory, pointer).Write(skeleton, [1]);
        arena = Matrix(arena, 0x68, Identity with { X = 100, Y = 200, Z = 300 });
        arena = Matrix(arena, skeleton + 32, Identity with { X = 10, Y = 20, Z = 30 });
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena };
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null);
        NativeBattleQuezacotlRigWrite written = NativeBattleQuezacotlRig86.ComposePathModel5095B0(memory, effect, Identity with { X = 5, Y = 6, Z = 7 });
        NativeBattleActionMatrix bone = NativeBattleQuezacotlRig86.ReadBoneMatrix(written.Memory, 0);
        Assert.Equal((115, 226, 337), (bone.X, bone.Y, bone.Z));
        Assert.Equal(arena.Read(0x68, 32), written.Memory.Arena!.Read(0x68, 32));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena!.Read(skeleton + 50, 2));
        Assert.Same(effect, written.Effect);
    }

    [Fact]
    public void Missing_projection_unknown_bones_and_malformed_geometry_do_not_create_defaults()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlPathModelPackets.Emit6A79B0(Geometry(), _ => throw new NotSupportedException("Unknown native bone."), Projection, 0, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlPathModelPackets.Emit6A79B0(Geometry(), _ => Identity, Projection with { FocalLength = null }, 0, 1));
        Assert.Throws<InvalidDataException>(() => Emit([]));
        byte[] negative = Geometry().ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(negative.AsSpan(12), -1);
        Assert.Throws<NotSupportedException>(() => Emit(negative.ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => Emit(Geometry().AsSpan().Slice(0, 50).ToArray().ToImmutableArray()));
    }

    [Fact]
    public void JSON_preserves_projected_scratch_and_shared_packet_provenance()
    {
        string json = JsonSerializer.Serialize(Emit(Geometry()));
        NativeBattleQuezacotlPathModelBatch restored = JsonSerializer.Deserialize<NativeBattleQuezacotlPathModelBatch>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
    }

    private static NativeBattleQuezacotlPathModelBatch Emit(ImmutableArray<byte> geometry, NativeBattleActionMatrix? bone = null)
    {
        return NativeBattleQuezacotlPathModelPackets.Emit6A79B0(geometry, _ => bone ?? Identity, Projection, 0x123456, uint.MaxValue);
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[132];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 4);
        short[][] points = [[-100, -100, 1024], [100, -100, 1024], [-100, 100, 1024], [100, 100, 1024]];
        int cursor = 14;
        foreach (short[] point in points)
        {
            foreach (short coordinate in point)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(cursor), coordinate);
                cursor += 2;
            }
        }
        cursor = (cursor + 3) & ~3;
        for (int group = 0; group < 4; group++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + group * 2), 1);
        }
        cursor += 12;
        for (int group = 0; group < 4; group++)
        {
            bool quad = (group & 1) != 0;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor), 0x8000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 6), quad ? (ushort)3 : (ushort)0x0605);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 8), 0x12340201);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x00AB0403);
            if (quad)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 16), 0x0605);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 18), 0x0807);
            }
            int length = quad ? 20 : 16;
            if (group >= 2)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + length), quad ? 0x2E010203u : 0x26ABCDEFu);
                length += 4;
            }
            cursor += length;
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattleEffectArena Matrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix)
    {
        byte[] bytes = new byte[32];
        for (int word = 0; word < 9; word++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(word * 2), matrix.Rotation[word]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), matrix.Z);
        return arena.Write(offset, bytes);
    }

    private static uint U32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));

    private static uint XY(short x, short y) => (ushort)x | unchecked((uint)(ushort)y << 16);
}
