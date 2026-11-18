using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUiServicesTests
{
    [Theory]
    [InlineData(-1, 0x10, true, null, true)]
    [InlineData(4, 0x10, true, null, true)]
    [InlineData(2, 0, true, null, true)]
    [InlineData(2, 0x10, false, null, true)]
    [InlineData(2, 0x10, true, (ushort)0, true)]
    [InlineData(2, 0x10, true, (ushort)1, false)]
    [InlineData(2, 0x10, true, null, null)]
    public void Explicit_ready_presence_matches_the_native_mask_gate(sbyte menu, ushort flags, bool ready, ushort? options, bool? admitted)
    {
        NativeBattleUiServices ui = NativeBattleUiServices.Initialize() with { Menu957 = menu, Flags942 = flags, ReadyMask = ready ? 4u : 0 };
        NativeBattleUiServices expanded = ui with { ReadyMask = 0 };
        if (admitted is bool expected)
        {
            Assert.Equal(expected, ui.AllowsAtb(options));
            Assert.Equal(expected, expanded.AllowsAtb(options, ready));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => ui.AllowsAtb(options));
            Assert.Throws<NotSupportedException>(() => expanded.AllowsAtb(options, ready));
        }
    }

    [Fact]
    public void Stage_initialization_mid_frame_places_the_actual_edge_on_third_pre_service()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize().QueuePublication(4).AdvancePublication();
        Assert.Equal(1u, state.Counter);
        Assert.False(state.Frame33);
        for (int frame = 0; frame < 3; frame++)
        {
            state = state.AdvancePublication();
            Assert.False(state.Frame33);
            state = state.AdvancePublication();
            Assert.False(state.Frame33);
            state = state.AdvancePublication();
            Assert.True(state.Frame33);
            state = state.QueuePublication(4).AdvancePublication();
            Assert.False(state.Frame33);
        }
    }

    [Fact]
    public void Four_services_publish_one_frame_edge_only_after_the_graphics_pending_producer()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize();
        for (int frame = 0; frame < 3; frame++)
        {
            for (int pre = 0; pre < 3; pre++)
            {
                state = state.AdvancePublication();
                Assert.False(state.Frame33);
            }
            state = state.QueuePublication(4).AdvancePublication();
            Assert.True(state.Frame33);
            Assert.False(state.PendingPublication);
            Assert.Equal((uint)(4 * (frame + 1)), state.Counter);
            Assert.Equal(state.Counter, state.LastPublication);
            state = JsonSerializer.Deserialize<NativeBattleUiServices>(JsonSerializer.Serialize(state))!;
        }
    }

    [Fact]
    public void Missing_graphics_publication_never_invents_a_pose_edge_and_the_unsigned_counter_wraps()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { Counter = uint.MaxValue - 1, LastPublication = uint.MaxValue - 2 };
        state = state.QueuePublication(4).AdvancePublication();
        Assert.False(state.Frame33);
        state = state.AdvancePublication();
        Assert.False(state.Frame33);
        state = state.AdvancePublication();
        Assert.True(state.Frame33);
        Assert.Equal(1u, state.Counter);
        Assert.False(state.AdvancePublication().Frame33);
        Assert.Equal(1u, state.QueuePublication(0).Duration);
    }

    [Fact]
    public void Menu_flag_producers_preserve_the_single_flags_WORD()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize().SetFlags(8).SetFlags(0x1800);
        Assert.Equal((ushort)0x1848, state.Flags942);
        Assert.Equal((ushort)0x48, state.SetFlags(0, 0x1800).Flags942);
    }

    [Fact]
    public void Vitals_opening_tail_queues_once_and_survives_save_before_next_dispatch()
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize().SetFlags(8).DispatchCommands4AD400();
        state = state with { Commands = state.Commands.Enqueue(0, 64) };
        state = state.QueueVitalsOpening4A84E0();
        Assert.Equal((ushort)0x40, state.Flags942);
        Assert.Equal(new ushort[] { 64, 80 }, state.Commands.Queue.Select(slot => state.Commands.Packets[slot].Command));
        Assert.Equal(1, state.Windows!.VitalsState6868);
        Assert.Equal(0, state.Windows.VitalsProgress686C);
        Assert.False(state.Windows.DrawsVitals4B1740(state.Flags942));
        Assert.Same(state, state.QueueVitalsOpening4A84E0());

        state = JsonSerializer.Deserialize<NativeBattleUiServices>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(new ushort[] { 64, 80 }, state.Commands.Queue.Select(slot => state.Commands.Packets[slot].Command));
        Assert.Equal(1, state.Windows!.VitalsState6868);
        state = state.DispatchCommands4AD400();
        Assert.Empty(state.Commands.Queue);
        Assert.Equal(2, state.Windows!.VitalsState6868);
        Assert.Equal(0, state.Windows.VitalsProgress686C);
        Assert.Same(state, state.QueueVitalsOpening4A84E0());

        for (int callback = 0; callback < 32; callback++)
        {
            state = state with { Windows = state.Windows!.AdvanceVitals4B0450().CommitDraw4B9DB0(true, state.Flags942) };
            Assert.Equal(callback == 31, state.Windows.DrawsVitals4B1740(state.Flags942));
        }
        Assert.Equal(4096, state.Windows!.VitalsProgress686C);
        Assert.Equal(5, state.Windows.VitalsState6868);
        Assert.All(Enumerable.Range(0, 3), row => Assert.True(state.Windows.IsVitalsRowVisible4B1740(row)));
        NativeBattleUiServices repeated = state.SetFlags(8).QueueVitalsOpening4A84E0().DispatchCommands4AD400();
        Assert.Equal(state.Windows, repeated.Windows);
        Assert.Empty(repeated.Commands.Queue);
    }

    [Theory]
    [InlineData(0x41)]
    [InlineData(0x840)]
    [InlineData(0)]
    public void Open_vitals_remain_hidden_under_native_ui_suppression(ushort flags)
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize().SetFlags(8).QueueVitalsOpening4A84E0().DispatchCommands4AD400();
        for (int callback = 0; callback < 32; callback++)
        {
            state = state with { Windows = state.Windows!.AdvanceVitals4B0450().CommitDraw4B9DB0(true, state.Flags942) };
        }
        Assert.True(state.Windows!.DrawsVitals4B1740(state.Flags942));
        state = state with { Flags942 = flags };
        Assert.False(state.Windows.DrawsVitals4B1740(state.Flags942));
        Assert.True(state.Windows.DrawsVitals4B1740(0x40));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Wait_admission_uses_the_owned_menu_ready_mask_and_saved_settings_bit(ushort options, bool expected)
    {
        NativeBattleUiServices state = NativeBattleUiServices.Initialize() with { Menu957 = 2, ReadyMask = 1, Flags942 = 0x50 };
        Assert.Equal(expected, state.AllowsAtb(options));
        Assert.Throws<NotSupportedException>(() => state.AllowsAtb(null));
        Assert.True((state with { Menu957 = -1 }).AllowsAtb(null));
        Assert.True((state with { Menu957 = 4 }).AllowsAtb(null));
        Assert.True((state with { ReadyMask = 0 }).AllowsAtb(null));
        Assert.True((state with { Flags942 = 0x40 }).AllowsAtb(null));
    }
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Card_choice_retains_native_action_menu_time_policy_across_ready_actor_selection(ushort options, bool active)
    {
        NativeBattleUiServices submenu = NativeBattleUiServices.Initialize() with { Menu957 = 2, ReadyMask = 1, Flags942 = 0x50 };
        NativeBattleUiServices retained = BattleSession.ApplyNativeReadyMenuSelection(submenu, selectMenu: true, slotsPending: true);
        Assert.Equal((sbyte)2, retained.Menu957);
        Assert.Equal((ushort)0x50, retained.Flags942);
        Assert.Equal(active, retained.AllowsAtb(options));
        NativeBattleUiServices released = BattleSession.ApplyNativeReadyMenuSelection(submenu, selectMenu: true, slotsPending: false);
        Assert.Equal((sbyte)4, released.Menu957);
        Assert.True(released.AllowsAtb(options));
    }

}
