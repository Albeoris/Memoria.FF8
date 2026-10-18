using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaScrollingPacketsTests
{
    [Theory]
    [InlineData(false, false, 40)]
    [InlineData(true, false, 48)]
    [InlineData(false, true, 48)]
    [InlineData(true, true, 60)]
    public void Accepted_faces_keep_inline_window_reset_materials_and_explicit_average_depth(bool quad, bool gouraud, int length)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleShivaScrollingPackets.EmitFace(Face(quad, gouraud), Corner, quad, gouraud, new(0x33, null, 0, 0, 17, 2, new(64, 0, 64, 128)), 0x200, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(length, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(0xE2002218u, Word(attempt, 4));
        Assert.Equal(0xE2000000u, Word(attempt, length - 4));
        Assert.Equal(0xAABB2010u, Word(attempt, 16));
        Assert.Equal(0xCCDD4030u, Word(attempt, gouraud ? 28 : 24));
        Assert.Equal(quad ? 79 : 110, attempt.Primitive!.Bucket);
        Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Near_rejection_writes_only_original_prefix_and_never_projects_fourth_or_consumes_fade(bool gouraud)
    {
        List<int> reads = [];
        NativeBattleGfGeometryBatch batch = NativeBattleShivaScrollingPackets.EmitFace(Face(true, gouraud), Read, true, gouraud, new(0xF3, null, -17, 40, 17, 2, new(64, 0, 64, 128)), 0x155, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(new[] { 0, 1, 2 }, reads);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(0xAABB2010u, Word(attempt, 16));
        Assert.All(attempt.WriteMask.Skip(4).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        if (gouraud)
        {
            Assert.All(attempt.WriteMask.Skip(20).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }

        NativeBattleModuleProjectedVertex Read(int corner)
        {
            reads.Add(corner);
            return Corner(corner) with { Flags = corner == 0 ? 0x20000u : 0 };
        }
    }

    [Fact]
    public void Signed_scroll_changes_the_prefix_before_a_degenerate_triangle_rejects()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleShivaScrollingPackets.EmitFace(Face(true, false), _ => Corner(0), true, false, new(0x33, null, -17, 0, 17, 2, new(64, 0, 64, 128)), 0x155, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0xAABB203Fu, Word(attempt, 16));
        Assert.Equal(0xCCDD405Fu, Word(attempt, 24));
        Assert.All(attempt.WriteMask.Skip(12).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    [Fact]
    public void Viewport_rejection_preserves_all_XY_but_defers_gouraud_colors_and_mode_words()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleShivaScrollingPackets.EmitFace(Face(true, true), corner => Corner(corner) with { Screen = Corner(corner).Screen + 3000 }, true, true, new(0x33, null, 0, 0, 17, 2, new(64, 0, 64, 128)), 0x155, 0x100, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        foreach (int offset in new[] { 12, 24, 36, 48 })
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        }
        foreach (int offset in new[] { 4, 20, 32, 44, 56 })
        {
            Assert.All(attempt.WriteMask.Skip(offset).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        }
    }

    private static NativeBattleModuleProjectedVertex Corner(int corner)
    {
        int x = corner is 1 or 3 ? 200 : 100;
        int y = corner >= 2 ? 200 : 100;
        return new((uint)(x | y << 16), 1000, 0);
    }

    private static byte[] Face(bool quad, bool gouraud)
    {
        byte[] bytes = new byte[gouraud ? quad ? 36 : 28 : quad ? 24 : 20];
        Put(0, gouraud ? quad ? 0x3C808080u : 0x34808080u : quad ? 0x2C808080u : 0x24808080u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0x6050);
        Put(12, 0xAABB2010);
        Put(16, 0xCCDD4030);
        if (quad)
        {
            Put(20, 0x80706050);
        }
        if (gouraud)
        {
            Put(quad ? 24 : 20, 0x00112233);
            Put(quad ? 28 : 24, 0x00445566);
            if (quad)
            {
                Put(32, 0x00778899);
            }
        }
        return bytes;

        void Put(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }

    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
}
