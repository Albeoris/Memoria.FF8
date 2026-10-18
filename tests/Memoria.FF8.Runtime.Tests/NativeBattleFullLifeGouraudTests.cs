using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullLifeGouraudTests
{
    private static NativeBattleFullLifeMeshContext Context => new(0x20, null, null, null, null, null, 17, 2);

    [Theory]
    [InlineData(0x20u, 0x30808080u)]
    [InlineData(0x22u, 0x32808080u)]
    [InlineData(0x2Au, 0x30808080u)]
    [InlineData(0x1020u, 0x30808080u)]
    public void G3_uses_its_command_flags_and_exact_physical_packet_without_textured_operands(uint flags, uint command)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => Corners()[index], Context with { Flags = flags }, byteOffset: 44);
        Assert.Equal(28, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(44, attempt.ByteOffsetRelative);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(272, primitive.Bucket);
        Assert.Equal(new uint[] { 0x06000000, command, XY(100, 100), 0x00402010, XY(300, 100), 0x00102040, XY(100, 300) }, primitive.Words);
        Assert.Equal(new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void G3_fade_reuses_the_current_Gouraud_interpolator_and_requires_its_actual_operands()
    {
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096, 4503601774854144d);
        NativeBattleFullLifeMeshContext context = Context with { Flags = 0xA2, FarColor = 0x404040, Fade = 2048 };
        NativeBattleGfGeometryBatch actual = NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => Corners()[index], context, colors);
        NativeBattleCuragaMeshContext otherContext = new(context.Flags, context.FarColor, context.Fade, null, null, null, context.TableBaseBucket, context.DepthShift);
        NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitG3_87F960(Face(), index => Corners()[index], otherContext, colors);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        ImmutableArray<uint> interpolated = NativeBattleGteColorInterpolation.Gouraud45F4C0([0x32808080, 0x00402010, 0x00102040], false, 2048, 0x404040, colors);
        Assert.Equal(interpolated, actual.Attempts[0].Primitive!.Words.Where((_, index) => index is 1 or 3 or 5));
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => Corners()[index], Context with { Flags = 0xA0 }));
    }

    [Fact]
    public void Near_and_winding_rejection_preserve_only_prefix_and_do_not_require_unknown_fade()
    {
        NativeBattleModuleProjectedVertex[] near = Corners();
        near[0] = near[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch rejected = NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => near[index], Context with { Flags = 0x80 });
        Prefix(rejected);
        NativeBattleModuleProjectedVertex[] reversed = Corners();
        (reversed[1], reversed[2]) = (reversed[2], reversed[1]);
        Prefix(NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => reversed[index], Context with { Flags = 0x80 }));
    }

    [Fact]
    public void Outside_rejection_retains_SXY_stores_but_leaves_later_colors_untouched()
    {
        NativeBattleModuleProjectedVertex[] outside = Corners().Select(vertex => vertex with { Screen = XY(-1, 100) }).ToArray();
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(), index => outside[index], Context with { Flags = 0xA0 });
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        foreach (int offset in new[] { 8, 16, 24 })
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        }
        foreach (int offset in new[] { 12, 20 })
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        }
    }

    [Fact]
    public void Full_life_dispatch_admits_group4_with_original_vertex_offsets()
    {
        byte[] mesh = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 32);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 3);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(16), 200);
        BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(26), 200);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(48), 1);
        Face().CopyTo(mesh, 52);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(56), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(58), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60), 6);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitBatch884370(mesh.ToImmutableArray(), matrix, new(null, 256, 160, 108), Context);
        Assert.Equal(28, batch.AllocationBytes);
        Assert.NotNull(Assert.Single(batch.Attempts).Primitive);
    }

    private static void Prefix(NativeBattleGfGeometryBatch batch)
    {
        Assert.Equal(0, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    private static byte[] Face()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x30808080);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x00402010);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00102040);
        return face;
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
}
