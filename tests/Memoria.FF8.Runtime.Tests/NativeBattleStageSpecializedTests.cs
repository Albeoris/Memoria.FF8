using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageSpecializedTests
{
    [Theory]
    [InlineData(0x519FC0u)]
    [InlineData(0x518440u)]
    [InlineData(0x518290u)]
    [InlineData(0x5180C0u)]
    public void First_callback_only_advances_phase(uint callback)
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        NativeBattleStageSpecializedStep step = Step(state, callback, 0);
        Assert.Equal(state, step.State);
        Assert.Equal(1, step.Phase);
        Assert.False(step.StandardStage);
        Assert.False(step.Texture0);
        Assert.Null(step.Overlay);
    }

    [Fact]
    public void Stage19_masks_both_low_flags_and_publishes_only_even_counter_without_overwriting_existing_shake()
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        Assert.Equal(state, Step(state, 0x519FC0, flags: 1).State);
        Assert.Equal(state, Step(state, 0x519FC0, flags: 2).State);
        NativeBattleStageSpecializedStep odd = Step(state, 0x519FC0);
        Assert.Equal(1, odd.State.Counter19);
        Assert.Equal(0, odd.CameraShake);
        NativeBattleStageSpecializedStep even = Step(odd.State, 0x519FC0);
        Assert.Equal(2, even.State.Counter19);
        Assert.Equal(-1, even.CameraShake);
        Assert.Equal(7, Step(odd.State, 0x519FC0, shake: 7).CameraShake);
        Assert.True(even.StandardStage);
    }

    [Theory]
    [InlineData(0x518440u)]
    [InlineData(0x518290u)]
    [InlineData(0x5180C0u)]
    public void Color_overlay_freezes_its_angle_only_on_bit8_and_keeps_standard_stage(uint callback)
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        NativeBattleStageSpecializedStep blocked = Step(state, callback, flags: 8);
        Assert.Equal(state, blocked.State);
        Assert.Null(blocked.Overlay);
        Assert.True(blocked.StandardStage);
        NativeBattleStageSpecializedStep first = Step(state, callback, flags: 3);
        Assert.Equal(2048, first.Overlay!.Cue);
        NativeBattleStageSpecializedStep second = Step(first.State, callback);
        Assert.Equal((NativeBattleCameraMath.Sine(128) + 4096) / 2, second.Overlay!.Cue);
        Assert.False(second.Texture0);
    }

    [Fact]
    public void Stage92_initialization_consumes_three_global_values_and_one_CRT_value_in_order()
    {
        Queue<uint> globals = new([29, 17, 767]);
        int crtCalls = 0;
        NativeBattleStageSpecializedStep initialized = NativeBattleStageSpecialized.Advance(NativeBattleStageSpecializedState.FromColdProcess(), 0x5156D0, 0, 15, 8, 0, globals.Dequeue, () =>
        {
            crtCalls++;
            return 0x3FFF;
        });
        Assert.Empty(globals);
        Assert.Equal(1, crtCalls);
        Assert.Equal(59, initialized.State.Delay92);
        Assert.Equal(47, initialized.State.Rise92);
        Assert.Equal(107, initialized.State.Countdown92);
        Assert.Equal(-1023, initialized.State.Magnitude92);
        Assert.Equal(0, initialized.RotationSpeed);
        Assert.False(initialized.StandardStage);
        Assert.False(initialized.Texture0);
    }

    [Fact]
    public void Stage92_gates_texture_and_rotation_separately_and_preserves_held_rotation_when_disabled()
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess() with { Delay92 = 1, Countdown92 = 90, Rise92 = 30, Magnitude92 = 256 };
        NativeBattleStageSpecializedStep frozen = Step(state, 0x5156D0, flags: 8, speed: 123);
        Assert.Equal(state, frozen.State);
        Assert.Equal(123, frozen.RotationSpeed);
        Assert.True(frozen.Texture0);
        NativeBattleStageSpecializedStep delay = Step(state, 0x5156D0, flags: 2, speed: 123);
        Assert.Equal(0, delay.State.Delay92);
        Assert.Equal(90, delay.State.Countdown92);
        Assert.Equal(0, delay.RotationSpeed);
        Assert.False(delay.Texture0);
        Assert.True(delay.StandardStage);
    }

    [Fact]
    public void Stage92_exhausted_cycle_reseeds_once_without_advancing_the_new_countdown()
    {
        int globalCalls = 0;
        int crtCalls = 0;
        NativeBattleStageSpecializedStep step = NativeBattleStageSpecialized.Advance(NativeBattleStageSpecializedState.FromColdProcess(), 0x5156D0, 1, 0, -99, 0, () =>
        {
            globalCalls++;
            return 0;
        }, () =>
        {
            crtCalls++;
            return 0x4000;
        });
        Assert.Equal(3, globalCalls);
        Assert.Equal(1, crtCalls);
        Assert.Equal(90, step.State.Countdown92);
        Assert.Equal(30, step.State.Delay92);
        Assert.Equal(256, step.State.Magnitude92);
        Assert.Equal(0, step.RotationSpeed);
        Assert.True(step.Texture0);
        Assert.True(step.StandardStage);
    }

    [Theory]
    [InlineData(61, 30, 256, 235)]
    [InlineData(60, 30, 256, 256)]
    [InlineData(31, 30, -768, -768)]
    [InlineData(30, 30, 256, 256)]
    [InlineData(1, 30, -768, -2)]
    public void Stage92_envelope_retains_signed_native_division_and_decrement_timing(int remaining, int rise, int magnitude, short expected)
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess() with { Countdown92 = remaining, Rise92 = rise, Magnitude92 = magnitude };
        NativeBattleStageSpecializedStep step = Step(state, 0x5156D0);
        Assert.Equal(expected, step.RotationSpeed);
        Assert.Equal(remaining - 1, step.State.Countdown92);
    }

    [Fact]
    public void Overlay_packets_keep_physical_buffers_mode_tag_links_and_stage45_unwritten_rectangle()
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        NativeBattleStageSpecializedPacketStep wide = NativeBattleStageSpecializedPackets.Build(state, 0x518440, 1, 0x123456);
        Assert.Equal(256, wide.Packets[0].Offset);
        Assert.Equal(0x62123456u, BinaryPrimitives.ReadUInt32LittleEndian(wide.Packets[0].Bytes.AsSpan().Slice(4)));
        Assert.Equal(320, BinaryPrimitives.ReadInt16LittleEndian(wide.Packets[0].Bytes.AsSpan().Slice(12)));
        Assert.Equal(224, BinaryPrimitives.ReadInt16LittleEndian(wide.Packets[0].Bytes.AsSpan().Slice(14)));
        Assert.Equal(0xE1000620u, BinaryPrimitives.ReadUInt32LittleEndian(wide.Packets[1].Bytes.AsSpan().Slice(4)));
        NativeBattleStageSpecializedPacketStep narrow = NativeBattleStageSpecializedPackets.Build(wide.State, 0x518290, 0, 0xFF);
        Assert.All(narrow.Packets[0].Bytes.Skip(8), value => Assert.Equal((byte)0, value));
        state = NativeBattleStageSpecializedPackets.Write(narrow.State, 0x518290, 8, [1, 2, 3, 4, 5, 6, 7, 8]);
        state = NativeBattleStageSpecializedPackets.Write(state, 0x518290, 16, [0xAA, 0xBB, 0xCC]);
        NativeBattleStageSpecializedPacketStep retained = NativeBattleStageSpecializedPackets.Build(state, 0x518290, 0, 0);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, retained.Packets[0].Bytes.Skip(8));
        Assert.Equal(0x01CCBBAAu, BinaryPrimitives.ReadUInt32LittleEndian(retained.Packets[1].Bytes.AsSpan()));
        Assert.Equal(wide.State.Packets44.ToArray(), retained.State.Packets44.ToArray());
    }

    [Fact]
    public void Fresh_and_live_JSON_restoration_continue_without_reinitializing_process_owners()
    {
        foreach (NativeBattleStageSpecializedState original in new[] { NativeBattleStageSpecializedState.FromColdProcess(), NativeBattleStageSpecializedState.FromColdProcess() with { Countdown92 = 31, Rise92 = 42, Magnitude92 = -512, Counter19 = 15, Angle44 = 1024, PublishedColor = 0x12345678 } })
        {
            NativeBattleStageSpecializedState restored = JsonSerializer.Deserialize<NativeBattleStageSpecializedState>(JsonSerializer.Serialize(original))!;
            restored.Validate();
            NativeBattleStageSpecializedStep expected = Step(original, 0x518440);
            NativeBattleStageSpecializedStep actual = Step(restored, 0x518440);
            Assert.Equal(expected.Overlay, actual.Overlay);
            Assert.Equal(expected.State.Angle44, actual.State.Angle44);
            Assert.Equal(expected.State.PublishedColor, actual.State.PublishedColor);
            Assert.Equal(expected.State.Packets44.ToArray(), actual.State.Packets44.ToArray());
            Assert.Equal(Step(original, 0x519FC0).CameraShake, Step(restored, 0x519FC0).CameraShake);
            if (original.Countdown92 != 0)
            {
                NativeBattleStageSpecializedStep expected92 = Step(original, 0x5156D0);
                NativeBattleStageSpecializedStep actual92 = Step(restored, 0x5156D0);
                Assert.Equal(expected92.RotationSpeed, actual92.RotationSpeed);
                Assert.Equal(expected92.State.Countdown92, actual92.State.Countdown92);
            }
        }
    }

    private static NativeBattleStageSpecializedStep Step(NativeBattleStageSpecializedState state, uint callback, short phase = 1, uint flags = 0, short speed = 0, short shake = 0) => NativeBattleStageSpecialized.Advance(state, callback, phase, flags, speed, shake, () => throw new InvalidOperationException("Unexpected global random draw."), () => throw new InvalidOperationException("Unexpected CRT random draw."));
}
