using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleComponentHeldScreenBlendTests
{
    [Fact]
    public void Held_native_blend_keeps_publishing_without_state_changes_and_reverse_invalidates_it()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear().BeginScreenBlend501E40(4);
        List<uint> colors = [];
        for (int callback = 0; callback < 3; callback++)
        {
            state = state.Advance(new() { ApplyScreenBlend501E80 = colors.Add }).State;
        }
        BattleComponentHeldScreenBlendTask held = Observe(state);
        Assert.True(held.IsHeld);
        string frozen = JsonSerializer.Serialize(state);
        colors.Clear();
        for (int callback = 0; callback < 2; callback++)
        {
            state = state.Advance(new() { ApplyScreenBlend501E80 = colors.Add }).State;
            Assert.Equal(frozen, JsonSerializer.Serialize(state));
            Assert.Equal(held.Fingerprint, Observe(state).Fingerprint);
        }
        Assert.Equal(new uint[] { 0x62FFFFFF, 0x62FFFFFF }, colors);
        state = state.ReverseScreenBlend501F30(4);
        Assert.False(Observe(state).IsHeld);
        for (int callback = 0; callback < 4; callback++)
        {
            state = state.Advance(new() { ApplyScreenBlend501E80 = colors.Add }).State;
        }
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Progressing_unknown_reversed_or_different_native_tasks_are_not_held_screen_blends(int corruption)
    {
        BattleComponentHeldScreenBlendTask held = Observe(NativeBattleAuxiliaryState.Clear().BeginScreenBlend501E40(1));
        Assert.True(held.IsHeld);
        BattleComponentHeldScreenBlendTask invalid = corruption switch
        {
            0 => held with { Payload = held.Payload.SetItem(0, 0) },
            1 => held with { Payload = held.Payload.SetItem(5, 0) },
            2 => held with { UnknownByteMask = 1 },
            3 => held with { Next = held.Slot },
            4 => held with { HasProgram = true },
            _ => held with { Kind = NativeBattleAuxiliaryKind.OutcomeFade501D10 }
        };
        Assert.False(invalid.IsHeld);
    }

    private static BattleComponentHeldScreenBlendTask Observe(NativeBattleAuxiliaryState state)
    {
        int slot = state.Head!.Value;
        NativeBattleAuxiliaryTask task = state.Slots[slot];
        return new(slot, task.Flags, task.State, task.Next, task.Kind, task.UnknownByteMask, task.Program is not null, task.Payload);
    }
}
