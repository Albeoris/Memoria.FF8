using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationMeshPacketsTests
{
    private static NativeBattleEsunaMeshContext Esuna => new(0x30, null, null, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 128, 128), 17, 2);
    private static NativeBattleFullLifeMeshContext FullLife => new(0x30, null, null, null, null, null, 17, 2);
    private static NativeBattleCuragaMeshContext Curaga => new(0x30, null, null, null, null, null, 17, 2);

    [Theory]
    [InlineData((sbyte)1)]
    [InlineData((sbyte)2)]
    public void Esuna_advances_the_actual_generic_WORD_once_per_node_and_wraps_through_JSON(sbyte source)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x34 + 0x2A, 0xAB7C);
        NativeBattleEsunaMeshPreparation first = NativeBattleRestorationMeshPackets.Prepare88E090(task, source, 0, 0, null);
        Assert.Equal((ushort)126, first.Task.ReadWord(0x34 + 0x2A));
        Assert.Equal((ushort)126, first.Context.VOffset);
        Assert.Equal(0x30u, first.Context.Flags);
        Assert.Null(first.Context.FarColor);
        Assert.Null(first.Context.Fade);
        NativeBattleModulePoolTask clone = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(first.Task))!;
        NativeBattleEsunaMeshPreparation second = NativeBattleRestorationMeshPackets.Prepare88E090(clone, source, 1, -20, 0xAABBCC);
        Assert.Equal((ushort)0, second.Task.ReadWord(0x34 + 0x2A));
        Assert.Equal((ushort)0, second.Context.VOffset);
        Assert.Equal(0xC0u, second.Context.Flags);
        Assert.Equal(-20, second.Context.Fade);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitFt3_88E820(Face(2), index => Corners()[index], second.Context with { Flags = 0x30 });
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Single(batch.Attempts);
    }

    [Theory]
    [InlineData((sbyte)0)]
    [InlineData((sbyte)4)]
    [InlineData((sbyte)-1)]
    public void Other_Esuna_sources_preserve_the_counter_and_use_full_windows(sbyte source)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x34 + 0x2A, 0xBEEF);
        NativeBattleEsunaMeshPreparation prepared = NativeBattleRestorationMeshPackets.Prepare88E090(task, source, 1, 0, null);
        Assert.Same(task, prepared.Task);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 256, 256), prepared.Context.WindowAfter);
        Assert.Equal((ushort)0, prepared.Context.VOffset);
    }

    [Theory]
    [InlineData(2, 32, 56)]
    [InlineData(3, 40, 64)]
    [InlineData(7, 52, 76)]
    public void Esuna_aggregate_UV_wrap_preserves_material_words_and_exact_E2_physical_and_LIFO_order(int group, int polygonBytes, int allocation)
    {
        byte[] face = Face(group);
        face[12] = 250;
        face[13] = 10;
        face[16] = 10;
        face[17] = 250;
        NativeBattleEsunaMeshContext context = Esuna with { UOffset = 10, VOffset = 10 };
        NativeBattleGfGeometryBatch batch = Emit(group, face, Corners(), context);
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(new[] { 0, polygonBytes, 0, polygonBytes + 12 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        NativeBattleGeometryPrimitive polygon = batch.Attempts[2].Primitive!;
        int secondUv = group == 7 ? 24 : 20;
        Assert.Equal((byte)132, batch.Attempts[0].Data[12]);
        Assert.Equal((byte)148, batch.Attempts[0].Data[secondUv]);
        Assert.Equal((byte)148, batch.Attempts[0].Data[13]);
        Assert.Equal((byte)132, batch.Attempts[0].Data[secondUv + 1]);
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(batch.Attempts[0].Data.AsSpan().Slice(14)));
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(batch.Attempts[0].Data.AsSpan().Slice(secondUv + 2)));
        Assert.Equal(new uint[] { 0x02000000, 0xE2000000, 0 }, batch.Attempts[1].Primitive!.Words);
        Assert.Equal(new uint[] { 0x02000000, 0xE2000210, 0 }, batch.Attempts[3].Primitive!.Words);
        Assert.All(batch.Attempts[2].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(23);
        memory = memory with { Arena = memory.Arena!.Write(128, Enumerable.Repeat((byte)0xA5, allocation).ToArray()).MarkUnknown(128 + polygonBytes + 8, 4) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 128, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(128 + allocation, written.Cursor);
        Assert.Equal(new byte[4], written.Memory.Arena!.Read(128 + polygonBytes + 8, 4));
        Assert.Equal(new byte[4], written.Memory.Arena.Read(128 + polygonBytes + 20, 4));
        byte[] lengths = new byte[256];
        lengths[0xE2] = 1;
        lengths[(byte)(polygon.Words[1] >> 24)] = (byte)(polygonBytes / 4 - 1);
        NativeBattleLogicalOrdering clone = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(written.Ordering))!;
        ImmutableArray<NativeBattleLogicalCommand> commands = clone.Traverse(lengths.ToImmutableArray());
        Assert.Equal(new byte[] { 0xE2, (byte)(polygon.Words[1] >> 24), 0xE2 }, commands.Select(command => command.Opcode));
        NativeBattlePrimitiveDrawMode mode = new(0) { TextureWindowBits = 0x1234 };
        foreach (NativeBattleLogicalCommand command in commands)
        {
            mode = mode.ExecuteWords(command.Opcode, command.Words);
            if (command.Opcode != 0xE2)
            {
                Assert.Equal(0x210u, mode.TextureWindowBits);
            }
        }
        Assert.Equal(0u, mode.TextureWindowBits);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Rejected_textured_attempts_keep_only_native_prefix_and_never_emit_windows_or_read_fourth(int group)
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch batch = Emit(group, Face(group), corners, Esuna with { UOffset = 10 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[^1]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal((byte)1, attempt.Data[12]);
        if (group == 7)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
        }
    }

    [Fact]
    public void FT3_uses_AVSZ3_341_and_signed_depth_bias_clamp_after_geometry_admission()
    {
        NativeBattleGeometryPrimitive primitive = Assert.Single(Emit(2, Face(2), Corners(), Esuna).Attempts).Primitive!;
        Assert.Equal(272, primitive.Bucket);
        Assert.Equal(new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        Assert.Equal(0x07000000u, primitive.Words[0]);
        Assert.Equal(0x0605u, primitive.Words[7]);
        Assert.Equal(17, Assert.Single(Emit(2, Face(2), Corners(), Esuna with { DepthBias = -2000 }).Attempts).Primitive!.Bucket);
        Assert.Equal(17, Assert.Single(Emit(2, Face(2), Corners(), Esuna with { DepthBias = int.MaxValue }).Attempts).Primitive!.Bucket);
    }

    [Fact]
    public void FT3_post_screen_rejection_preserves_XY_without_late_UV_or_fade_reads()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners().Select(corner => corner with { Screen = XY(-1, 100) }).ToArray();
        NativeBattleGfGeometryAttempt attempt = Assert.Single(Emit(2, Face(2), corners, Esuna with { Flags = 0x70, VOffset = 5 }).Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(XY(-1, 100), Word(attempt, 8));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[24]);
        Assert.Equal((byte)2, attempt.Data[13]);
    }

    [Fact]
    public void UV_addition_zero_extends_the_full_WORD_and_wrap_uses_only_the_dimension_BYTE()
    {
        Assert.Equal(new byte[] { 128, 126 }, NativeBattleEffectTextureWindows.OffsetAxis([1, 255], ushort.MaxValue, 128));
        Assert.Equal(new byte[] { 0, 254 }, NativeBattleEffectTextureWindows.OffsetAxis([1, 255], ushort.MaxValue, 0));
    }

    [Fact]
    public void Restoration_reads_only_its_own_leaf_operands_and_requires_unknown_fade_only_after_admission()
    {
        NativeBattleGfGeometryBatch esuna = Emit(2, Face(2), Corners(), Esuna with { Flags = 0xF30 });
        Assert.Equal(0xABCD0403u, esuna.Attempts[0].Primitive!.Words[5]);
        Assert.Equal(24, NativeBattleRestorationMeshPackets.EmitF4_8846C0(Face(1), index => Corners()[index], FullLife with { Flags = 0x1030 }).AllocationBytes);
        Assert.Throws<NotSupportedException>(() => Emit(2, Face(2), Corners(), Esuna with { Flags = 0x70 }));
        NativeBattleModuleProjectedVertex[] rejected = Corners();
        rejected[0] = rejected[0] with { Flags = 0x20000 };
        Assert.Equal(0, Emit(2, Face(2), rejected, Esuna with { Flags = 0x70 }).AllocationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Full_life_F4_and_Curaga_F4_share_exact_prefix_geometry_color_and_allocation(bool fade)
    {
        uint flags = fade ? 0x71u : 0x35u;
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleFullLifeMeshContext context = FullLife with { Flags = flags, FarColor = 0x404040, Fade = 2048 };
        NativeBattleGfGeometryBatch full = NativeBattleRestorationMeshPackets.EmitF4_8846C0(Face(1), index => Corners()[index], context, constants);
        NativeBattleGfGeometryBatch curaga = NativeBattleCuragaMeshPackets.EmitF4_87F200(Face(1), index => Corners()[index], Curaga with { Flags = flags, FarColor = context.FarColor, Fade = context.Fade }, constants);
        Assert.Equal(JsonSerializer.Serialize(curaga), JsonSerializer.Serialize(full));
        Assert.Equal(24, full.AllocationBytes);
        Assert.Equal(273, full.Attempts[0].Primitive!.Bucket);
        Assert.Equal(fade ? 0x2A606060u : 0x28808080u, full.Attempts[0].Primitive!.Words[1]);
    }

    [Fact]
    public void Flat_quad_rejections_preserve_the_native_prefix_then_screen_stores_and_defer_unknown_fade()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch near = NativeBattleRestorationMeshPackets.EmitF4_8846C0(Face(1), index => index == 3 ? throw new InvalidOperationException() : corners[index], FullLife with { Flags = 0x70 });
        Assert.Equal(0, near.AllocationBytes);
        Assert.All(near.Attempts[0].WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(near.Attempts[0].WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        corners = Corners().Select(vertex => vertex with { Screen = XY(-1, 100) }).ToArray();
        NativeBattleGfGeometryBatch outside = NativeBattleRestorationMeshPackets.EmitF4_8846C0(Face(1), index => corners[index], FullLife with { Flags = 0x70 });
        Assert.Equal(0, outside.AllocationBytes);
        Assert.All(outside.Attempts[0].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationMeshPackets.EmitF4_8846C0(Face(1), index => Corners()[index], FullLife with { Flags = 0x70 }));
    }

    [Fact]
    public void Curaga_FT3_preserves_early_packed_DWORD_addition_and_material_override_before_winding()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners();
        (corners[1], corners[2]) = (corners[2], corners[1]);
        NativeBattleCuragaMeshContext context = Curaga with { Flags = 0x1F00, UvOffset = 0x0101, TexturePage = 2, Clut = 3 };
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaMeshPackets.EmitFt3_87F480(Face(2), index => corners[index], context);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(0x12370302u, Word(batch.Attempts[0], 12));
        Assert.Equal(0xABCF0504u, Word(batch.Attempts[0], 20));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Attempts[0].WriteMask[8]);
    }

    private static NativeBattleGfGeometryBatch Emit(int group, byte[] face, NativeBattleModuleProjectedVertex[] corners, NativeBattleEsunaMeshContext context)
    {
        NativeBattleModuleProjectedVertex Read(int index) => index == 3 && (corners[0].Flags & 0x60000) != 0 ? throw new InvalidOperationException("Fourth near vertex read.") : corners[index];
        return group switch
        {
            2 => NativeBattleRestorationMeshPackets.EmitFt3_88E820(face, Read, context),
            3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, Read, context),
            _ => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, Read, context)
        };
    }

    private static NativeBattleModulePoolTask Task()
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.Esuna;
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(profile.Descriptors[2], profile.Banks[2], 676, 3)]);
        NativeBattleEffectHierarchyAppend task = hierarchy.Append8DC540(0, profile.GenericCallback, 676);
        return task.Hierarchy.Read(task.Address);
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0), new(XY(300, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));

    private static byte[] Face(int group)
    {
        byte[] face = new byte[group switch { 1 => 12, 2 => 20, 3 => 24, _ => 36 }];
        BinaryPrimitives.WriteUInt32LittleEndian(face, group switch { 1 => 0x2A808080u, 2 => 0x26808080u, 3 => 0x2E808080u, _ => 0x3E808080u });
        if (group == 1)
        {
            return face;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD0403);
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x08070605);
        }
        if (group == 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x11223344);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(28), 0x55667788);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(32), 0x99AABBCC);
        }
        return face;
    }
}
