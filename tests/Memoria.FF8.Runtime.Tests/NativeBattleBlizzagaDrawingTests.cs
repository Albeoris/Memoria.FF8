using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzagaDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static ImmutableArray<NativeBattleModuleProjectedVertex> Corners => [new(XY(0, 0), 4096, 0), new(XY(128, 0), 4096, 0), new(XY(0, 128), 4096, 0)];

    [Fact]
    public void Main_companion_preserves_physical_allocation_but_links_windows_companion_then_main()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzagaDrawing.EmitMainFace(Face(6, 64), Face(2), Corners, 32);
        Assert.Equal(96, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 40, 72, 40, 84, 0 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        NativeBattleGeometryPrimitive[] links = batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new uint[] { 0xE2, 0x26, 0xE2, 0x34 }, links.Select(packet => packet.Words[1] >> 24).ToArray());
        Assert.Equal(0x26404040u, links[1].Words[1]);
        Assert.Equal(0x34A0B0C0u, links[3].Words[1]);
        Assert.Equal(new[] { 4096, 4096, 4096, 0 }, links[1].VertexPayload4.ToArray());
        Assert.Equal(links[1].VertexPayload4.ToArray(), links[3].VertexPayload4.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, links[0].VertexPayload4.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, links[2].VertexPayload4.ToArray());
        Assert.All(links, packet => Assert.Equal(272, packet.Bucket));
        Assert.Equal(12, batch.Attempts[2].PacketLength);
        Assert.Equal(0u, links[0].Words[2]);
        Assert.Equal(0u, links[2].Words[2]);
    }

    [Fact]
    public void Main_rejection_and_fractured_zero_companion_keep_the_native_partial_store_boundary()
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> near = Corners.SetItem(0, Corners[0] with { Flags = 0x60000 });
        NativeBattleGfGeometryBatch rejected = NativeBattleBlizzagaDrawing.EmitMainFace(Face(6, 64), Face(2), near, 40);
        Assert.Equal(0, rejected.AllocationBytes);
        Assert.Null(Assert.Single(rejected.Attempts).Primitive);
        NativeBattleGfGeometryBatch fractured = NativeBattleBlizzagaDrawing.EmitMainFace(Face(6), Face(2), Corners, 40);
        Assert.Equal(40, fractured.AllocationBytes);
        Assert.Single(fractured.Attempts);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaDrawing.EmitMainMesh([], [], [], 33, View, Projection));
    }

    [Fact]
    public void Companion_V_wrap_applies_to_all_three_coordinates_and_leaves_U_and_material_words_intact()
    {
        byte[] companion = Face(2).ToArray();
        companion[13] = 250;
        companion[17] = 1;
        companion[11] = 2;
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzagaDrawing.EmitMainFace(Face(6, 64), companion.ToImmutableArray(), Corners, 31);
        NativeBattleGeometryPrimitive packet = batch.Attempts.Single(attempt => attempt.Primitive?.Words[1] >> 24 == 0x26).Primitive!;
        Assert.Equal(246u, packet.Words[3] >> 8 & 255);
        Assert.Equal(253u, packet.Words[5] >> 8 & 255);
        Assert.Equal(254u, packet.Words[7] >> 8 & 255);
        Assert.Equal(0x7788u, packet.Words[3] >> 16);
        Assert.Equal(0x00B4u, packet.Words[5] >> 16);
        Assert.Equal((uint)companion[12], packet.Words[3] & 255);
    }

    [Fact]
    public void Auxiliary_A_accepts_backfaces_for_all_three_groups_and_brackets_even_zero_offsets()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzagaDrawing.AuxiliaryA(Task(0), 48, Resources(), Projection, View, Colors);
        NativeBattleGeometryPrimitive[] polygons = batch.Attempts.Where(attempt => attempt.Primitive is { } primitive && primitive.Words[1] >> 24 != 0xE2).Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(new uint[] { 0x2C, 0x34, 0x3C }, polygons.Select(packet => packet.Words[1] >> 24).ToArray());
        Assert.Equal(new[] { 40, 40, 52 }, polygons.Select(packet => packet.Words.Length * 4).ToArray());
        Assert.Equal(40 + 40 + 52 + 3 * 24, batch.AllocationBytes);
        Assert.Equal(6, batch.Attempts.Count(attempt => attempt.Primitive is { } primitive && primitive.Words[1] >> 24 == 0xE2));
        Assert.All(polygons, polygon => Assert.Equal(4, polygon.VertexPayload4.Length));
    }

    [Fact]
    public void Auxiliary_A_GT3_preserves_three_color_commands_and_uses_aggregate_U_wrap()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzagaDrawing.AuxiliaryA(Task(0), 50, Resources(), Projection, View, Colors);
        NativeBattleGeometryPrimitive packet = batch.Attempts.Single(attempt => attempt.Primitive?.Words[1] >> 24 == 0x34).Primitive!;
        Assert.Equal(0x34A0B0C0u, packet.Words[1]);
        Assert.Equal(0x80112233u, packet.Words[4]);
        Assert.Equal(0x00445566u, packet.Words[7]);
        Assert.Equal(138u, packet.Words[3] & 255);
        Assert.Equal(145u, packet.Words[6] & 255);
        Assert.Equal(146u, packet.Words[9] & 255);
        Assert.Equal(0, packet.VertexPayload4[3]);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaDrawing.AuxiliaryA(Task(0), 71, Resources(), Projection, View, Colors));
    }

    [Fact]
    public void Shared_textured_leaf_preserves_default_zero_window_behavior_and_excludes_fourth_projection_flags()
    {
        NativeBattleEffectMeshPacketContext context = new(0, null, null, null, null, 0, false, null, 17, 2, new(0, 0, new(0, 0, 256, 256), new(0, 128, 128, 64)));
        NativeBattleGfGeometryBatch triangle = NativeBattleEffectMeshPackets.EmitTextured(Face(6).AsSpan(), corner => Corners[corner], false, true, context, null, 0);
        Assert.Equal(40, triangle.AllocationBytes);
        Assert.Single(triangle.Attempts);
        ImmutableArray<NativeBattleModuleProjectedVertex> quad = [.. Corners, new(XY(128, 128), 4096, 0x60000)];
        NativeBattleGfGeometryBatch accepted = NativeBattleEffectMeshPackets.EmitTextured(Face(3).AsSpan(), corner => quad[corner], true, false, context, null, 0, NativeBattleEffectMeshClip.OutsideScreen);
        Assert.Equal(40, accepted.AllocationBytes);
        quad = quad.SetItem(0, quad[0] with { Flags = 0x60000 });
        Assert.Equal(0, NativeBattleEffectMeshPackets.EmitTextured(Face(3).AsSpan(), corner => quad[corner], true, false, context, null, 0, NativeBattleEffectMeshClip.OutsideScreen).AllocationBytes);
    }

    [Fact]
    public void Auxiliary_B_uses_axis_scale_without_primary_orientation_and_preserves_ordinary_G4()
    {
        NativeBattleBlizzagaResources resources = Resources();
        NativeBattleGfGeometryBatch straight = NativeBattleBlizzagaDrawing.AuxiliaryB(Task(0), 34, resources, Projection, View, Colors);
        NativeBattleGfGeometryBatch rotated = NativeBattleBlizzagaDrawing.AuxiliaryB(Task(0).WriteWord(0x10, 1024), 34, resources, Projection, View, Colors);
        Assert.Equal(36, straight.AllocationBytes);
        Assert.Equal(Assert.Single(straight.Attempts).Primitive!.Words.ToArray(), Assert.Single(rotated.Attempts).Primitive!.Words.ToArray());
        Assert.Equal(0x38u, straight.Attempts[0].Primitive!.Words[1] >> 24);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaDrawing.AuxiliaryB(Task(0), 30, resources, Projection, View, Colors));
    }

    [Fact]
    public void Spark_uses_published_full_MAC_translation_without_transforming_local_point_twice()
    {
        NativeBattleModulePoolTask spark = Task(0x6D9DD0).WriteWord(0x10, 10).WriteWord(0x12, 20).WriteWord(0x14, 30);
        NativeBattleActionMatrix published = View with { X = 40010, Y = 20, Z = 50030 };
        NativeBattleModulePacketBatch batch = NativeBattleBlizzagaDrawing.Sprite(spark, Resources(), Projection, View, 0, published);
        Assert.Equal(50030, Assert.Single(batch.Primitives).Primitive.VertexPayload4[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzagaDrawing.Sprite(spark, Resources(), Projection, View, 0));
        Assert.NotEmpty(NativeBattleBlizzagaDrawing.Sprite(Task(0x6D96A0).WriteWord(0xC, 13).WriteWord(0x1A, 4096), Resources(), Projection, View, 0).Primitives);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzagaDrawing.Sprite(Task(0x6D95E0).WriteWord(0xC, 10), Resources(), Projection, View, 0));
    }

    private static NativeBattleModulePoolTask Task(uint callback) => new(1, 0, null, callback, new byte[64].ToImmutableArray());
    private static uint XY(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);

    private static ImmutableArray<byte> Face(int group, byte companionBrightness = 0)
    {
        int length = group switch { 2 => 20, 3 => 24, 5 => 24, 6 => 28, 7 => 36, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte[] face = new byte[length];
        uint command = group switch { 2 => 0x24u, 3 => 0x2Cu, 5 => 0x38u, 6 => 0x34u, _ => 0x3Cu };
        BinaryPrimitives.WriteUInt32LittleEndian(face, command << 24 | 0xA0B0C0);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 2);
        if ((group & 1) != 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 6);
        }
        if (group != 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x778801FA);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00B40201);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x04030302);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0302);
            }
        }
        if (group == 6)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x80112233);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x00445566);
            face[27] = companionBrightness;
        }
        else if (group is 5 or 7)
        {
            int start = group == 5 ? 12 : 24;
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(start), 0x00112233);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(start + 4), 0x00445566);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(start + 8), 0x00778899);
        }
        return face.ToImmutableArray();
    }

    private static ImmutableArray<byte> Mesh(params int[] groups)
    {
        List<byte> bytes = new(new byte[40]);
        byte[] header = bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(header, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 4);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(8 + vertex * 8), (short)((vertex & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(10 + vertex * 8), (short)(vertex < 2 ? -100 : 100));
        }
        bytes = new(header);
        for (int group = 0; group < 8; group++)
        {
            bytes.AddRange(BitConverter.GetBytes(groups.Contains(group) ? 1 : 0));
            if (groups.Contains(group))
            {
                bytes.AddRange(Face(group));
            }
        }
        return bytes.ToImmutableArray();
    }

    private static NativeBattleBlizzagaResources Resources()
    {
        NativeBattleModuleResource Sprite(int count)
        {
            byte[] bytes = new byte[10 + (count + 1) * 2 + 24];
            int offset = bytes.Length - 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
            for (int frame = 0; frame <= count; frame++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 1;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 1;
            bytes[offset + 7] = 0x2C;
            return new(0, 0, 0, 0, bytes.ToImmutableArray());
        }
        return new(new("animation", []), Sprite(10), Sprite(14), new(0, 177, []), new(0, 177, []), new(0, []), new(0, []), new(0, 0, Mesh(3, 6, 7)), new(0, 0, Mesh(5)), 0);
    }
}
