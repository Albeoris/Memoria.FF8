using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUiCommandsTests
{
    [Fact]
    public void Outcome_command65_clears_only_ui_bit40_when_the_original_queue_is_consumed()
    {
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize() with { Flags942 = 0xFFFF };
        NativeBattleUiCommands commands = ui.Commands.Enqueue(0, 65);
        commands.Validate(0);
        Assert.Equal((ushort)0xFFFF, ui.Flags942);
        (commands, uint mask, bool select) = commands.Dispatch(0, clear => ui = ui.SetFlags(0, clear));
        Assert.Equal((ushort)0xFFBF, ui.Flags942);
        Assert.Equal(0u, mask);
        Assert.False(select);
        Assert.Empty(commands.Queue);
    }

    [Fact]
    public void Ready_commands_wait_for_the_next_service_and_preserve_order_across_restore()
    {
        NativeBattleUiCommands state = NativeBattleUiCommands.Clear().Enqueue(2, 17).Enqueue(0, 17).Enqueue(2, 17);
        state.Validate(0);
        Assert.Null(state.SelectedActor);
        state = JsonSerializer.Deserialize<NativeBattleUiCommands>(JsonSerializer.Serialize(state))!;
        (state, uint mask, bool selected) = state.Dispatch(0);
        Assert.Equal(5u, mask);
        Assert.True(selected);
        Assert.Equal((sbyte)2, state.SelectedActor);
        Assert.Equal(2, state.ReadyHead);
        Assert.Equal(0, state.ReadyNodes[2].Next);
        state.Validate(mask);
        (state, mask, selected) = state.Enqueue(2, 18).Dispatch(mask);
        Assert.True(selected);
        Assert.Equal(1u, mask);
        Assert.Equal((sbyte)0, state.SelectedActor);
        Assert.Equal(0, state.ReadyHead);
        state.Validate(mask);
        (state, mask, selected) = state.Enqueue(0, 18).Dispatch(mask);
        Assert.Equal(0u, mask);
        Assert.Equal((sbyte)-1, state.SelectedActor);
        Assert.True(selected);
        state.Validate(mask);
    }

    [Fact]
    public void Ui20_and_graphics30_share_the_original_sentinel_capacity_and_packet_reuse()
    {
        NativeBattleUiCommands ui = NativeBattleUiCommands.Clear();
        for (int index = 0; index < 19; index++)
        {
            ui = ui.Enqueue((byte)(index % 3), 17);
        }
        Assert.Throws<InvalidOperationException>(() => ui.Enqueue(0, 17));
        (ui, uint mask, _) = ui.Dispatch(0);
        ui.Validate(mask);
        Assert.Empty(ui.Queue);
        ui = ui.Enqueue(1, 18);
        Assert.Equal(0, Assert.Single(ui.Queue));
        Assert.Equal((ushort)0xF00, ui.Packets[0].UnknownByteMask);
        ui.Validate(mask);
        NativeBattleDescriptorState graphics = NativeBattleDescriptorState.Clear();
        for (int index = 0; index < 29; index++)
        {
            graphics = graphics.Enqueue(112, 128, 0).State;
        }
        Assert.Throws<InvalidOperationException>(() => graphics.Enqueue(112, 128, 0));
        graphics = graphics.Rotate().RemoveFirst();
        graphics.Validate();
    }

    [Fact]
    public void Ready_mask_and_link_mismatches_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleUiCommands.Clear().Validate(1));
        NativeBattleUiCommands state = NativeBattleUiCommands.Clear().Enqueue(0, 17).Dispatch(0).Commands;
        Assert.Throws<InvalidDataException>(() => (state with { ReadyNodes = state.ReadyNodes.SetItem(0, new(0, 1)) }).Validate(1));
    }
}
