using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312GeometryTests
{
    [Theory]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Local_tint_preserves_opcode_and_retains_unwritten_cue_and_current_SZ(int group, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffectArena context = Context(mesh, 0x33, 108).MarkUnknown(12, 4).Write(0x20, BitConverter.GetBytes(0x0030307Fu));
        NativeBattleEffect312MeshStep result = NativeBattleEffect312Mesh.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, true);
        Assert.Equal(length, result.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Batch.Attempts).Primitive!;
        Assert.Equal((group == 4 ? 0x32000000u : 0x3A000000u) | 0x0030307Fu, primitive.Words[1]);
        Assert.Equal(group == 4 ? new[] { 777, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.DoesNotContain(true, result.Context.ReadSnapshot(12, 4).Known);
        Assert.Equal(0x0030307Fu, U(result.Context, 0x58));
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), primitive.VertexPayload4);
    }

    [Fact]
    public void Local_zero_area_keeps_only_header_and_source_colors_without_allocating()
    {
        NativeBattleStaticMeshResource mesh = Mesh(4, degenerate: true);
        NativeBattleEffect312MeshStep result = NativeBattleEffect312Mesh.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh, 0x33, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, true);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[0]);
        Assert.All(attempt.WriteMask.Skip(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(0x32808080u, U(result.Context, 0x5C));
        Assert.Equal(0u, U(result.Context, 0x28));
    }

    [Fact]
    public void Rejected_offscreen_quad_still_publishes_fourth_vertex_and_average_GTE_state()
    {
        NativeBattleStaticMeshResource mesh = Mesh(5);
        NativeBattleEffect312MeshStep result = NativeBattleEffect312Mesh.DrawLoaded(Memory(x: 5000), mesh, new(null, 100, 160, 108), Context(mesh, 0x33, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[32]);
        Assert.NotNull(result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Memory.GeometryRegisters.Sz);
    }

    [Fact]
    public void Standard_GT3_preserves_UV_and_color_register_feedback_and_original_face_extent()
    {
        NativeBattleStaticMeshResource mesh = Mesh(6);
        NativeBattleEffectArena context = Context(mesh, 0x33, 88).MarkUnknown(12, 4);
        NativeBattleEffect312MeshStep result = NativeBattleEffect312Mesh.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, false);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(0x09000000u, attempt.Primitive!.Words[0]);
        Assert.Equal(0x36808080u, attempt.Primitive.Words[1]);
        Assert.Equal(0x12340201u, attempt.Primitive.Words[3]);
        Assert.Equal(0x56780403u, attempt.Primitive.Words[6]);
        Assert.Equal(0x0605u, attempt.Primitive.Words[9]);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context, 0x20));
        Assert.DoesNotContain(true, result.Context.ReadSnapshot(12, 4).Known);
    }

    [Fact]
    public void Near_rejection_keeps_UV_prefix_without_loading_fourth_vector()
    {
        NativeBattleStaticMeshResource mesh = Mesh(3);
        NativeBattleEffect312MeshStep result = NativeBattleEffect312Mesh.DrawLoaded(Memory(z: -2000), mesh, new(null, 100, 160, 108), Context(mesh, 0x33, 88), NativeBattleGteColorInterpolation.CodeConstants45F270, false);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[32]);
        Assert.Equal(3, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
    }

    [Fact]
    public void Fade_with_unknown_temporary_operand_fails_instead_of_substituting_zero()
    {
        NativeBattleStaticMeshResource mesh = Mesh(4);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312Mesh.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh, 0xF3, 108).MarkUnknown(12, 4), NativeBattleGteColorInterpolation.CodeConstants45F270, true));
    }

    private static NativeBattleEffectMemory Memory(int z = 2000, int x = 0)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, 0, z, 4096);
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null) with { Sz = [11, 22, 33, 777] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static NativeBattleEffectArena Context(NativeBattleStaticMeshResource mesh, uint flags, int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x20, BitConverter.GetBytes(0x00808080u));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static NativeBattleStaticMeshResource Mesh(int group, bool degenerate = false)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = new byte[8 + 4 * 8 + 8 * 4 + strides[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] xs = [-100, 100, -100, 100];
        short[] ys = [-100, -100, 100, 100];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), xs[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), ys[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), (short)(index + 3));
        }
        int cursor = 40;
        for (int section = 0; section < 8; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), section == group ? 1 : 0);
            cursor += 4;
            if (section != group)
            {
                continue;
            }
            byte command = group switch { 3 => 0x2C, 4 => 0x30, 5 => 0x38, 6 => 0x34, _ => 0x20 };
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)command << 24 | 0x808080u);
            for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), (ushort)(degenerate ? 0 : corner * 2));
            }
            if ((group & 2) != 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 0x0605);
                if ((group & 1) != 0)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 10), 6);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x08070605);
                }
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x12340201);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x56780403);
            }
            if ((group & 4) != 0)
            {
                int start = (group & 2) != 0 ? (group & 1) != 0 ? 24 : 20 : 12;
                for (int corner = 1; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + start + (corner - 1) * 4), 0x00808080);
                }
            }
            cursor += strides[group];
        }
        return new(0, 0x1000, bytes.ToImmutableArray());
    }
}
