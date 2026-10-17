using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuragaGouraudPacketsTests
{
    private static NativeBattleCuragaMeshContext Context => new(0x30, null, null, null, null, null, 17, 2);

    [Theory]
    [InlineData(false, 28, 0x06000000u, 272)]
    [InlineData(true, 36, 0x08000000u, 273)]
    public void Original_untextured_formats_keep_color_DWORDs_and_AVSZ3_341_or_AVSZ4_256(bool quad, int bytes, uint tag, int bucket)
    {
        NativeBattleGfGeometryBatch batch = Emit(quad, Corners(), Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(bytes, batch.AllocationBytes);
        Assert.Equal(tag, primitive.Words[0]);
        Assert.Equal(bucket, primitive.Bucket);
        Assert.Equal(quad ? 0x38808080u : 0x30808080u, primitive.Words[1]);
        Assert.Equal(0xAA202020u, primitive.Words[3]);
        Assert.Equal(0xBB606060u, primitive.Words[5]);
        Assert.Equal(quad ? new[] { 4096, 4096, 4096, 4096 } : new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Near_and_winding_rejection_write_only_tag_and_first_color_before_secondary_reads(bool quad)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        vertices[0] = vertices[0] with { Flags = 0x60000 };
        NativeBattleGfGeometryBatch batch = Emit(quad, vertices, Context);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        vertices = Corners();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        Assert.Null(Assert.Single(Emit(quad, vertices, Context with { Flags = 0x10 }).Attempts).Primitive);
        Assert.NotNull(Assert.Single(Emit(quad, vertices, Context).Attempts).Primitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_outside_rejection_keeps_screens_but_no_secondary_color_stores(bool quad)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners().Select(vertex => vertex with { Screen = XY(-1, 50) }).ToArray();
        NativeBattleGfGeometryAttempt attempt = Assert.Single(Emit(quad, vertices, Context with { Flags = 0xB0 }).Attempts);
        Assert.Null(attempt.Primitive);
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8 + corner * 8]);
            if (corner != 0)
            {
                Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[4 + corner * 8]);
            }
        }
    }

    [Theory]
    [InlineData(false, 0x32606060u, 0x32303030u)]
    [InlineData(true, 0x3A606060u, 0xCC303030u)]
    public void Shared_depth_cue_uses_modified_triangle_command_and_original_fourth_quad_command(bool quad, uint first, uint second)
    {
        NativeBattleSummonColors constants = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleCuragaMeshContext context = Context with { Flags = 0xB2, Fade = 2048, FarColor = 0x404040 };
        NativeBattleGeometryPrimitive primitive = Assert.Single(Emit(quad, Corners(), context, constants).Attempts).Primitive!;
        Assert.Equal(first, primitive.Words[1]);
        Assert.Equal(second, primitive.Words[3]);
        NativeBattleGeometryPrimitive cleared = Assert.Single(Emit(quad, Corners(), context with { Flags = context.Flags | 8 }, constants).Attempts).Primitive!;
        Assert.Equal(first & 0xFDFFFFFF, cleared.Words[1]);
    }

    [Fact]
    public void Triangle_does_not_read_a_fourth_vertex_and_malformed_quad_command_requires_prior_depth_owner()
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        NativeBattleCuragaMeshPackets.EmitG3_87F960(Face(false), index => index == 3 ? throw new InvalidOperationException() : vertices[index], Context);
        byte[] face = Face(false);
        face[3] = 0x38;
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaMeshPackets.EmitG3_87F960(face, index => vertices[index], Context));
    }

    private static NativeBattleGfGeometryBatch Emit(bool quad, NativeBattleModuleProjectedVertex[] vertices, NativeBattleCuragaMeshContext context, NativeBattleSummonColors? colors = null)
    {
        return quad ? NativeBattleCuragaMeshPackets.EmitG4_87FB90(Face(true), index => vertices[index], context, colors) : NativeBattleCuragaMeshPackets.EmitG3_87F960(Face(false), index => vertices[index], context, colors);
    }

    private static byte[] Face(bool quad)
    {
        byte[] data = new byte[quad ? 24 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(data, quad ? 0x38808080u : 0x30808080u);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), 0xAA202020);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 0xBB606060);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20), 0xCCC0C0C0);
        }
        return data;
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(200, 100), 4096, 0), new(XY(100, 200), 4096, 0), new(XY(200, 200), 4096, 0)];
    private static uint XY(short x, short y) => (ushort)x | unchecked((uint)(ushort)y << 16);
}
