using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLevelGlyphPacketsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Scroll_overflow_adjusts_every_UVY_once_and_keeps_the_native_mask_link_order(bool gouraud, bool overflow)
    {
        byte[] face = Face(gouraud, overflow ? new byte[] { 250, 10, 20, 30 } : new byte[] { 10, 20, 30, 40 });
        NativeBattleGfGeometryBatch batch = NativeBattleLevelGlyphPackets.EmitFace(face, gouraud, Corner, Context(16), null!, 17, 2, 100);
        int length = gouraud ? 52 : 40;
        Assert.Equal(length + 24, batch.AllocationBytes);
        Assert.Equal(4, batch.Attempts.Length);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(100 + length, batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(100, batch.Attempts[2].ByteOffsetRelative);
        Assert.Equal(100 + length + 12, batch.Attempts[3].ByteOffsetRelative);
        Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan(4, 4)));
        Assert.Equal(NativeBattleEffectTextureWindows.Encode(new(0, 0, 16, 64)), BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[3].Data.AsSpan(4, 4)));
        byte[] expected = overflow ? [202, 218, 228, 238] : [26, 36, 46, 56];
        int stride = gouraud ? 12 : 8;
        for (int corner = 0; corner < 4; corner++)
        {
            Assert.Equal(expected[corner], batch.Attempts[0].Data[13 + corner * stride]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Zero_scroll_still_emits_both_masks_and_does_not_reject_reverse_winding(bool gouraud)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleLevelGlyphPackets.EmitFace(Face(gouraud, [10, 20, 30, 40]), gouraud, Corner, Context(0), null!, 17, 2);
        Assert.Equal((gouraud ? 52 : 40) + 24, batch.AllocationBytes);
        Assert.Equal(3, batch.Attempts.Count(attempt => attempt.Primitive is not null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initial_projection_rejection_preserves_header_material_and_UV_stores_without_reading_corner_four(bool gouraud)
    {
        NativeBattleModuleProjectedVertex Read(int corner)
        {
            Assert.InRange(corner, 0, 2);
            return Corner(corner) with { Flags = 0x20000 };
        }
        NativeBattleGfGeometryBatch batch = NativeBattleLevelGlyphPackets.EmitFace(Face(gouraud, [10, 20, 30, 40]), gouraud, Read, Context(16), null!, 17, 2);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Single(batch.Attempts);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, batch.Attempts[0].WriteMask[0]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, batch.Attempts[0].WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Attempts[0].WriteMask[8]);
    }

    [Theory]
    [InlineData(false, 42)]
    [InlineData(true, 17)]
    public void Only_the_second_family_applies_its_signed_depth_bias_and_native_lower_bound(bool applyDepthBias, int bucket)
    {
        NativeBattleEffectArena context = Context(0).Write(0x18, BitConverter.GetBytes(-200));
        NativeBattleGfGeometryBatch batch = NativeBattleLevelGlyphPackets.EmitFace(Face(false, [10, 20, 30, 40]), false, Corner, context, null!, 17, 2, applyDepthBias: applyDepthBias);
        Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(bucket, attempt.Primitive!.Bucket));
    }

    private static NativeBattleModuleProjectedVertex Corner(int corner)
    {
        (int x, int y) = corner switch
        {
            0 => (100, 100),
            1 => (100, 200),
            2 => (200, 100),
            _ => (200, 200)
        };
        return new(unchecked((uint)(ushort)x | (uint)(ushort)y << 16), 400, 0);
    }

    private static NativeBattleEffectArena Context(ushort scroll)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().Write(0x22, BitConverter.GetBytes(scroll));
        context = context.Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        return context.Write(0x30, BitConverter.GetBytes((ushort)16)).Write(0x32, BitConverter.GetBytes((ushort)64));
    }

    private static byte[] Face(bool gouraud, byte[] y)
    {
        byte[] face = new byte[gouraud ? 36 : 24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, gouraud ? 0x3C808080u : 0x2C808080u);
        face[13] = y[0];
        face[17] = y[1];
        face[21] = y[2];
        face[23] = y[3];
        return face;
    }
}
