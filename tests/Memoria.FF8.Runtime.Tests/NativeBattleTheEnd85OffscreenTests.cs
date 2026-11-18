using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85OffscreenTests
{
    private static readonly BattleSceneVisibleArea Wide = new(-1013.3333333333334, 0, 3573.3333333333335, 1728);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sky_cap_opposite_vertical_sides_reject_before_depth_cue_allocation_or_link(bool quad)
    {
        using IDisposable visible = BattleSceneVisibleArea.Begin(Wide);
        NativeBattleModuleProjectedVertex[] vertices = [Point(1280, 4206, 32953), Point(1280, -8192, 8211), Point(2677, -8192, 8766), Point(-117, -8192, 8766)];
        NativeBattleEsunaMeshContext context = Context(0x70);
        NativeBattleGfGeometryBatch result = Emit(quad, vertices, context);
        NativeBattleGfGeometryAttempt rejected = Assert.Single(result.Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0, result.AllocationBytes);
        Assert.Equal(quad ? 24 : 20, rejected.PacketLength);
        Assert.Equal((quad ? 0x28000000u : 0x20000000u) | 0xC66847u, BinaryPrimitives.ReadUInt32LittleEndian(rejected.Data.AsSpan().Slice(4)));
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            Assert.Equal(vertices[corner].Screen, BinaryPrimitives.ReadUInt32LittleEndian(rejected.Data.AsSpan().Slice(8 + corner * 4)));
        }
        Assert.All(rejected.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Throws<NotSupportedException>(() => Default(quad, vertices, context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Opposite_horizontal_sides_preserve_native_axis_rejection_only_for_the_proved_producer(bool quad)
    {
        using IDisposable visible = BattleSceneVisibleArea.Begin(Wide);
        NativeBattleModuleProjectedVertex[] vertices = [Point(-2000, 400), Point(5000, 400), Point(-2000, 800), Point(5000, 800)];
        Assert.Null(Assert.Single(Emit(quad, vertices, Context(0x30)).Attempts).Primitive);
        Assert.NotNull(Assert.Single(Default(quad, vertices, Context(0x30)).Attempts).Primitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_genuinely_visible_wide_edge_keeps_the_original_packet_and_depth(bool quad)
    {
        using IDisposable visible = BattleSceneVisibleArea.Begin(Wide);
        NativeBattleModuleProjectedVertex[] vertices = [Point(-800, 400), Point(-400, 400), Point(-800, 800), Point(-400, 800)];
        NativeBattleGfGeometryBatch expected = Default(quad, vertices, Context(0x30));
        NativeBattleGfGeometryBatch actual = Emit(quad, vertices, Context(0x30));
        Assert.NotNull(Assert.Single(actual.Attempts).Primitive);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        using IDisposable native = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        Assert.Null(Assert.Single(Emit(quad, vertices, Context(0x30)).Attempts).Primitive);
    }

    private static NativeBattleEsunaMeshContext Context(uint flags)
    {
        return new(flags, null, null, 1024, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
    }

    private static byte[] Face(bool quad)
    {
        byte[] face = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(face, (quad ? 0x28000000u : 0x20000000u) | 0xC66847u);
        return face;
    }

    private static NativeBattleModuleProjectedVertex Point(short x, short y, ushort depth = 2000)
    {
        return new((ushort)x | ((uint)(ushort)y << 16), depth, 0);
    }

    private static NativeBattleGfGeometryBatch Emit(bool quad, NativeBattleModuleProjectedVertex[] vertices, NativeBattleEsunaMeshContext context)
    {
        return quad ? NativeBattleTheEnd85GeometryPackets.EmitF4_785DE0(Face(true), corner => vertices[corner], context) : NativeBattleTheEnd85GeometryPackets.EmitF3_785BC0(Face(false), corner => vertices[corner], context);
    }

    private static NativeBattleGfGeometryBatch Default(bool quad, NativeBattleModuleProjectedVertex[] vertices, NativeBattleEsunaMeshContext context)
    {
        return quad ? NativeBattleRestorationMeshPackets.EmitF4_88E590(Face(true), corner => vertices[corner], context) : NativeBattleRestorationMeshPackets.EmitF3_88E370(Face(false), corner => vertices[corner], context);
    }
}
