using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaShardPacketsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Offscreen_nonzero_triangles_are_admitted_with_actual_ZSF3_and_original_packet_prefixes(bool gouraud)
    {
        NativeBattleModuleProjectedVertex[] vertices = [new(Screen(5000, 5000), 1000, 0), new(Screen(5100, 5000), 2000, 0), new(Screen(5000, 5100), 3000, 0)];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaShardPackets.EmitTriangle5C2410(Face(gouraud), corner => vertices[corner], gouraud, false, Context(), 4096);
        Assert.NotNull(result.Primitive);
        Assert.Equal(1517, result.Primitive.Bucket);
        Assert.Equal(gouraud ? 40 : 32, result.PacketLength);
        Assert.Equal(0x24808080u, result.Primitive.Words[1]);
        Assert.Equal(0x11223344u, result.Primitive.Words[3]);
        Assert.Equal(0x55667788u, result.Primitive.Words[gouraud ? 6 : 5]);
        Assert.Equal(0x99AAu, result.Primitive.Words[gouraud ? 9 : 7]);
        Assert.All(result.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Zero_area_and_near_rejection_preserve_prefix_stores_without_XY_or_depthcue(bool near)
    {
        NativeBattleModuleProjectedVertex[] vertices = [new(Screen(0, 0), 10, near ? 0x60000u : 0u), new(Screen(100, 0), 20, 0), new(Screen(200, 0), 30, 0)];
        List<ushort> depths = [];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaShardPackets.EmitTriangle5C2410(Face(false), corner => vertices[corner], false, true, Context(), 341, null, 24, depths.Add, _ => throw new InvalidOperationException("A rejected shard cannot load depth cue."));
        Assert.Null(result.Primitive);
        Assert.Equal(24, result.ByteOffsetRelative);
        Assert.Equal(new ushort[] { 10, 20, 30 }, depths);
        Assert.All(result.WriteMask.AsSpan().Slice(8, 4).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Equal(0x07000000u, Word(result, 0));
        Assert.Equal(0x24808080u, Word(result, 4));
        Assert.Equal(0x11223344u, Word(result, 12));
        Assert.Equal(0x99AAu, Word(result, 28));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Animated_GT3_rejection_preserves_unwritten_second_and_third_color_slots(bool animated)
    {
        NativeBattleGfGeometryAttempt result = NativeBattleShivaShardPackets.EmitTriangle5C2410(Face(true), _ => new(Screen(0, 0), 100, 0), true, animated, Context(), 341);
        Assert.Null(result.Primitive);
        foreach (int offset in new[] { 16, 28 })
        {
            Assert.All(result.WriteMask.AsSpan().Slice(offset, 4).ToArray(), value => Assert.Equal(animated ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, value));
        }
        Assert.Equal(0x11223344u, Word(result, 12));
        Assert.Equal(0x55667788u, Word(result, 24));
        Assert.Equal(0x99AAu, Word(result, 36));
    }

    [Theory]
    [InlineData(false, false, 0x10u, true)]
    [InlineData(true, false, 0x10u, false)]
    [InlineData(true, false, 0x20u, true)]
    [InlineData(false, true, 0x10u, false)]
    [InlineData(false, true, 0x20u, true)]
    public void Negative_area_uses_the_native_static_and_animated_selector_bits(bool gouraud, bool animated, uint flags, bool admitted)
    {
        NativeBattleModuleProjectedVertex[] vertices = [new(Screen(0, 0), 100, 0), new(Screen(0, 100), 100, 0), new(Screen(100, 0), 100, 0)];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaShardPackets.EmitTriangle5C2410(Face(gouraud), corner => vertices[corner], gouraud, animated, Context(flags), 341, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(admitted, result.Primitive is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Only_accepted_animated_triangles_load_explicit_depthcue_and_shade_original_RGB(bool gouraud)
    {
        NativeBattleModuleProjectedVertex[] vertices = [new(Screen(0, 0), 100, 0), new(Screen(100, 0), 100, 0), new(Screen(0, 100), 100, 0)];
        List<int> cues = [];
        NativeBattleGfGeometryAttempt result = NativeBattleShivaShardPackets.EmitTriangle5C2410(Face(gouraud), corner => vertices[corner], gouraud, true, Context() with { DepthCue = 4096 }, 341, NativeBattleGteColorInterpolation.CodeConstants45F270, observeDepthCue: cues.Add);
        Assert.Equal(new[] { 4096 }, cues);
        Assert.Equal(0x24000000u, result.Primitive!.Words[1]);
        if (gouraud)
        {
            Assert.Equal(0x24000000u, result.Primitive.Words[4]);
            Assert.Equal(0x24000000u, result.Primitive.Words[7]);
        }
    }

    private static byte[] Face(bool gouraud)
    {
        byte[] face = new byte[gouraud ? 28 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x99AA);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x11223344);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x55667788);
        if (gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), 0x34A0A0A0);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x44C0C0C0);
        }
        return face;
    }

    private static NativeBattleGfGeometryContext Context(uint flags = 0x33) => new(flags, 0, 0, null, null, 0, 17, 2);
    private static uint Screen(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
}
