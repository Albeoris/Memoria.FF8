using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuxiliaryScreenBlendTests
{
    [Fact]
    public void Full_auxiliary_bank_cannot_silently_drop_the_required_native_screen_blend()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        for (int index = 0; index < 16; index++)
        {
            state = state.AppendCameraBlend(4);
        }
        Assert.Throws<InvalidOperationException>(() => state.BeginScreenBlend501E40(4));
    }

    [Fact]
    public void Forward_blend_starts_at_one_holds_full_strength_then_reverse_releases_after_last_preterminal_packet()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        Assert.Same(state, state.ReverseScreenBlend501F30(4));
        state = state.BeginScreenBlend501E40(4);
        Assert.Same(state, state.BeginScreenBlend501E40(99));
        foreach (uint expected in new uint[] { 0x623F3F3F, 0x627F7F7F, 0x62BFBFBF, 0x62FFFFFF, 0x62FFFFFF })
        {
            state = Step(state, expected);
            Assert.NotNull(state.Head);
        }
        state = state.ReverseScreenBlend501F30(4);
        foreach (uint expected in new uint[] { 0x62FFFFFF, 0x62BFBFBF, 0x627F7F7F, 0x623F3F3F })
        {
            state = Step(state, expected);
        }
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Same(state, state.ReverseScreenBlend501F30(1));
    }

    [Fact]
    public void Native_zero_duration_fault_and_negative_duration_packed_bits_are_not_clamped_or_replaced()
    {
        NativeBattleAuxiliaryState empty = NativeBattleAuxiliaryState.Clear();
        Assert.Throws<DivideByZeroException>(() => empty.BeginScreenBlend501E40(0).Advance(new() { ApplyScreenBlend501E80 = _ => throw new InvalidOperationException() }));
        NativeBattleAuxiliaryState state = Step(empty.BeginScreenBlend501E40(-2), 0xFFFFFF80);
        Assert.NotNull(state.Head);
        Assert.Equal((byte)1, state.Slots[state.Head.Value].Payload[0]);
    }

    private static NativeBattleAuxiliaryState Step(NativeBattleAuxiliaryState state, uint expected)
    {
        List<uint> colors = [];
        NativeBattleAuxiliaryStep live = state.Advance(new() { ApplyScreenBlend501E80 = colors.Add });
        Assert.Equal(new[] { expected }, colors);
        NativeBattleAuxiliaryState restored = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        NativeBattleAuxiliaryStep next = restored.Advance(new() { ApplyScreenBlend501E80 = value => Assert.Equal(expected, value) });
        Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(next));
        return live.State;
    }
}
