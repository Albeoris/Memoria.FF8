using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStatusIconsTests
{
    private static NativeBattleStatusIconResources Resources => new(0xFF000000, [Entry(5, 0, -32, 3328), Entry(6, 0, -8, 4096), Entry(7, 192, -104, 3328), Entry(8, 0, 0, 4096), Entry(12, -104, -16, 3072)]);
    private static NativeBattleGraphicsModel Model => new(1, 3) { Flags2 = 0, Flags8 = 0, Transform = new(new(0, 0, 0), new(12, 34, 56), 0, 0, 4096) };

    [Theory]
    [InlineData(5, 0, -32, 3328)]
    [InlineData(6, 0, -8, 4096)]
    [InlineData(7, 192, -104, 3328)]
    [InlineData(8, 0, 0, 4096)]
    [InlineData(12, -104, -16, 3072)]
    public void Five_original_selectors_keep_distinct_resources_and_original_context_flags(int bit, int x, int y, int scale)
    {
        int anchors = 0;
        NativeBattleGraphicsModel model = Model;
        NativeBattleStatusIconResult result = NativeBattleStatusIcons.Advance5711C0(model, bit, 1, 0, Resources, current =>
        {
            Assert.Same(model.Transform, current.Transform);
            anchors++;
            return new(123, -456, 789);
        });
        NativeBattleModuleDraw draw = Assert.IsType<NativeBattleModuleDraw>(result.Draw);
        Assert.Equal((byte)2, result.TimerAfter);
        Assert.Equal(1, anchors);
        Assert.Equal(Resources.Entries.Single(entry => entry.StatusBit == bit).Sprite.Address, draw.Resource!.Address);
        Assert.Equal((ushort)1, draw.Phase);
        Assert.Equal(new NativeBattleModuleAnchor(123, -456, 789, 0), draw.Anchor);
        Assert.Equal(scale, draw.Scale);
        Assert.Equal(-2000, draw.DepthOffset);
        Assert.Equal((ushort)8, draw.SpriteFlags);
        Assert.Equal(x, draw.SpriteCameraXOffset);
        Assert.Equal(y, draw.SpriteCameraYOffset);
        Assert.Equal(0, draw.SpriteCameraZOffset);
        Assert.Equal(bit == 6, draw.SpriteFrame!.ExtendedPacket);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(32)]
    public void Unmapped_selector_returns_negative_descriptor_AL_without_reading_model_or_anchor(int bit)
    {
        NativeBattleStatusIconResult result = NativeBattleStatusIcons.Advance5711C0(new(0, 0), bit, 123, 0, Resources, _ => throw new InvalidOperationException());
        Assert.Equal(byte.MaxValue, result.TimerAfter);
        Assert.Null(result.Draw);
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 7)]
    public void Terminal_frame_draws_before_pause_or_wrap_decision_even_when_projection_rejects_every_quad(uint graphicsFlags, byte expectedTimer)
    {
        NativeBattleStatusIconResult result = NativeBattleStatusIcons.Advance5711C0(Model, 5, 7, graphicsFlags, Resources, _ => new(0, 0, 0));
        NativeBattleModuleDraw draw = Assert.IsType<NativeBattleModuleDraw>(result.Draw);
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, -10000, 4096);
        NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(draw, new(view, 216, 160, 108), 0, 17, 2);
        Assert.Empty(packets.Primitives);
        Assert.Equal(0, packets.AllocationBytes);
        Assert.Equal(expectedTimer, result.TimerAfter);
        Assert.Equal((ushort)7, draw.Phase);
    }

    [Fact]
    public void Multiple_statuses_run_in_bit_order_and_only_own_selected_timer_bytes()
    {
        NativeBattleGraphicsModel model = Model with { Flags2 = 8, Flags8 = 0x11E0, StatusTimers = Model.StatusTimers.SetItem(5, 7).SetItem(6, 2).SetItem(7, 3).SetItem(8, 0).SetItem(12, 5).SetItem(31, 91) };
        List<int> observed = [];
        NativeBattleStatusIconStep step = NativeBattleStatusIcons.Advance50A165(model, 0, Resources, current =>
        {
            observed.Add(current.StatusTimers[5]!.Value);
            return new(0, 0, 1000);
        });
        Assert.Equal(new byte[] { 5, 6, 7, 8, 12 }, step.Draws.Select(draw => Resources.Entries.Single(entry => entry.Sprite.Address == draw.Resource!.Address).StatusBit).ToArray());
        Assert.Equal(new[] { 7, 0, 0, 0, 0 }, observed);
        Assert.Equal(new byte?[] { 0, 3, 0, 1, 6 }, new[] { 5, 6, 7, 8, 12 }.Select(bit => step.Model.StatusTimers[bit]).ToArray());
        Assert.Equal((byte?)91, step.Model.StatusTimers[31]);
        Assert.Null(step.Model.StatusTimers[4]);
        Assert.Equal(model.Flags8, step.Model.Flags8);
        Assert.Equal(model.Transform, step.Model.Transform);
        Assert.Equal((byte?)7, model.StatusTimers[5]);
    }

    [Fact]
    public void Repeated_frame_offsets_keep_four_distinct_timer_phases_and_paused_output_is_identical()
    {
        NativeBattleStatusIconResult first = NativeBattleStatusIcons.Advance5711C0(Model, 7, 0, 0, Resources, _ => new(0, 0, 1000));
        NativeBattleStatusIconResult repeated = NativeBattleStatusIcons.Advance5711C0(Model, 7, first.TimerAfter, 0, Resources, _ => new(0, 0, 1000));
        Assert.Equal(first.Draw!.SpriteFrame!.Quads.ToArray(), repeated.Draw!.SpriteFrame!.Quads.ToArray());
        Assert.Equal((byte)1, first.TimerAfter);
        Assert.Equal((byte)2, repeated.TimerAfter);
        NativeBattleGraphicsModel model = Model with { Flags8 = 0x80, StatusTimers = Model.StatusTimers.SetItem(7, 3) };
        NativeBattleStatusIconStep paused = NativeBattleStatusIcons.Advance50A165(model, 1, Resources, _ => new(0, 0, 1000));
        Assert.Equal((byte?)3, paused.Model.StatusTimers[7]);
        Assert.Single(paused.Draws);
    }

    [Theory]
    [InlineData(2049, -1000)]
    [InlineData(-32768, 16000)]
    [InlineData(32767, -15999)]
    public void Depth_displacement_uses_signed_model_scale_and_arithmetic_shift_before_WORD_store(short scale, int depth)
    {
        NativeBattleGraphicsModel model = Model with { Transform = Model.Transform! with { Scale38 = scale } };
        NativeBattleStatusIconResult result = NativeBattleStatusIcons.Advance5711C0(model, 5, 0, 0, Resources, _ => new(0, 0, 1000));
        Assert.Equal(depth, result.Draw!.DepthOffset);
    }

    [Fact]
    public void Gated_or_unselected_icons_do_not_read_missing_resources_timer_or_anchor()
    {
        NativeBattleGraphicsModel[] gated = [new(0, 0), new(0, 3) { Flags2 = 4 }, Model, Model with { Flags0 = 7, Flags8 = 0x20 }];
        foreach (NativeBattleGraphicsModel model in gated)
        {
            NativeBattleStatusIconStep step = NativeBattleStatusIcons.Advance50A165(model, 0, null, _ => throw new InvalidOperationException());
            Assert.Same(model, step.Model);
            Assert.Empty(step.Draws);
        }
        foreach (uint flags in new uint[] { 0x10, 0x400, 0x410 })
        {
            NativeBattleStatusIconStep step = NativeBattleStatusIcons.Advance50A165(new(0, 3) { Flags2 = 0 }, flags, null, _ => throw new InvalidOperationException());
            Assert.Empty(step.Draws);
        }
    }

    [Fact]
    public void Reached_old_unknown_metadata_or_selected_timer_is_strict_and_unselected_unknowns_remain_unknown()
    {
        NativeBattleGraphicsModel model = Model with { Flags8 = 0x20 };
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusIcons.Advance50A165(model, 0, null, _ => new(0, 0, 1000)));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusIcons.Advance50A165(model, 0, Resources, _ => new(0, 0, 1000)));
        NativeBattleStatusIconStep step = NativeBattleStatusIcons.Advance50A165(model with { StatusTimers = model.StatusTimers.SetItem(5, 0) }, 0, Resources, _ => new(0, 0, 1000));
        Assert.Equal((byte?)1, step.Model.StatusTimers[5]);
        Assert.Null(step.Model.StatusTimers[6]);
        Assert.Throws<InvalidDataException>(() => (Resources with { Entries = Resources.Entries.Reverse().ToImmutableArray() }).Validate());
    }

    [Fact]
    public void Mid_animation_JSON_roundtrip_preserves_next_draw_and_timer_without_initializing_other_bytes()
    {
        NativeBattleGraphicsModel model = Model with { Flags8 = 0x60, StatusTimers = Model.StatusTimers.SetItem(5, 4).SetItem(6, 7) };
        NativeBattleStatusIconStep before = NativeBattleStatusIcons.Advance50A165(model, 0, Resources, _ => new(100, -200, 1000));
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(before.Model))!;
        NativeBattleStatusIconResources resources = JsonSerializer.Deserialize<NativeBattleStatusIconResources>(JsonSerializer.Serialize(Resources))!;
        NativeBattleStatusIconStep next = NativeBattleStatusIcons.Advance50A165(before.Model, 0, Resources, _ => new(100, -200, 1000));
        NativeBattleStatusIconStep clone = NativeBattleStatusIcons.Advance50A165(restored, 0, resources, _ => new(100, -200, 1000));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(clone));
        Assert.Null(clone.Model.StatusTimers[4]);
        Assert.Equal((byte?)1, clone.Model.StatusTimers[6]);
    }

    private static NativeBattleStatusIconResource Entry(byte bit, short x, short y, short scale)
    {
        int count = bit == 7 ? 4 : 8;
        int unique = bit == 7 ? 2 : count;
        int start = 10 + 2 * (count + 1);
        byte[] bytes = new byte[start + unique * 24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x524);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int phase = 0; phase < count; phase++)
        {
            int frame = bit == 7 ? phase / 2 : phase;
            int offset = start + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * phase), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), bit == 6 ? 0x80000001 : 1);
            bytes[offset + 4] = 8;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 8;
            bytes[offset + 7] = 0x2E;
            bytes[offset + 8] = (byte)frame;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 10), 0x3800);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 18), 0x142);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + 2 * count), ushort.MaxValue);
        return new(bit, x, y, scale, new(0, 0xC70000u + (uint)bit * 1024, 0x570CC0, 0x570BC0, bytes.ToImmutableArray()));
    }
}
