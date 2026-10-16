using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUiServicesTests
{
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
}
