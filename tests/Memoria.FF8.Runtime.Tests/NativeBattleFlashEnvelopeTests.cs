using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFlashEnvelopeTests
{
    [Fact]
    public void Double_zero_rise_one_hold_two_fall_envelope_keeps_pause_and_exact_fullscreen_receipts_after_json()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendFlash(0, 1, 2, 128);
        foreach (byte expected in new byte[] { 128, 128, 64 })
        {
            NativeBattleModuleStep paused = state.Advance(new(1));
            Assert.Equal(JsonSerializer.Serialize(state.Slots), JsonSerializer.Serialize(paused.State.Slots));
            Assert.Empty(paused.Draws);
            NativeBattleModuleState restored = JsonSerializer.Deserialize<NativeBattleModuleState>(JsonSerializer.Serialize(state))!;
            NativeBattleModuleStep step = state.Advance(new(0));
            Assert.Equal(JsonSerializer.Serialize(step), JsonSerializer.Serialize(restored.Advance(new(0))));
            NativeBattleModuleDraw draw = Assert.Single(step.Draws);
            Assert.Equal(expected, draw.Intensity);
            NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit(draw);
            NativeBattleAllocatedPrimitive packet = Assert.Single(batch.Primitives);
            uint rgb = (uint)(expected | expected << 8 | expected << 16);
            Assert.Equal(new uint[] { 0x03000000, 0x62000000 | rgb, 0, 0x00D80140 }, packet.Primitive.Words.ToArray());
            Assert.Equal(new[] { 0, 0, 0, 0 }, packet.Primitive.VertexPayload4.ToArray());
            Assert.Equal((ushort)1, packet.Flags20);
            Assert.Equal(17, packet.Primitive.Bucket);
            Assert.Equal(16, batch.AllocationBytes);
            Assert.All(packet.Primitive.PhysicalWriteMask!.Value, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
            state = step.State;
        }
        NativeBattleModuleStep complete = state.Advance(new(0));
        Assert.Empty(complete.Draws);
        Assert.Null(complete.State.Head);
        Assert.Equal((ushort)0, complete.State.Slots[0].Flags);
        Assert.Equal((short)4, BinaryPrimitives.ReadInt16LittleEndian(complete.State.Slots[0].Payload.AsSpan()));
    }

    [Fact]
    public void Producer_packs_original_or_shift_operands_and_only_overwrites_its_three_words_in_a_reused_slot()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        state = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }) };
        NativeBattleModuleState appended = state.AppendFlash(0x11, 0x23, 0x45, 0x1234);
        ReadOnlySpan<byte> payload = appended.Slots[0].Payload.AsSpan();
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(payload));
        Assert.Equal((ushort)0x4731, BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]));
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(payload[16..]));
        foreach (int index in Enumerable.Range(4, 12).Concat(Enumerable.Range(18, 6)))
        {
            Assert.Equal((byte)0xA5, payload[index]);
        }
        Assert.All(state.Slots[0].Payload, value => Assert.Equal((byte)0xA5, value));
    }

    [Theory]
    [InlineData(300, 44, false)]
    [InlineData(-1, 0, true)]
    [InlineData(-32768, 0, true)]
    public void Maximum_is_signed_word_before_release_and_only_nonnegative_draw_intensity_is_narrowed_to_byte(short maximum, byte intensity, bool released)
    {
        NativeBattleModuleStep step = NativeBattleModuleState.Clear().AppendFlash(0, 1, 0, maximum).Advance(new(0));
        if (released)
        {
            Assert.Empty(step.Draws);
            Assert.Null(step.State.Head);
        }
        else
        {
            Assert.Equal(intensity, Assert.Single(step.Draws).Intensity);
            Assert.NotNull(step.State.Head);
        }
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(step.State.Slots[0].Payload.AsSpan()));
    }

    [Fact]
    public void Paused_callback_still_evaluates_the_native_envelope_before_its_flag_gate()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear().AppendFlash(0, 1, 2, 128);
        byte[] payload = state.Slots[0].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, -1);
        state = state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = payload.ToImmutableArray() }) };
        //57131C compares signed counter-1 below zero rise, then IDIV by0, before57135B tests the pause bit.
        Assert.Throws<DivideByZeroException>(() => state.Advance(new(1)));
    }
}
