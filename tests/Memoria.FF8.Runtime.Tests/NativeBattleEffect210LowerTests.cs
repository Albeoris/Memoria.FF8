using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210LowerTests
{
    [Fact]
    public void Custom_GT4_inserts_full_reset_then_body_then_half_window_with_actual_depths()
    {
        NativeBattleEffect210CustomMeshStep result = Custom(2000);
        Assert.Equal(76, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 52, 0, 64 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0xE2000000u, result.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0xE2080200u, result.Batch.Attempts[2].Primitive!.Words[1]);
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Batch.Attempts[0].Primitive!.VertexPayload4);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, result.Batch.Attempts[1].Primitive!.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, result.Batch.Attempts[2].Primitive!.VertexPayload4);
        Assert.Equal(0x3E808080u, result.Batch.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0x20808080u, result.Batch.Attempts[1].Primitive!.Words[4]);
        Assert.False(result.Context152.ReadSnapshot(0x78, 1).Known[0]);
    }

    [Fact]
    public void Custom_first_projection_rejection_retains_only_native_tag_and_UV_writes()
    {
        NativeBattleEffect210CustomMeshStep result = Custom(0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        for (int offset = 0; offset < 52; offset++)
        {
            bool written = offset < 4 || offset is >= 12 and < 16 or >= 24 and < 28 or >= 36 and < 40 or >= 48 and < 52;
            Assert.Equal(written ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[offset]);
        }
        Assert.DoesNotContain(true, result.Context152.ReadSnapshot(0x30, 4).Known);
    }

    [Fact]
    public void Custom_quad_accepts_reverse_winding_without_a_native_NCLIP_test()
    {
        NativeBattleEffect210CustomMeshStep result = Custom(2000, reverse: true);
        Assert.Equal(76, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
    }

    [Fact]
    public void Custom_vertex_colors_use_each_current_Z_and_low_IR_bytes_with_source_codes_retained()
    {
        NativeBattleStaticMeshResource mesh = Mesh(7, [-256, 16, 128, 256]);
        NativeBattleEffectArena context = CustomContext(0);
        NativeBattleEffect210CustomMeshStep result = NativeBattleEffect210CustomMesh.DrawLoaded(Memory(2000), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        ImmutableArray<uint> words = result.Batch.Attempts[1].Primitive!.Words;
        Assert.Equal(0x3E000000u, words[1]);
        Assert.Equal(0x20080808u, words[4]);
        Assert.Equal(0x30404040u, words[7]);
        Assert.Equal(0x40808080u, words[10]);
        Assert.Equal(new[] { 1744, 2016, 2128, 2256 }, result.Batch.Attempts[1].Primitive!.VertexPayload4);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Selected_raw_F0_context_reaches_existing_FT4_and_GT4_leaves(int group)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group, [0, 0, 0, 0]);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(0)).Write(24, BitConverter.GetBytes(0)).Write(28, BitConverter.GetBytes(0xF0u)).Write(0x20, new byte[20]);
        context = context.Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)256));
        NativeBattleWindowedMeshContext108Step result = NativeBattleWindowedMeshContext108.DrawLoaded(Memory(2000), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 4, index =>
        {
            ReadOnlySpan<byte> row = mesh.Bytes.AsSpan().Slice(8 + index * 8, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(4))), BinaryPrimitives.ReadInt16LittleEndian(row.Slice(6)));
        });
        Assert.True(result.Batch.AllocationBytes > 0);
        Assert.Contains(result.Batch.Attempts, attempt => attempt.Primitive is not null);
        Assert.Equal(0xF0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Context108.Read(28, 4)));
    }

    [Fact]
    public void Echo_initializer_preserves_header_alignment_gap_and_unwritten_history_masks()
    {
        const int offset = 0x60;
        NativeBattleEffectMemory memory = Memory(0) with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(offset, 264).Write(offset + 133, [0xA5, 0x5A, 0xC3]) };
        NativeBattleOriginalEffectState effect = new(210, 209, new(210, 0x6F6800, 0x5755D0), null, null) { Pools = [] };
        NativeBattleEffect210Frame frame = new(memory, effect, new(9), NativeBattleEffect210State.FromColdProcess());
        NativeBattleEffect210Frame result = NativeBattleEffect210Lower.InitializeEcho6A1230(frame, 0x20DFAB8 + offset);
        byte[] bytes = result.Memory.Arena!.Read(offset, 136);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 1, 255, 0 }, bytes[..7]);
        Assert.All(bytes[7..133], value => Assert.Equal((byte)255, value));
        Assert.Equal(new byte[] { 0xA5, 0x5A, 0xC3 }, bytes[133..136]);
        Assert.DoesNotContain(true, result.Memory.Arena.ReadSnapshot(offset + 136, 128).Known);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Ribbon_uses_selected_color_and_keeps_final_unlinked_half_pair_through_JSON()
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
        NativeBattleEffectArena second = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
        for (int index = 0; index < 32; index++)
        {
            first = Point(first, index * 8, index * 4 - 60, -100, 0);
            second = Point(second, index * 8, index * 4 - 60, 100, 0);
        }
        NativeBattleRibbonPair72Step result = NativeBattleRibbonPair72.DrawLoaded(Memory(2000), first, second, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, 0x3A185820);
        Assert.Equal(2196, result.Batch.AllocationBytes);
        Assert.True(result.Batch.NativeUnlinkedAllocation);
        Assert.Equal(0x3A185820u, result.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(60, result.Batch.Attempts.Length);
        Assert.DoesNotContain(true, result.Temporary64.ReadSnapshot(0, 8).Known);
        NativeBattleRibbonPair72Step restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleRibbonPair72Step>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(result.Batch.AllocationBytes, restored.Batch.AllocationBytes);
        Assert.True(restored.Batch.NativeUnlinkedAllocation);
        Assert.Equal(result.Batch.Attempts[0].Primitive!.Words, restored.Batch.Attempts[0].Primitive!.Words);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    public void Presentation_custom_mesh_preserves_all_stores_and_captures_actual_mutable_depths(int z)
    {
        NativeBattleEffect210CustomMeshStep plain = Draw(null);
        NativeBattleEffect210CustomMeshStep captured = Draw(new(210, 0x100, 0, 123, 9));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        if (z != 0)
        {
            NativeBattleGeometryPrimitive body = captured.Batch.Attempts[1].Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(body.PilotGeometry);
            Assert.Equal(new System.Numerics.Vector3(-100, -100, 1744), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(100, 100, 2256), geometry.D);
            Assert.Equal(new System.Numerics.Vector4(1744, 2016, 2128, 2256), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
            Assert.Null(captured.Batch.Attempts[0].Primitive!.PilotGeometry);
            Assert.Null(captured.Batch.Attempts[2].Primitive!.PilotGeometry);
        }
        else
        {
            Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        }

        NativeBattleEffect210CustomMeshStep Draw(NativeBattlePilotVisualIdentity? identity) => NativeBattleEffect210CustomMesh.DrawLoaded(Memory(z), Mesh(7, [-256, 16, 128, 256]), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), CustomContext(0), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: identity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    public void Presentation_ribbon_keeps_both_passes_full_fifo_and_unlinked_allocation(int z)
    {
        NativeBattleRibbonPair72Step plain = Draw(null);
        NativeBattleRibbonPair72Step captured = Draw(new(210, 0x100, 0, 123, 10));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        if (z != 0)
        {
            Assert.Equal(60, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
            NativeBattlePilotVisualGeometry first = captured.Batch.Attempts[0].Primitive!.PilotGeometry!;
            Assert.Equal(new System.Numerics.Vector3(-60, -100, 2000), first.A);
            Assert.Equal(new System.Numerics.Vector3(-56, 100, 2000), first.D);
            Assert.Equal(first.A, captured.Batch.Attempts[1].Primitive!.PilotGeometry!.A);
            Assert.True(first.UseSourceDepths);
        }
        Assert.True(captured.Batch.NativeUnlinkedAllocation);

        NativeBattleRibbonPair72Step Draw(NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattleEffectArena first = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
            NativeBattleEffectArena second = NativeBattleEffectArena.Clear().MarkUnknown(0, 256);
            for (int index = 0; index < 32; index++)
            {
                first = Point(first, index * 8, index * 4 - 60, -100, 0);
                second = Point(second, index * 8, index * 4 - 60, 100, 0);
            }
            return NativeBattleRibbonPair72.DrawLoaded(Memory(z), first, second, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, 0x3A185820, pilotIdentity: identity);
        }
    }

    private static NativeBattleEffect210CustomMeshStep Custom(int z, bool reverse = false) => NativeBattleEffect210CustomMesh.DrawLoaded(Memory(z), Mesh(7, [0, 0, 0, 0], reverse), new(null, 100, 160, 108), CustomContext(256), NativeBattleGteColorInterpolation.CodeConstants45F270);

    private static NativeBattleEffectArena CustomContext(int bias)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 152).Write(0, BitConverter.GetBytes(0x10000u)).Write(4, BitConverter.GetBytes(0x10008u)).Write(0x12, BitConverter.GetBytes((short)0)).Write(0x38, BitConverter.GetBytes(bias));
        return context.Write(0x14, new byte[16]).Write(0x18, BitConverter.GetBytes((ushort)256)).Write(0x1A, BitConverter.GetBytes((ushort)256)).Write(0x1E, BitConverter.GetBytes((ushort)128)).Write(0x20, BitConverter.GetBytes((ushort)256)).Write(0x22, BitConverter.GetBytes((ushort)128));
    }

    private static NativeBattleStaticMeshResource Mesh(int group, short[] z, bool reverse = false)
    {
        int length = group == 7 ? 36 : 24;
        byte[] bytes = new byte[8 + 32 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        (short X, short Y)[] points = [(-100, -100), (100, -100), (-100, 100), (100, 100)];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + index * 8), z[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), (short)(77 + index));
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40 + group * 4), 1);
        int face = 44 + group * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 7 ? 0x3E808080u : 0x2E808080u);
        int[] indices = reverse ? [0, 2, 1, 3] : [0, 1, 2, 3];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(indices[corner] * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12340000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x01000040);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 20), 0x4000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 22), 0x4040);
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 24), 0x20808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 28), 0x30808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 32), 0x40808080);
        }
        return new(0, 0x10000, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectArena Point(NativeBattleEffectArena arena, int offset, int x, int y, int z) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)x))).Write(offset + 2, BitConverter.GetBytes(unchecked((short)y))).Write(offset + 4, BitConverter.GetBytes(unchecked((short)z)));
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ScriptedLighting = NativeBattleScriptedLightingMemory.Unknown() };
}
