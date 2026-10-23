using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMiniPulseCannon286MeshContext108Tests
{
    [Theory]
    [InlineData(2, 0x2030u)]
    [InlineData(2, 0x20F0u)]
    [InlineData(3, 0x2030u)]
    [InlineData(3, 0x20F0u)]
    [InlineData(4, 0x2030u)]
    [InlineData(4, 0x20F0u)]
    [InlineData(5, 0x2030u)]
    [InlineData(5, 0x20F0u)]
    [InlineData(6, 0x2030u)]
    [InlineData(6, 0x20F0u)]
    [InlineData(7, 0x2030u)]
    [InlineData(7, 0x20F0u)]
    public void Selected_six_leaves_publish_current_projection_depth_and_color(int group, uint flags)
    {
        List<int> reads = [];
        var mesh = Mesh(group);
        var actual = Draw(Memory(2000), mesh, Context(flags), offset =>
        {
            reads.Add(offset);
            return (Vertex(offset), null);
        });
        bool quad = (group & 1) != 0;
        var packet = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(packet.Primitive);
        Assert.Equal(quad ? new[] { 0, 8, 16, 24 } : new[] { 0, 8, 16 }, reads);
        Assert.Equal(quad ? 500u : 499u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(quad ? 372u : 371u, U(actual.Context108, 0x40));
        Assert.Equal(110 - (quad ? 0 : 1), primitive.Bucket);
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(actual.Context108, 0x34));
        Assert.Equal(0u, U(actual.Context108, 0x44) & 0x60000);
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(unchecked((uint)-128), U(actual.Context108, 0x18));
        Assert.Equal(0u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        if (flags == 0x20F0)
        {
            Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.NotNull(actual.Memory.ScriptedColorRegisters.Material);
            Assert.NotNull(actual.Memory.ScriptedColorRegisters.Rgb[2]);
        }
        else
        {
            Assert.Null(actual.Memory.ScriptedColorRegisters.Material);
        }
        if (group is 2 or 6)
        {
            int last = group == 2 ? 30 : 38;
            Assert.Equal(NativeBattleGfPacketByteState.Known, packet.WriteMask[last]);
            Assert.Equal((byte)0, packet.Data[last]);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Near_rejected_triangles_retain_native_source_color_without_consuming_cue_bias_or_window(int group)
    {
        var context = Context(0x20F0).MarkUnknown(8, 8).MarkUnknown(24, 4).MarkUnknown(32, 20);
        var actual = Draw(Memory(1), Mesh(group), context, offset => (Vertex(offset), null));
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.Null(packet.Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Equal(Command(group), BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(4)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packet.WriteMask[8]);
        Assert.NotEqual(0u, U(actual.Context108, 0x44) & 0x60000);
        Assert.Throws<NotSupportedException>(() => actual.Context108.Read(0x38, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context108.Read(0x40, 4));
        Assert.Null(actual.Memory.GeometryRegisters!.Otz);
        Assert.Null(actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Active_vertical_window_emits_store_before_polygon_link_then_after_with_same_actual_depth(int group)
    {
        var before = new NativeBattleTextureWindow(0, 0, 256, 256);
        var after = new NativeBattleTextureWindow(256, 128, 256, 128);
        var context = Context(0x2030).Write(34, BitConverter.GetBytes((ushort)96));
        context = Rectangle(context, 36, before);
        context = Rectangle(context, 44, after);
        var actual = Draw(Memory(2000), Mesh(group), context, offset => (Vertex(offset), null));
        Assert.Equal(4, actual.Batch.Attempts.Length);
        Assert.Null(actual.Batch.Attempts[0].Primitive);
        var first = Assert.IsType<NativeBattleGeometryPrimitive>(actual.Batch.Attempts[1].Primitive);
        var polygon = Assert.IsType<NativeBattleGeometryPrimitive>(actual.Batch.Attempts[2].Primitive);
        var last = Assert.IsType<NativeBattleGeometryPrimitive>(actual.Batch.Attempts[3].Primitive);
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(before), first.Words[1]);
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(after), last.Words[1]);
        Assert.Equal(0u, first.Words[2]);
        Assert.Equal(0u, last.Words[2]);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, polygon.VertexPayload4);
        Assert.All(actual.Batch.Attempts[2].WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(polygon.Bucket, first.Bucket);
        Assert.Equal(polygon.Bucket, last.Bucket);
        Assert.Equal((group == 3 ? 40 : 52) + 24, actual.Batch.AllocationBytes);
        int[] uv = group == 3 ? new[] { 13, 21, 29, 37 } : new[] { 13, 25, 37, 49 };
        Assert.All(uv, index => Assert.Equal((byte)218, actual.Batch.Attempts[0].Data[index]));
    }

    [Fact]
    public void Fourth_near_RTPT_admission_is_retained_and_AVSZ4_resets_the_final_flag()
    {
        var actual = Draw(Memory(2000), Mesh(7), Context(0x2030), offset => (offset == 24 ? Vertex(offset) with { Z = -1999 } : Vertex(offset), null));
        var polygon = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(new[] { 2000, 2000, 2000, 1 }, polygon.VertexPayload4);
        Assert.Equal(375u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(247u, U(actual.Context108, 0x40));
        Assert.Equal(0u, actual.Memory.ScriptedColorRegisters!.Flags);
        Assert.Equal(0u, U(actual.Context108, 0x44) & 0x60000);
    }

    [Fact]
    public void Actual_ZSF3_controls_the_context_and_all_ordering_buckets()
    {
        var memory = Memory(2000);
        memory = memory with { ScriptedGraphics = memory.ScriptedGraphics! with { Zsf3 = 1024 } };
        var actual = Draw(memory, Mesh(6), Context(0x2030), offset => (Vertex(offset), (short?)0));
        var polygon = Assert.IsType<NativeBattleGeometryPrimitive>(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(1500u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal(1372u, U(actual.Context108, 0x40));
        Assert.Equal(360, polygon.Bucket);
    }

    [Fact]
    public void Unreached_flat_group_fails_before_selected_rows()
    {
        Assert.Throws<NotSupportedException>(() => Draw(Memory(2000), Mesh(1), Context(0x2030), _ => throw new InvalidOperationException()));
    }

    private static NativeBattleMiniPulseCannon286MeshContext108Step Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context, Func<int, (NativeBattleVector Vertex, short? W)> read) => NativeBattleMiniPulseCannon286MeshContext108.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, read);
    private static NativeBattleVector Vertex(int offset) => offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(0, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException() };
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0x10000000u)).Write(4, BitConverter.GetBytes(0x20000000u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(-128)).Write(28, BitConverter.GetBytes(flags)).Write(32, new byte[4]);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint Command(int group) => (uint)(group switch { 1 => 0x28, 2 => 0x26, 3 => 0x2E, 4 => 0x32, 5 => 0x3A, 6 => 0x36, _ => 0x3E }) << 24 | 0x00112233;
    private static NativeBattleEffectArena Rectangle(NativeBattleEffectArena context, int offset, NativeBattleTextureWindow window) => context.Write(offset, BitConverter.GetBytes(window.X)).Write(offset + 2, BitConverter.GetBytes(window.Y)).Write(offset + 4, BitConverter.GetBytes(window.Width)).Write(offset + 6, BitConverter.GetBytes(window.Height));
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int length = group switch { 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 };
        byte[] bytes = new byte[12 + 32 + length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 1);
        int face = 16 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), Command(group));
        for (int corner = 0; corner < ((group & 1) != 0 ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group is 2 or 3 or 6 or 7)
        {
            if ((group & 1) == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10), 0xFA12);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x1234FA12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x5678FA34);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0xFA78FA56);
            }
        }
        if (group >= 4)
        {
            int start = group == 6 ? 20 : group == 7 ? 24 : 12;
            for (int corner = 1; corner < ((group & 1) != 0 ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + start + (corner - 1) * 4), 0x00445566u + (uint)corner);
            }
        }
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }
}
