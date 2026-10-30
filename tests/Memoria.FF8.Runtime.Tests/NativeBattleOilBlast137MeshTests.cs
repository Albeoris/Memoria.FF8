using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOilBlast137MeshTests
{
    [Theory]
    [InlineData(3, 64, 0x2E)]
    [InlineData(4, 28, 0x32)]
    [InlineData(5, 36, 0x3A)]
    [InlineData(7, 76, 0x3E)]
    public void Four_actual_groups_keep_raw_colors_and_publish_current_GTE_without_reloading_matrix(int group, int allocated, byte opcode)
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleOilBlast137MeshStep result = Draw(group, memory: memory);
        Assert.Equal(allocated, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt polygon = Assert.Single(result.Batch.Attempts, attempt => attempt.Primitive is not null && (attempt.Primitive.Words[1] >> 24) == opcode);
        Assert.Equal(0x112233u, U32(polygon.Data, 4) & 0xFFFFFF);
        Assert.Empty(polygon.Primitive!.VertexPayload4);
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, U32(result.Context84, 0x28));
        Assert.Equal(17 + (checked((int)result.Memory.GeometryRegisters.Otz!.Value) >> 2), polygon.Primitive.Bucket);
        Assert.Equal(memory.GeometryRegisters!.Rotation20, result.Memory.GeometryRegisters.Rotation20);
        Assert.Equal(memory.GeometryRegisters.Translation, result.Memory.GeometryRegisters.Translation);
        Assert.Equal(memory.ScriptedColorRegisters!.Material, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(memory.ScriptedColorRegisters.Rgb, result.Memory.ScriptedColorRegisters.Rgb);
        Assert.Equal(NativeBattleTestSnapshots.Json(memory.PrimitiveProcess), NativeBattleTestSnapshots.Json(result.Memory.PrimitiveProcess));
        Assert.Equal(Address + 8, U32(result.Context84, 0x18));
        Assert.Equal(Address + (uint)Source(group).Bytes.Length, U32(result.Context84, 0x1C));
        Assert.Throws<NotSupportedException>(() => result.Context84.Read(6, 2));
        Assert.Throws<NotSupportedException>(() => result.Context84.Read(0x30, 4));
        byte[] vector = group == 4 ? result.Memory.GeometryRegisters.V2.Select(value => value!.Value).ToArray() : result.Memory.ScriptedColorRegisters!.NormalInput!.Value.ToArray();
        Assert.Equal(group == 4 ? (short)0x3333 : (short)0x4444, BinaryPrimitives.ReadInt16LittleEndian(vector.AsSpan(6)));
        if (group >= 4)
        {
            int color = group == 7 ? 16 : 12;
            Assert.Equal(0xA1224466u, U32(polygon.Data, color));
        }
    }

    [Theory]
    [InlineData(3, 40)]
    [InlineData(7, 52)]
    public void Textured_packets_link_environment_polygon_environment_at_nonsequential_physical_offsets(int group, int polygonBytes)
    {
        NativeBattleOilBlast137MeshStep result = Draw(group);
        Assert.Equal(new int[] { 0, polygonBytes, 0, polygonBytes + 12 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        NativeBattleGfGeometryAttempt[] linked = result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).ToArray();
        Assert.Equal(0xE2000000u, linked[0].Primitive!.Words[1]);
        Assert.Equal(0xE2000010u, linked[2].Primitive!.Words[1]);
        Assert.Equal(new uint[] { 0x02000000, 0xE2000000, 0 }, linked[0].Primitive!.Words.ToArray());
        Assert.All(result.Batch.Attempts[2].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        NativeBattleEffectMemory memory = result.Memory with { Arena = NativeBattleEffectArena.Clear().Write(0x1000, Enumerable.Repeat((byte)0xA5, 96).ToArray()) };
        NativeBattleEffectPacketWrite applied = NativeBattleEffectPacketArena.Append(memory, 0x1000, NativeBattleLogicalOrdering.Clear(), result.Batch);
        Assert.Equal(0x1000 + polygonBytes + 24, applied.Cursor);
        Assert.Equal(new int[] { 0x1000 + polygonBytes, 0x1000, 0x1000 + polygonBytes + 12 }, applied.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset).ToArray());
        Assert.All(applied.Ordering.Proxies, proxy => Assert.Empty(proxy.VertexPayload4));
    }

    [Theory]
    [InlineData(3, 32, 48, 64, 80, 96)]
    [InlineData(7, 32, 48, 64, 80, 96)]
    [InlineData(3, 240, 128, 144, 160, 176)]
    [InlineData(7, 240, 128, 144, 160, 176)]
    public void U_offset_overflow_subtracts_actual_second_window_width_without_UV_word_carry(int group, ushort offset, byte u0, byte u1, byte u2, byte u3)
    {
        NativeBattleOilBlast137MeshStep result = Draw(group, Context(offset));
        NativeBattleGfGeometryAttempt polygon = Assert.Single(result.Batch.Attempts, attempt => attempt.Primitive is not null && attempt.ByteOffsetRelative == 0);
        int[] offsets = group == 3 ? [12, 20, 28, 36] : [12, 24, 36, 48];
        Assert.Equal(new byte[] { u0, u1, u2, u3 }, offsets.Select(index => polygon.Data[index]).ToArray());
        Assert.Equal(new byte[] { 16, 32, 48, 64 }, offsets.Select(index => polygon.Data[index + 1]).ToArray());
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Data.AsSpan().Slice(offsets[0] + 2)));
        Assert.Equal((ushort)0x5678, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Data.AsSpan().Slice(offsets[1] + 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Data.AsSpan().Slice(offsets[3] + 2)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void RTPT_near_rejection_preserves_only_native_prefix_and_never_reads_bad_fourth_vertex_or_UV_controls(int group)
    {
        NativeBattleStaticMeshResource source = Source(group, badFourth: true);
        NativeBattleEffectArena context = Context().MarkUnknown(4, 20);
        NativeBattleOilBlast137MeshStep result = NativeBattleOilBlast137Mesh.Draw(Memory(0), source, context, Projection);
        NativeBattleGfGeometryAttempt prefix = Assert.Single(result.Batch.Attempts);
        Assert.Null(prefix.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, prefix.WriteMask[8]);
        Assert.Equal((short)0x1111, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.NotEqual(0u, U32(result.Context84, 0x2C) & 0x60000);
        Assert.Throws<NotSupportedException>(() => result.Context84.Read(0x28, 4));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
        if (group is 3 or 7)
        {
            Assert.Equal(0x12341010u, U32(prefix.Data, 12));
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Viewport_rejection_runs_AVSZ_and_preserves_context_depth_and_unmodified_prefix_UV(int group)
    {
        NativeBattleOilBlast137MeshStep result = Draw(group, Context(240).MarkUnknown(8, 16), projection: new(null, 100, -300, -300));
        NativeBattleGfGeometryAttempt prefix = Assert.Single(result.Batch.Attempts);
        Assert.Null(prefix.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.NotNull(result.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => result.Context84.Read(0x28, 4));
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[8]);
        if (group is 3 or 7)
        {
            Assert.Equal(0x12341010u, U32(prefix.Data, 12));
        }
        if (group >= 4)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, prefix.WriteMask[group == 7 ? 16 : 12]);
        }
    }

    [Fact]
    public void Back_facing_and_zero_area_faces_are_admitted_without_NCLIP_or_color_shading()
    {
        foreach (bool degenerate in new[] { false, true })
        {
            NativeBattleOilBlast137MeshStep result = NativeBattleOilBlast137Mesh.Draw(Memory(), Source(4, reverse: !degenerate, degenerate: degenerate), Context(), Projection);
            Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
            Assert.Equal(result.Memory.GeometryRegisters!.Otz, U32(result.Context84, 0x28));
        }
    }

    [Fact]
    public void Nonzero_window_offsets_masks_and_JSON_keep_physical_link_order_and_context_unknown_bytes()
    {
        NativeBattleEffectArena context = Context().Write(8, Window(8, 16, 128, 64));
        NativeBattleOilBlast137MeshStep source = Draw(3, context);
        NativeBattleGfGeometryAttempt firstEnvironment = source.Batch.Attempts[1];
        Assert.Equal(0xE2010710u, firstEnvironment.Primitive!.Words[1]);
        NativeBattleOilBlast137MeshStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleOilBlast137MeshStep>(NativeBattleTestSnapshots.Json(source))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(source), NativeBattleTestSnapshots.Json(restored));
        Assert.Throws<NotSupportedException>(() => restored.Context84.Read(0x30, 4));
        Assert.Equal(source.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray(), restored.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
    }

    [Fact]
    public void Empty_directories_do_not_read_GTE_or_unwritten_context_controls()
    {
        byte[] bytes = new byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = null, ScriptedColorRegisters = null, ScriptedGraphics = null };
        NativeBattleOilBlast137MeshStep result = NativeBattleOilBlast137Mesh.Draw(memory, new(0, Address, bytes.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 84).Write(0, BitConverter.GetBytes(Address)), Projection);
        Assert.Empty(result.Batch.Attempts);
        Assert.Same(memory, result.Memory);
        Assert.Equal(Address + 40, U32(result.Context84, 0x1C));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Captured_custom_mesh_preserves_windows_UVs_masks_native_GTE_and_zero_proxy_depths(int group)
    {
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        NativeBattleOilBlast137MeshStep native = NativeBattleOilBlast137Mesh.Draw(Memory(), Source(group), Context(), projection);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleOilBlast137MeshStep captured = NativeBattleOilBlast137Mesh.DrawPresentation(Memory(), Source(group), Context(), projection, pilotIdentity: new(137, 1, 2, 3, 4) { ResourceGeneration = Address });
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        NativeBattleGeometryPrimitive polygon = Assert.Single(captured.Batch.Attempts, attempt => attempt.Primitive is { PilotGeometry: not null }).Primitive!;
        NativeBattlePilotVisualGeometry visual = polygon.PilotGeometry!;
        Assert.Equal((ushort)137, visual.Identity.Effect);
        Assert.True(visual.UseSourceDepths);
        Assert.Equal(System.Numerics.Vector4.Zero, visual.SourceDepths);
        Assert.Empty(polygon.VertexPayload4);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Captured_custom_rejection_leaves_the_bad_fourth_vertex_unread(int group)
    {
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        NativeBattleOilBlast137MeshStep native = NativeBattleOilBlast137Mesh.Draw(Memory(0), Source(group, badFourth: true), Context(), projection);
        NativeBattleOilBlast137MeshStep captured = NativeBattleOilBlast137Mesh.DrawPresentation(Memory(0), Source(group, badFourth: true), Context(), projection, pilotIdentity: new(137, 1, 2, 3, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    private const uint Address = 0x1234000;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleOilBlast137MeshStep Draw(int group, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null, NativeBattleCameraProjection? projection = null) => NativeBattleOilBlast137Mesh.Draw(memory ?? Memory(), Source(group), context ?? Context(), projection ?? Projection);
    private static NativeBattleEffectMemory Memory(int z = 2000)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static NativeBattleEffectArena Context(ushort offset = 0) => NativeBattleEffectArena.Clear().MarkUnknown(0, 84).Write(0, BitConverter.GetBytes(Address)).Write(4, BitConverter.GetBytes(offset)).Write(8, Window(0, 0, 256, 256)).Write(16, Window(0, 0, 128, 256));
    private static byte[] Window(ushort x, ushort y, ushort width, ushort height)
    {
        byte[] bytes = new byte[8];
        ushort[] values = [x, y, width, height];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), values[index]);
        }
        return bytes;
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static NativeBattleStaticMeshResource Source(int group, bool badFourth = false, bool reverse = false, bool degenerate = false)
    {
        int faceBytes = group switch { 3 or 5 => 24, 4 => 20, 7 => 36, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte[] bytes = new byte[40 + 32 + faceBytes];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), vertices[vertex].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), vertices[vertex].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + vertex * 8), vertices[vertex].Z);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + vertex * 8), unchecked((short)((vertex + 1) * 0x1111)));
        }
        int cursor = 40;
        for (int current = 0; current < 8; current++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), current == group ? 1 : 0);
            cursor += 4;
            if (current != group)
            {
                continue;
            }
            uint command = group switch { 3 => 0x2E112233, 4 => 0x32112233, 5 => 0x3A112233, _ => 0x3E112233 };
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), command);
            int count = group == 4 ? 3 : 4;
            for (int corner = 0; corner < count; corner++)
            {
                int selected = degenerate ? 0 : reverse && corner < 3 ? 2 - corner : corner;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), badFourth && corner == 3 ? (ushort)0xFFFE : (ushort)(selected * 2));
            }
            if (group is 3 or 7)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x12341010);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x56782020);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x40403030);
            }
            if (group >= 4)
            {
                int colors = cursor + (group == 7 ? 24 : 12);
                uint[] values = [0xA1224466, 0xB16688AA, 0xC1AACCEE];
                for (int color = 0; color < count - 1; color++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(colors + color * 4), values[color]);
                }
            }
            cursor += faceBytes;
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
