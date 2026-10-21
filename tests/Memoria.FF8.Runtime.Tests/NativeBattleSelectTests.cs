using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSelectTests
{
    [Fact]
    public void Select_reads_the_retained_held_word_only_on_a_publication_edge()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { HeldButtons16 = 0x100 };
        state = state.QueuePublication(2).AdvancePublication().ApplySelect4A8C50();
        Assert.False(state.Frame33);
        Assert.Equal((ushort)0x40, state.Flags942);
        state = state.AdvancePublication().ApplySelect4A8C50();
        Assert.True(state.Frame33);
        Assert.Equal((ushort)0x41, state.Flags942);
        state = state with { HeldButtons16 = 0 };
        Assert.Equal((ushort)0x41, state.AdvancePublication().ApplySelect4A8C50().Flags942);
    }

    [Fact]
    public void Paused_publication_preserves_the_gate_until_an_unpaused_edge()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { HeldButtons16 = 0x100, PauseDE9 = true };
        state = state.QueuePublication(1).AdvancePublication().ApplySelect4A8C50();
        Assert.True(state.Frame33);
        Assert.Equal((ushort)0x40, state.Flags942);
        state = state with { PauseDE9 = false };
        state = state.QueuePublication(1).AdvancePublication().ApplySelect4A8C50();
        Assert.Equal((ushort)0x41, state.Flags942);
    }

    [Fact]
    public void Releasing_select_clears_only_its_flag_and_preserves_other_held_buttons()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { HeldButtons16 = 0x180, Flags942 = 0x1C50 };
        state = state.QueuePublication(1).AdvancePublication().ApplySelect4A8C50();
        Assert.Equal((ushort)0x1C51, state.Flags942);
        state = state with { HeldButtons16 = 0x80 };
        state = state.QueuePublication(1).AdvancePublication().ApplySelect4A8C50();
        Assert.Equal((ushort)0x1C50, state.Flags942);
        Assert.Equal((ushort?)0x80, state.HeldButtons16);
    }

    [Fact]
    public void Missing_old_held_owner_fails_only_when_select_publication_reaches_it()
    {
        JsonObject saved = JsonNode.Parse(JsonSerializer.Serialize(NativeBattleUiServices.Initialize()))!.AsObject();
        Assert.True(saved.Remove(nameof(NativeBattleUiServices.HeldButtons16)));
        NativeBattleUiServices state = JsonSerializer.Deserialize<NativeBattleUiServices>(saved.ToJsonString())!;
        Assert.Null(state.HeldButtons16);
        state = state.AdvancePublication();
        Assert.Same(state, state.ApplySelect4A8C50());
        state = state.QueuePublication(1).AdvancePublication();
        NativeBattleUiServices paused = state with { PauseDE9 = true };
        Assert.Same(paused, paused.ApplySelect4A8C50());
        Assert.Throws<NotSupportedException>(() => state.ApplySelect4A8C50());
    }

    [Fact]
    public void Restored_retained_input_produces_the_identical_next_publication()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { HeldButtons16 = 0x100 };
        state = state.QueuePublication(2).AdvancePublication();
        NativeBattleUiServices restored = JsonSerializer.Deserialize<NativeBattleUiServices>(JsonSerializer.Serialize(state))!;
        NativeBattleUiServices expected = state.AdvancePublication().ApplySelect4A8C50();
        NativeBattleUiServices actual = restored.AdvancePublication().ApplySelect4A8C50();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal((ushort)0x41, actual.Flags942);
    }
}
