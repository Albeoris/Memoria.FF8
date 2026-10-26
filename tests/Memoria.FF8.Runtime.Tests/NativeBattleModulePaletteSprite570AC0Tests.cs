using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModulePaletteSprite570AC0Tests
{
    [Theory]
    [InlineData(0x00000000u, 0, 0, 4096)]
    [InlineData(0x007FFF00u, 32512, 4080, 4096)]
    [InlineData(0x00FFFF00u, 32512, 4080, -4096)]
    [InlineData(0xFFFF0100u, 32512, 16, -4096)]
    public void Packed_operands_select_exact_scale_rotation_and_mirror_without_changing_palette(uint packed, int scale, short rotation, int direction)
    {
        NativeBattleModuleState state = Child(0, packed);
        NativeBattleModuleStep actual = state.Advance(new(0) { Resources = [Sprite(5)] });
        NativeBattleModuleDraw draw = Assert.Single(actual.Draws);
        Assert.Equal(scale, draw.Scale);
        Assert.Equal(rotation, draw.Rotation);
        Assert.Equal(direction, draw.Direction);
        Assert.Equal(-(scale >> 4), draw.DepthOffset);
        Assert.Equal((ushort)0x13, draw.SpriteFlags);
        Assert.Equal(new byte[] { 0x11, 0, 0, 0 }, draw.SpritePaletteOffsets);
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(actual.State.Slots[0].Payload.AsSpan()));
        Assert.Equal(state.Slots[0].Payload.AsSpan().Slice(2).ToArray(), actual.State.Slots[0].Payload.AsSpan().Slice(2).ToArray());
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)4)]
    public void Any_nonzero_high_header_byte_selects_the_actual_fallback_slot(byte kind)
    {
        NativeBattleModuleResource marker = new(5, 0x700500, 0x570CC0, 0x570AC0, [0, 0, 0, kind]);
        NativeBattleModuleResource fallback = Sprite(0);
        NativeBattleModuleDraw draw = Assert.Single(Child(0).Advance(new(0) { Resources = [marker, fallback] }).Draws);
        Assert.Equal(fallback, draw.Resource);
    }

    [Fact]
    public void Pause_draws_the_terminal_frame_without_advancing_or_releasing_and_JSON_preserves_the_next_callback()
    {
        NativeBattleModuleState state = Child(1, anchorDepth: -123);
        NativeBattleModuleInputs inputs = new(1) { Resources = [Sprite(5)], CrtRandom = new(777) };
        NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
        NativeBattleModuleStep paused = state.Advance(inputs);
        Assert.Equal(JsonSerializer.Serialize(paused), JsonSerializer.Serialize(restored.Advance(inputs)));
        NativeBattleModuleDraw draw = Assert.Single(paused.Draws);
        Assert.Equal((ushort)1, draw.Phase);
        Assert.Equal(123, draw.DepthOffset);
        Assert.Equal(state.Slots[0], paused.State.Slots[0]);
        Assert.Equal(inputs.CrtRandom, paused.CrtRandomAfter);
        NativeBattleModuleStep completed = paused.State.Advance(inputs with { GraphicsFlags = 0 });
        Assert.Single(completed.Draws);
        Assert.Null(completed.State.Head);
        Assert.Null(completed.State.Tail);
        Assert.Equal((short)2, BinaryPrimitives.ReadInt16LittleEndian(completed.State.Slots[0].Payload.AsSpan()));
    }

    [Fact]
    public void Placement_dispatches_the_palette_sprite_child_and_draws_it_in_the_same_pass()
    {
        NativeBattleModuleResource descriptor = new(7, 0x700700, 0x570CC0, 0x570AC0, [0, 0, 0, 2]);
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendModelResource(3, descriptor, 0x00100007);
        NativeBattleModuleStep actual = state.Advance(new(0) { Resources = [descriptor, Sprite(5)], ReadModelAnchor = model =>
        {
            Assert.Equal(3, model);
            return new(10, 20, 30, 40);
        } });
        Assert.Equal(NativeBattleModuleKind.PaletteSprite570AC0, actual.State.Slots[actual.State.Head!.Value].Kind);
        NativeBattleModuleDraw draw = Assert.Single(actual.Draws);
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 40), draw.Anchor);
        Assert.Equal(-40, draw.DepthOffset);
        Assert.Equal((ushort)0, draw.Phase);
        Assert.Equal((byte)1, actual.State.PublishedCount);
    }

    [Fact]
    public void Descriptor_payload_cannot_replace_missing_actual_sprite_table_bindings()
    {
        Assert.Throws<NotSupportedException>(() => Child(0).Advance(new(0)));
        NativeBattleModuleResource marker = new(5, 0x700500, 0x570CC0, 0x570AC0, [0, 0, 0, 2]);
        Assert.Throws<NotSupportedException>(() => Child(0).Advance(new(0) { Resources = [marker] }));
    }

    private static NativeBattleModuleState Child(short phase, uint packed = 0x00100000, short anchorDepth = 0)
    {
        (NativeBattleModuleState state, int slot) = NativeBattleModuleState.Clear().Append(NativeBattleModuleKind.PaletteSprite570AC0);
        byte[] payload = Enumerable.Repeat((byte)0xA5, 24).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, phase);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), 10);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(6), 20);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(8), 30);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), anchorDepth);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(16), packed);
        return state with { Slots = state.Slots.SetItem(slot, state.Slots[slot] with { Payload = payload.ToImmutableArray() }) };
    }

    private static NativeBattleModuleResource Sprite(byte id)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 40);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), 1);
        return new(id, 0x700000u + 0x100u * id, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }
}
