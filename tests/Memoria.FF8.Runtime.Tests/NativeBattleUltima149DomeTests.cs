using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltima149DomeTests
{
    [Theory]
    [InlineData(false, 52, 12)]
    [InlineData(true, 64, 15)]
    public void Compound_quad_preserves_window_draw_mode_depth_bias_and_material(bool gouraud, int length, uint tag)
    {
        byte[] face = Face(gouraud);
        NativeBattleGfGeometryBatch batch = NativeBattleUltima149Dome.EmitQuad(face, Project, gouraud, NativeBattleGteColorInterpolation.CodeConstants45F270, 1);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(length, batch.AllocationBytes);
        Assert.Equal(tag << 24, primitive.Words[0]);
        Assert.Equal(20, primitive.Bucket);
        Assert.Equal(0xE2000218u, primitive.Words[1]);
        Assert.Equal(0xE2000000u, primitive.Words[^2]);
        Assert.Equal(0xE1000220u, primitive.Words[^1]);
        Assert.Equal((byte)130, attempt.Data[17]);
        Assert.Equal((byte)50, attempt.Data[16]);
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(18, 2)));
        Assert.Equal(gouraud ? 0x3E808080u : 0x2E808080u, primitive.Words[2]);
    }

    [Fact]
    public void Zero_area_quad_is_accepted_and_depth_bucket_clamps_before_base()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleUltima149Dome.EmitQuad(Face(false), _ => new(0, 0, 0), false, NativeBattleGteColorInterpolation.CodeConstants45F270, 0);
        Assert.Equal(17, Assert.Single(batch.Attempts).Primitive!.Bucket);
        Assert.Equal(52, batch.AllocationBytes);
    }

    [Fact]
    public void Overflowed_vertical_coordinates_receive_one_shared_wrap_before_BYTE_stores()
    {
        byte[] face = Face(false);
        face[13] = 250;
        NativeBattleGfGeometryAttempt attempt = Assert.Single(NativeBattleUltima149Dome.EmitQuad(face, Project, false, NativeBattleGteColorInterpolation.CodeConstants45F270, 1).Attempts);
        Assert.Equal((byte)130, attempt.Data[17]);
        Assert.Equal((byte)2, attempt.Data[25]);
        Assert.Equal((byte)2, attempt.Data[33]);
        Assert.Equal((byte)2, attempt.Data[41]);
    }

    [Fact]
    public void First_three_flags_reject_before_screen_stores_but_ignore_fourth_flags()
    {
        NativeBattleGfGeometryAttempt rejected = Assert.Single(NativeBattleUltima149Dome.EmitQuad(Face(false), index => Project(index) with { Flags = index == 2 ? 0x60000u : 0 }, false, NativeBattleGteColorInterpolation.CodeConstants45F270, 0).Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[12]);
        NativeBattleGfGeometryBatch accepted = NativeBattleUltima149Dome.EmitQuad(Face(false), index => Project(index) with { Flags = index == 3 ? 0x60000u : 0 }, false, NativeBattleGteColorInterpolation.CodeConstants45F270, 0);
        Assert.NotNull(Assert.Single(accepted.Attempts).Primitive);
    }

    [Theory]
    [InlineData(53, 0x2E808080u)]
    [InlineData(54, 0x2E808080u)]
    [InlineData(62, 0x2E000000u)]
    public void Fade_starts_at_original_frame54_and_keeps_command_BYTE(int frame, uint expected)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleUltima149Dome.EmitQuad(Face(false), Project, false, NativeBattleGteColorInterpolation.CodeConstants45F270, frame);
        Assert.Equal(expected, Assert.Single(batch.Attempts).Primitive!.Words[2]);
    }

    private static NativeBattleModuleProjectedVertex Project(int index)
    {
        uint screen = index switch { 0 => 100u | 100u << 16, 1 => 200u | 100u << 16, 2 => 100u | 200u << 16, _ => 200u | 200u << 16 };
        return new(screen, 64, 0);
    }

    private static byte[] Face(bool gouraud)
    {
        byte[] face = new byte[gouraud ? 36 : 24];
        BinaryPrimitives.WriteUInt32LittleEndian(face, gouraud ? 0x3C808080u : 0x2C808080u);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12347A32);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x56787A3C);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x7A3C7A32);
        if (gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x3C808080);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(28), 0x3C808080);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(32), 0x3C808080);
        }
        return face;
    }
}
