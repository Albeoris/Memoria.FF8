using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22GeometryPacketTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(Identity, 256, 160, 112);

    [Fact]
    public void Hidden_task_does_not_consume_mesh_or_transform_or_projection()
    {
        byte[] payload = new byte[0x27 - 12];
        payload[0x26 - 12] = 4;
        NativeBattleModulePoolTask hidden = new(1, 0, null, 0x89AAE0, payload.ToImmutableArray());
        Assert.Empty(NativeBattleBio22GeometryPackets.Emit89AB90(hidden, default, null!, 341).Attempts);
    }

    [Theory]
    [InlineData(1, 40)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    public void Only_strict_positive_NCLIP_writes_a_packet(int winding, int expected)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBio22GeometryPackets.Emit89AB90(Task(1000), Mesh(winding), Projection, 341);
        Assert.Equal(expected, batch.AllocationBytes);
        Assert.Equal(expected / 40, batch.Attempts.Length);
    }

    [Fact]
    public void Raw_GT3_materials_full_tag_and_retained_tail_are_not_replaced_by_generic_geometry_policy()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBio22GeometryPackets.Emit89AB90(Task(1000), Mesh(1), Projection, 341);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(0x09000000u, primitive.Words[0]);
        Assert.Equal(0x36AABBCCu, primitive.Words[1]);
        Assert.Equal(0x71234567u, primitive.Words[3]);
        Assert.Equal(0xA1B2C3D4u, primitive.Words[4]);
        Assert.Equal(0x2AAABBBBu, primitive.Words[6]);
        Assert.Equal(0xE1F2A3B4u, primitive.Words[7]);
        Assert.Equal(0x1234u, primitive.Words[9]);
        Assert.All(attempt.WriteMask.Take(38), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(38), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(new[] { 1000, 1000, 1000, 0 }, primitive.VertexPayload4);
        Assert.Equal(79, primitive.Bucket);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x100, Enumerable.Repeat((byte)0xA5, 40).ToArray()) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0x100, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, written.Memory.Arena!.Read(0x126, 2));
    }

    [Fact]
    public void Positive_winding_is_kept_at_near_depth_and_outside_viewport_despite_projection_flags()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBio22GeometryPackets.Emit89AB90(Task(1), Mesh(1), Projection, 341);
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(new[] { 1, 1, 1, 0 }, attempt.Primitive!.VertexPayload4);
        Assert.Equal(17, attempt.Primitive.Bucket);
    }

    [Theory]
    [InlineData(-257, 14, -257, -18)]
    [InlineData(-256, 14, -256, -18)]
    [InlineData(-255, 14, -255, -18)]
    [InlineData(-1, 15, -1, -20)]
    [InlineData(0, 15, 0, -20)]
    [InlineData(512, 18, 0, -25)]
    [InlineData(513, 18, 0, -25)]
    public void Deformation_clamps_and_flattens_at_the_actual_signed_boundaries(short y, short x, short expectedY, short z)
    {
        NativeBattleModulePoolTask task = Task(-13).WriteWord(0x1C, 11);
        Assert.Equal(new(x, expectedY, z), NativeBattleBio22GeometryPackets.Transform89AD30(task, new(3, y, -5)));
    }

    [Fact]
    public void Deformation_divides_negative_odd_values_toward_zero_and_wraps_after_world_saturation()
    {
        NativeBattleModulePoolTask task = Task(-13).WriteWord(0x1C, 11);
        Assert.Equal(new NativeBattleVector(7, -1, -6), NativeBattleBio22GeometryPackets.Transform89AD30(task, new(-3, -1, 5)));
        NativeBattleModulePoolTask wrapping = Task(0).WriteWord(0x1C, 0x8000);
        Assert.Equal(new NativeBattleVector(-16387, 0, 0), NativeBattleBio22GeometryPackets.Transform89AD30(wrapping, new(32767, 512, 0)));
        NativeBattleModulePoolTask saturated = Task(short.MinValue).WriteWord(0x1C, 32767).WriteWord(0x1E, 512);
        Assert.Equal(new NativeBattleVector(32767, 0, -32768), NativeBattleBio22GeometryPackets.Transform89AD30(saturated, new(100, 0, -100)));
    }

    [Fact]
    public void Deformed_world_points_use_the_camera_once_and_share_the_raw_GT3_store_policy()
    {
        NativeBattleModulePoolTask task = Task(1000);
        NativeBattleGfGeometryBatch batch = NativeBattleBio22GeometryPackets.Emit89AD30(task, Mesh(1), Projection, 341);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(0x36AABBCCu, primitive.Words[1]);
        Assert.Equal(0x71234567u, primitive.Words[3]);
        Assert.Equal(0x1234u, primitive.Words[9]);
        Assert.Equal(new[] { 1000, 1000, 1000, 0 }, primitive.VertexPayload4);
        Assert.All(attempt.WriteMask.Skip(38), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(NativeBattleModuleProjection.Project45FE10(Identity, NativeBattleBio22GeometryPackets.Transform89AD30(task, new(-40, -40, 0)), Projection).Screen, primitive.Words[2]);
    }

    private static NativeBattleModulePoolTask Task(short z)
    {
        byte[] bytes = new byte[0x98 - 12];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x20 - 12), z);
        foreach (int offset in new[] { 0x30, 0x34, 0x38 })
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset - 12), 4096);
        }
        return new(1, 0, null, 0x89AAE0, bytes.ToImmutableArray());
    }

    private static ImmutableArray<byte> Mesh(int winding)
    {
        byte[] bytes = new byte[80];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        NativeBattleVector[] vertices = [new(-40, -40, 0), new(40, -40, 0), new(0, (short)(40 * winding), 0)];
        if (winding == 0)
        {
            vertices[2] = new(0, -40, 0);
        }
        else if (winding < 0)
        {
            (vertices[0], vertices[1]) = (vertices[1], vertices[0]);
            vertices[2] = new(0, 40, 0);
        }
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), vertices[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), vertices[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), vertices[index].Z);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14 + index * 8), 0xBEEF);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), 1);
        Span<byte> face = bytes.AsSpan(52);
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x36AABBCC);
        BinaryPrimitives.WriteUInt16LittleEndian(face.Slice(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(face.Slice(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.Slice(8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(face.Slice(10), 0x1234);
        BinaryPrimitives.WriteUInt32LittleEndian(face.Slice(12), 0x71234567);
        BinaryPrimitives.WriteUInt32LittleEndian(face.Slice(16), 0x2AAABBBB);
        BinaryPrimitives.WriteUInt32LittleEndian(face.Slice(20), 0xA1B2C3D4);
        BinaryPrimitives.WriteUInt32LittleEndian(face.Slice(24), 0xE1F2A3B4);
        return bytes.ToImmutableArray();
    }
}
