using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlSkinningTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 216, 160, 108);
    private static NativeBattleSummonColors Colors => new(0, 0, 1f / 4096, 4503601774854144d);
    private static NativeBattleQuezacotlSkinContext Context => new(uint.MaxValue, 0x808080, 0x808080, 0, 0, 320, 216, 0x3F54, 0x1C7, 0, 0, 128, 128, -4096, [0x02000000, 0xE2000000, 0], [0x02000000, 0xE2080210, 0], 17, 2);

    [Theory]
    [InlineData(false, 32, 56)]
    [InlineData(true, 40, 64)]
    public void Opaque_height_emits_native_dual_cursor_E2_overlay_E2_body_order(bool quad, int commonLength, int effectLength)
    {
        NativeBattleQuezacotlSkinBatch batch = Emit(quad);
        Assert.Equal(commonLength, batch.CommonAllocationBytes);
        Assert.Equal(effectLength, batch.EffectAllocationBytes);
        Assert.Equal(new[] { NativeBattleQuezacotlSkinBank.Effect, NativeBattleQuezacotlSkinBank.Common, NativeBattleQuezacotlSkinBank.Effect, NativeBattleQuezacotlSkinBank.Effect }, batch.Writes.Select(write => write.Bank));
        Assert.Equal(new[] { 20, 100, 32, 44 }, batch.Writes.Select(write => write.Attempt.ByteOffsetRelative));
        Assert.Equal(new uint[] { 0x02000000, 0xE2000000, 0 }, batch.Writes[0].Attempt.Primitive!.Words);
        Assert.Equal(new uint[] { 0x02000000, 0xE2080210, 0 }, batch.Writes[2].Attempt.Primitive!.Words);
        NativeBattleGeometryPrimitive overlay = batch.Writes[1].Attempt.Primitive!;
        NativeBattleGeometryPrimitive body = batch.Writes[3].Attempt.Primitive!;
        Assert.Equal(quad ? 0x2E808080u : 0x26808080u, overlay.Words[1]);
        Assert.Equal(quad ? 0x2C808080u : 0x24808080u, body.Words[1]);
        Assert.Equal(0x12340201u, body.Words[3]);
        Assert.Equal(0xABCD0403u, body.Words[5]);
        Assert.Equal(0x0605u, body.Words[7]);
        Assert.Equal(0x3F540201u, overlay.Words[3]);
        Assert.Equal(0x01C70403u, overlay.Words[5]);
        Assert.Equal(quad ? 273 : 272, overlay.Bucket);
        Assert.Equal(quad ? new[] { 4096, 4096, 4096, 4096 } : new[] { 4096, 4096, 4096, 0 }, body.VertexPayload4);
        Assert.Equal(new[] { 0, 0, 0, 0 }, batch.Writes[0].Attempt.Primitive!.VertexPayload4);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Writes[3].Attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, batch.Writes[1].Attempt.WriteMask[30]);
    }

    [Theory]
    [InlineData(false, 32, 100)]
    [InlineData(true, 40, 120)]
    public void Mixed_height_writes_both_prefixes_then_cumulative_GT_colors_glow_and_E1(bool quad, int commonLength, int effectLength)
    {
        NativeBattleQuezacotlSkinBatch batch = Emit(quad, Corners(quad, 2048));
        Assert.Equal(commonLength, batch.CommonAllocationBytes);
        Assert.Equal(effectLength, batch.EffectAllocationBytes);
        Assert.Equal(8, batch.Writes.Length);
        Assert.Null(batch.Writes[3].Attempt.Primitive);
        Assert.Null(batch.Writes[4].Attempt.Primitive);
        NativeBattleGfGeometryAttempt colorStores = batch.Writes[5].Attempt;
        NativeBattleGeometryPrimitive body = colorStores.Primitive!;
        Assert.Equal(quad ? 0x3E404040u : 0x36404040u, body.Words[1]);
        Assert.Equal(0x202020u, body.Words[4]);
        Assert.Equal(0x101010u, body.Words[7]);
        if (quad)
        {
            Assert.Equal(0x080808u, body.Words[10]);
        }
        Assert.Equal(NativeBattleGfPacketByteState.Known, colorStores.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, colorStores.WriteMask[19]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, colorStores.WriteMask[0]);
        Assert.Equal(quad ? 0x3A404040u : 0x32404040u, batch.Writes[6].Attempt.Primitive!.Words[1]);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000240 }, batch.Writes[7].Attempt.Primitive!.Words);
        Assert.Equal(20 + effectLength - 8, batch.Writes[7].Attempt.ByteOffsetRelative);
    }

    [Theory]
    [InlineData(0x10, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(4, 4, 4)]
    public void Near_and_common_clip_flags_reject_before_reading_unknown_projected_fields(byte first, byte second, byte third)
    {
        NativeBattleQuezacotlSkinnedVertex[] corners = [new(first, null, null, null, null, null), new(second, null, null, null, null, null), new(third, null, null, null, null, null)];
        NativeBattleQuezacotlSkinBatch batch = Emit(false, corners);
        Assert.Empty(batch.Writes);
        Assert.Equal(0, batch.CommonAllocationBytes);
        Assert.Equal(0, batch.EffectAllocationBytes);
    }

    [Fact]
    public void Zero_fade_and_negative_winding_reject_without_any_packet_write_but_degenerate_winding_is_admitted()
    {
        Assert.Empty(Emit(false, Corners(false, 0)).Writes);
        NativeBattleQuezacotlSkinnedVertex[] corners = Corners(false);
        (corners[1], corners[2]) = (corners[2], corners[1]);
        Assert.Empty(Emit(false, corners).Writes);
        corners[1] = corners[0];
        Assert.Equal(4, Emit(false, corners).Writes.Length);
    }

    [Theory]
    [InlineData(128, 1, 129)]
    [InlineData(129, 129, 2)]
    public void Generated_UV_seam_adjustment_uses_strict_span_greater_than_128(int span, int expectedFirst, int expectedSecond)
    {
        NativeBattleQuezacotlSkinnedVertex[] corners = Corners(false);
        corners[0] = corners[0] with { U = 1 };
        corners[1] = corners[1] with { U = (byte)(1 + span) };
        corners[2] = corners[2] with { U = 1 };
        NativeBattleGeometryPrimitive overlay = Emit(false, corners).Writes[1].Attempt.Primitive!;
        Assert.Equal(expectedFirst, (byte)overlay.Words[3]);
        Assert.Equal(expectedSecond, (byte)overlay.Words[5]);
    }

    [Fact]
    public void UV_offset_overflow_subtracts_the_BYTE_period_from_every_corner()
    {
        NativeBattleQuezacotlSkinnedVertex[] corners = Corners(false).Select(vertex => vertex with { U = 250, V = 240 }).ToArray();
        NativeBattleQuezacotlSkinBatch batch = NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(Face(false), corners, Context with { UOffset = 10, VOffset = 20 }, Colors);
        Assert.Equal(132, (byte)batch.Writes[1].Attempt.Primitive!.Words[3]);
        Assert.Equal(132, (byte)(batch.Writes[1].Attempt.Primitive!.Words[3] >> 8));
    }

    [Fact]
    public void Original_color_command_bits_preserve_semitransparency_and_require_prior_depth_only_for_quad_opcodes_after_triangles()
    {
        NativeBattleQuezacotlSkinBatch semi = NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(Face(false), Corners(false), Context with { ModelRgb = 0x02808080 }, Colors);
        Assert.Equal(0x26808080u, semi.Writes[3].Attempt.Primitive!.Words[1]);
        Assert.Equal(new[] { 4096, 4096, 4096, 0 }, semi.Writes[3].Attempt.Primitive!.VertexPayload4);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(Face(false), Corners(false), Context with { OverlayRgb = 0x08808080 }, Colors));
        NativeBattleQuezacotlSkinBatch quadOpcode = NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(Face(false), Corners(false), Context with { OverlayRgb = 0x08808080, PriorDepthFifo = 123 }, Colors);
        Assert.Equal(new[] { 123, 4096, 4096, 4096 }, quadOpcode.Writes[1].Attempt.Primitive!.VertexPayload4);
    }

    [Fact]
    public void Skinning_saturates_model_space_words_then_uses_camera_IR_for_UV_and_quarter_depth()
    {
        NativeBattleQuezacotlSkinProjection result = NativeBattleQuezacotlSkinning.Project6C74D0(new(30000, 16, 1000), Identity with { X = 10000 }, Identity, Projection, Context);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(result.Writes[0].Attempt.Data.AsSpan()));
        Assert.Equal<ushort?>(250, result.Vertex.QuarterDepth);
        Assert.Equal<byte?>(255, result.Vertex.U);
        Assert.Equal<byte?>(2, result.Vertex.V);
        Assert.Equal(2, result.Vertex.Flags);
        Assert.Equal<short?>(4096, result.Vertex.Fade);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, result.Writes[1].Attempt.WriteMask[9]);
        Assert.All(result.Writes[0].Attempt.WriteMask.Skip(6), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(-160, -108, 0)]
    [InlineData(-161, -109, 5)]
    [InlineData(160, 108, 10)]
    public void Clip_bounds_compare_signed_screen_eighth_pixels_with_inclusive_left_and_exclusive_right(short x, short y, byte expectedFlags)
    {
        NativeBattleQuezacotlSkinProjection result = NativeBattleQuezacotlSkinning.Project6C74D0(new(x, y, 216), Identity, Identity, Projection, Context);
        Assert.Equal(expectedFlags, result.Vertex.Flags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Nonpositive_quarter_depth_only_overwrites_flag_BYTE6_after_the_initial_skin_XYZ(short depth)
    {
        NativeBattleQuezacotlSkinProjection result = NativeBattleQuezacotlSkinning.Project6C74D0(new(8, 16, depth), Identity, Identity, Projection, Context);
        Assert.Equal(0x10, result.Vertex.Flags);
        Assert.Null(result.Vertex.Screen);
        Assert.Null(result.Vertex.QuarterDepth);
        Assert.Null(result.Vertex.Fade);
        Assert.Equal(0x10, result.Writes[1].Attempt.Data[6]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, result.Writes[1].Attempt.WriteMask[6]);
        Assert.Equal(1, result.Writes[1].Attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
    }

    [Theory]
    [InlineData(-500, 0)]
    [InlineData(0, 2048)]
    [InlineData(500, 4096)]
    public void Height_fade_uses_skinned_Y_and_clamps_to_Q12(short y, short expected)
    {
        NativeBattleQuezacotlSkinProjection result = NativeBattleQuezacotlSkinning.Project6C74D0(new(0, y, 1000), Identity, Identity with { Y = -y }, Projection, Context with { FadeOriginY = 0 });
        Assert.Equal(expected, result.Vertex.Fade);
        Assert.Equal<byte?>(0, result.Vertex.V);
    }

    [Fact]
    public void Wrapper_uses_signed_bone_selector_masks_face_indices_and_skins_whole_group_before_projection()
    {
        List<int> selectors = [];
        NativeBattleQuezacotlSkinBatch batch = NativeBattleQuezacotlSkinning.Emit6C68F0(Geometry(), selector =>
        {
            selectors.Add(selector);
            return Identity;
        }, Identity, Projection, Context, Colors);
        Assert.Equal(new[] { -1 }, selectors);
        Assert.Equal(new[] { 0, 12, 24, 0, 12, 24 }, batch.Writes.Take(6).Select(write => write.Attempt.ByteOffsetRelative));
        Assert.All(batch.Writes.Take(3), write => Assert.Equal(NativeBattleGfPacketByteState.Untouched, write.Attempt.WriteMask[6]));
        Assert.All(batch.Writes.Skip(3).Take(3), write => Assert.Equal(NativeBattleGfPacketByteState.Known, write.Attempt.WriteMask[6]));
        Assert.Equal(32, batch.CommonAllocationBytes);
        Assert.Equal(56, batch.EffectAllocationBytes);
    }

    [Fact]
    public void Mesh_mask_reuses_x86_low_five_shift_bits_and_scratch_base_resets_for_each_mesh()
    {
        byte[] one = Geometry().ToArray();
        byte[] data = new byte[one.Length + 128];
        BinaryPrimitives.WriteInt32LittleEndian(data, 33);
        for (int mesh = 0; mesh < 33; mesh++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4 + 4 * mesh), 136);
        }
        one.AsSpan(8).CopyTo(data.AsSpan(136));
        int calls = 0;
        NativeBattleQuezacotlSkinBatch batch = NativeBattleQuezacotlSkinning.Emit6C68F0(data.ToImmutableArray(), _ =>
        {
            calls++;
            return Identity;
        }, Identity, Projection, Context with { ModelMask = 1 }, Colors);
        Assert.Equal(2, calls);
        Assert.Equal(2, batch.Writes.Count(write => write.Bank == NativeBattleQuezacotlSkinBank.VertexScratch && write.Attempt.ByteOffsetRelative == 0 && write.Attempt.WriteMask[6] == NativeBattleGfPacketByteState.Untouched));
        Assert.Equal(64, batch.CommonAllocationBytes);
        Assert.Equal(112, batch.EffectAllocationBytes);
    }

    [Fact]
    public void Receipt_JSON_preserves_unknown_masks_and_both_cursors()
    {
        NativeBattleQuezacotlSkinBatch original = Emit(true, Corners(true, 2048));
        NativeBattleQuezacotlSkinBatch restored = JsonSerializer.Deserialize<NativeBattleQuezacotlSkinBatch>(JsonSerializer.Serialize(original))!;
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleQuezacotlSkinBatch Emit(bool quad, NativeBattleQuezacotlSkinnedVertex[]? corners = null)
    {
        return NativeBattleQuezacotlSkinnedPackets.Emit6C6A00(Face(quad), corners ?? Corners(quad), Context, Colors, 100, 20);
    }

    private static NativeBattleQuezacotlSkinnedVertex[] Corners(bool quad, short fade = 4096)
    {
        NativeBattleQuezacotlSkinnedVertex[] values = [new(0, XY(100, 100), 4096, 1, 2, fade), new(0, XY(200, 100), 4096, 3, 4, fade), new(0, XY(100, 200), 4096, 5, 6, fade), new(0, XY(200, 200), 4096, 7, 8, fade)];
        return values.Take(quad ? 4 : 3).ToArray();
    }

    private static byte[] Face(bool quad)
    {
        byte[] face = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0xABCD0403);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(quad ? 16 : 6), 0x0605);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(18), 0x0807);
        }
        return face;
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 3);
        short[] xyz = [-100, -100, 1000, 100, -100, 1000, -100, 100, 1000];
        for (int index = 0; index < xyz.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 2), xyz[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32), 1);
        Face(false).CopyTo(bytes, 44);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 0xF000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(46), 0xE001);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48), 0xD002);
        return bytes.ToImmutableArray();
    }

    private static uint XY(short x, short y)
    {
        return (ushort)x | unchecked((uint)(ushort)y << 16);
    }
}
