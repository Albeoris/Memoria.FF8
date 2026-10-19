using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice169Tests
{
    [Fact]
    public void Landing_morph_uses_integer_difference_shift_and_preserves_actual_fourth_words()
    {
        byte[] first = new byte[8 + 73 * 8];
        byte[] second = new byte[73 * 8];
        byte[] output = new byte[73 * 8];
        BinaryPrimitives.WriteInt16LittleEndian(first.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(6), 0xCAFE);
        NativeBattleGastricJuice169.Morph(first.ToImmutableArray(), second.ToImmutableArray(), output, 8);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(output));
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(6)));
        Assert.Equal(new[] { 0, 0, 3584, 4096, 512, 0, 3584 }, new[] { 7, 8, 15, 16, 23, 24, 31 }.Select(NativeBattleGastricJuice169.MeshFade));
    }

    [Fact]
    public void Custom_landing_packets_append_the_original_draw_mode_word_and_expand_the_tag_length()
    {
        byte[] model = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(model, 32);
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(44), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(model.AsSpan(52), 4);
        ImmutableArray<byte> vertices = World(new(-100, -100, 1000), new(100, -100, 1000), new(-100, 100, 1000));
        NativeBattleGfGeometryBatch batch = NativeBattleGastricJuice169.Emit5E0450(model.ToImmutableArray(), vertices, Identity, Projection, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(36, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(0x08000000u, attempt.Primitive!.Words[0]);
        Assert.Equal(0xE1000220u, attempt.Primitive.Words[^1]);
        Assert.Equal((uint)0x20, attempt.Primitive.Words[5] >> 16);
        Assert.All(attempt.WriteMask.AsSpan().Slice(32).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void Tint_outside_corners_keep_actual_shared_scratch_fourth_words_while_near_corner_gets_radial_fade()
    {
        byte[] face = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        byte[] world = World(new(0, 0, 1000), new(2000, 0, 1000), new(0, 2000, 1000)).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(world.AsSpan(14), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(world.AsSpan(22), -4096);
        NativeBattleGfGeometryBatch batch = NativeBattleGastricJuice169.Tint5DF540([new(false, face.ToImmutableArray())], world.ToImmutableArray(), Identity, Projection, new(0, 0, 1000), 0x808080, 8, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(2, batch.Attempts.Length);
        NativeBattleGeometryPrimitive body = batch.Attempts[0].Primitive!;
        NativeBattleGeometryPrimitive overlay = batch.Attempts[1].Primitive!;
        Assert.Equal(body.Bucket - 1, overlay.Bucket);
        Assert.Equal(0x28C098u, overlay.Words[1] & 0xFFFFFF);
        Assert.Equal(0x14604Cu, overlay.Words[3] & 0xFFFFFF);
        Assert.Equal(0u, overlay.Words[5] & 0xFFFFFF);
        Assert.Equal((short)2048, BinaryPrimitives.ReadInt16LittleEndian(world.AsSpan(14)));
    }

    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);
    private static ImmutableArray<byte> World(params NativeBattleVector[] points)
    {
        byte[] bytes = new byte[points.Length * 8];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 8 + 4), points[index].Z);
        }
        return bytes.ToImmutableArray();
    }
}
