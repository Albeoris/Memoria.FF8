using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFragmentPacketsTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 216, 160, 108);

    [Theory]
    [InlineData(1024, 0, 0, 0, 0, 4096)]
    [InlineData(0, 1024, 0, 0, 4096, 0)]
    [InlineData(0, 0, 1024, -4096, 0, 0)]
    public void Native56CD50_axis_signs_rotate_the_positive_Y_vector(short x, short y, short z, int expectedX, int expectedY, int expectedZ)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(x, y, z));
        Assert.Equal((expectedX, expectedY, expectedZ), NativeBattleGfMatrices.Rotate56C3D0(rotation, new(0, 4096, 0)));
    }

    [Fact]
    public void Noncommuting_axes_use_X_times_parenthesized_YZ_with_each_native_product_rounded()
    {
        NativeBattleActionMatrix actual = NativeBattleGfMatrices.Euler56CD50(new(300, 700, 1100));
        Assert.Equal(new short[] { -227, -1940, 3600, 3459, -2014, -867, 2181, 2992, 1750 }, actual.Rotation.ToArray());
        NativeBattleActionMatrix x = NativeBattleGfMatrices.Euler56CD50(new(300, 0, 0));
        NativeBattleActionMatrix y = NativeBattleGfMatrices.Euler56CD50(new(0, 700, 0));
        NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 1100));
        NativeBattleActionMatrix wrongAssociation = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(x, y), z);
        Assert.Equal((short)-2015, wrongAssociation.Rotation[4]);
        Assert.Equal((short)2993, wrongAssociation.Rotation[7]);
        Assert.False(actual.Rotation.SequenceEqual(NativeBattleGfMatrices.Euler701310(new(300, 700, 1100)).Rotation));
        Assert.False(actual.Rotation.SequenceEqual(NativeBattleGfMatrices.Euler7015B0(new(300, 700, 1100)).Rotation));
    }

    [Fact]
    public void Physical_task_angles_and_positions_are_signed_WORDs_and_compose_before_any_motion_update()
    {
        byte[] task = Enumerable.Repeat((byte)0xA5, 44).ToArray();
        WriteWord(task, 0x10, -32768);
        WriteWord(task, 0x12, -2);
        WriteWord(task, 0x14, 32767);
        WriteWord(task, 0x20, -32768);
        WriteWord(task, 0x22, 32767);
        WriteWord(task, 0x24, -32768);
        byte[] original = task.ToArray();
        NativeBattleActionMatrix actual = NativeBattleQuezacotlFragmentPackets.Compose6C6660(task.ToImmutableArray(), Identity with { X = 10, Y = 20, Z = 30 });
        Assert.Equal(new short[] { 4096, 0, -6, 0, 4096, 0, 6, 0, 4096 }, actual.Rotation.ToArray());
        Assert.Equal((-32758, 18, 32797), (actual.X, actual.Y, actual.Z));
        Assert.Equal(original, task);
    }

    [Fact]
    public void View_rotation_rounds_translation_before_addition_without_GTE_IR_saturation()
    {
        byte[] task = new byte[44];
        WriteWord(task, 0x10, 2048);
        NativeBattleActionMatrix view = new([1, 0, 0, 3, 0, 0, -3, 0, 0], 10, 20, 30, 4096);
        NativeBattleActionMatrix actual = NativeBattleQuezacotlFragmentPackets.Compose6C6660(task.ToImmutableArray(), view);
        Assert.Equal(view.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((10, 22, 28), (actual.X, actual.Y, actual.Z));
    }

    [Fact]
    public void F3_adapter_preserves_rejected_prefixes_cursor_reuse_depth_order_and_unknown_physical_suffix()
    {
        ImmutableArray<byte> mesh = Mesh();
        List<ushort> depths = [];
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitFlatTriangles572200(mesh, 0, Identity, Projection, 17, 2, depths.Add);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 20, 20, 40 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(new ushort[] { 4096, 4096, 4096, 106, 4096, 4096, 4096, 4096, 4096, 106, 4096, 4096 }, depths);
        Assert.Equal(new[] { true, false, true, false }, batch.Attempts.Select(attempt => attempt.Primitive is not null));
        foreach (NativeBattleGfGeometryAttempt attempt in batch.Attempts)
        {
            Assert.Equal(20, attempt.PacketLength);
            Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(attempt.Primitive is null ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, state));
            if (attempt.Primitive is { } primitive)
            {
                Assert.Equal(272, primitive.Bucket);
                Assert.Equal(0x04000000u, primitive.Words[0]);
                Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
            }
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, 80).ToArray()).MarkUnknown(176, 4) };
        string initial = NativeBattleTestSnapshots.Json(memory);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(168, written.Cursor);
        Assert.Equal(2, written.Ordering.Proxies.Length);
        Assert.Equal(0x04000000u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena!.Read(168, 4)));
        Assert.Equal(0x20ABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(172, 4)));
        Assert.Throws<NotSupportedException>(() => written.Memory.Arena.Read(176, 4));
        Assert.All(written.Memory.Arena.Read(180, 8), value => Assert.Equal((byte)0xA5, value));
        NativeBattleEffectMemory clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(initial);
        Assert.Equal(NativeBattleTestSnapshots.Json(written), NativeBattleTestSnapshots.Json(NativeBattleEffectPacketArena.Append(clone, 128, NativeBattleLogicalOrdering.Clear(), batch)));
    }

    [Fact]
    public void Backfaces_retain_only_the_prefix_but_collective_screen_rejection_keeps_all_XY_stores()
    {
        byte[] reversed = Mesh().ToArray();
        const int firstFace = 8 + 4 * 8 + 4;
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(firstFace + 6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(firstFace + 8), 2);
        NativeBattleGfGeometryAttempt backface = NativeBattleModulePackets.EmitFlatTriangles572200(reversed.ToImmutableArray(), 0, Identity, Projection, 17, 2).Attempts[0];
        Assert.Null(backface.Primitive);
        Assert.All(backface.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        NativeBattleGfGeometryBatch outside = NativeBattleModulePackets.EmitFlatTriangles572200(Mesh(), 0, Identity with { X = 100000 }, Projection, 17, 2);
        Assert.Equal(0, outside.AllocationBytes);
        Assert.Null(outside.Attempts[0].Primitive);
        Assert.All(outside.Attempts[0].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Current_executable_mesh_and_prior_projection_are_required_without_static_substitution()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Null(memory.QuezacotlExecutableMemory);
        ImmutableArray<byte> task = new byte[44].ToImmutableArray();
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, task, Projection));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPackets.Compose6C6660(new byte[43].ToImmutableArray(), Identity));
        ImmutableArray<byte> mesh = Mesh();
        NativeBattleQuezacotlExecutableMemory current = new(new byte[NativeBattleQuezacotlExecutableMemory.Length].ToImmutableArray());
        memory = memory with { QuezacotlExecutableMemory = current.Write(NativeBattleQuezacotlFragmentPackets.MeshAddress, mesh.AsSpan()) };
        NativeBattleEffectMemory knownMemory = memory;
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFragmentPackets.Emit6C6660(knownMemory, task, Projection with { View = null }));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleModulePackets.EmitFlatTriangles572200(mesh, 0, Identity, Projection, 17, 2)), JsonSerializer.Serialize(NativeBattleQuezacotlFragmentPackets.Emit6C6660(memory, task, Projection)));
        byte[] otherGroup = mesh.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(otherGroup.AsSpan(8 + 4 * 8 + 4 + 4 * 12), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.EmitFlatTriangles572200(otherGroup.ToImmutableArray(), 0, Identity, Projection, 17, 2));
    }

    private static ImmutableArray<byte> Mesh()
    {
        const int groups = 8 + 4 * 8;
        byte[] bytes = new byte[groups + 32 + 4 * 12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, groups);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = [new(-64, -64, 0), new(64, -64, 0), new(-64, 64, 0), new(-64, -64, -3990)];
        for (int index = 0; index < vertices.Length; index++)
        {
            WriteWord(bytes, 8 + index * 8, vertices[index].X);
            WriteWord(bytes, 10 + index * 8, vertices[index].Y);
            WriteWord(bytes, 12 + index * 8, vertices[index].Z);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(groups), 4);
        for (int index = 0; index < 4; index++)
        {
            int face = groups + 4 + index * 12;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x20ABCDEF);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4), (ushort)((index & 1) == 0 ? 0 : 6));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xBEEF);
        }
        return bytes.ToImmutableArray();
    }

    private static void WriteWord(byte[] bytes, int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    }
}
