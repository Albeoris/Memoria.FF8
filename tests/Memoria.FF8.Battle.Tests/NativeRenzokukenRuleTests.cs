using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeRenzokukenRuleTests
{
    [Fact]
    public void Early_trigger_locks_that_contact_while_a_later_contact_can_accept_its_own_window()
    {
        BattleNativeRenzokukenTimeline timeline = new([11, 51], 24);
        BattleNativeRenzokukenInputStep early = NativeBattleRenzokukenRules.AdvanceInput(timeline, new(4, 8), true, true, false, null);
        BattleNativeRenzokukenInputStep ignored = NativeBattleRenzokukenRules.AdvanceInput(timeline, early.State with { PoseClock = 28 }, true, true, false, null);
        BattleNativeRenzokukenInputStep first = NativeBattleRenzokukenRules.AdvanceInput(timeline, ignored.State with { PoseClock = 40 }, true, false, false, null);

        Assert.Equal((short)1, first.Contact!.TriggerTiming);
        Assert.Equal((byte)1, first.Contact.Index);
        BattleNativeRenzokukenInputStep second = NativeBattleRenzokukenRules.AdvanceInput(timeline, first.State with { PoseClock = 68 }, true, true, false, null);
        BattleNativeRenzokukenInputStep final = NativeBattleRenzokukenRules.AdvanceInput(timeline, second.State with { PoseClock = 80 }, true, false, false, null);

        Assert.Equal((short)22, final.Contact!.TriggerTiming);
        Assert.Equal((byte)2, final.Contact.Index);
        Assert.Equal((byte)8, final.State.Phase);
        Assert.Null(final.Random);
    }

    [Fact]
    public void Automatic_trigger_uses_the_distinct_battle_UI_generator_once_at_distance_twenty_five()
    {
        BattleNativeRenzokukenTimeline timeline = new([11], 24);
        NativeBattleUiRandomState random = new(100);
        NativeBattleUiRandomState expected = random.Draw(out ushort roll);

        BattleNativeRenzokukenInputStep trigger = NativeBattleRenzokukenRules.AdvanceInput(timeline, new(6, 36), true, false, true, random);
        BattleNativeRenzokukenInputStep contact = NativeBattleRenzokukenRules.AdvanceInput(timeline, trigger.State, true, false, true, trigger.Random);

        Assert.Null(trigger.Contact);
        Assert.Equal(expected, contact.Random);
        Assert.Equal(roll <= 0x2000 ? (short)1 : (short)25, contact.Contact!.TriggerTiming);
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokukenRules.AdvanceInput(timeline, new(6, 36), true, false, true, null));
    }

    [Fact]
    public void UI_subframes_saturate_without_pose_progress_and_each_pose_edge_advances_four_ticks()
    {
        BattleNativeRenzokukenTimeline timeline = new([31], 24);
        BattleNativeRenzokukenInputState state = new(4, 0);
        for (int callback = 0; callback < 10; callback++)
        {
            state = NativeBattleRenzokukenRules.AdvanceInput(timeline, state, false, false, false, null).State;
        }

        Assert.Equal((short)0, state.PoseClock);
        Assert.Equal((short)3, state.SubframeClock);
        state = NativeBattleRenzokukenRules.AdvanceInput(timeline, state, true, false, false, null).State;
        Assert.Equal((short)4, state.PoseClock);
        Assert.Equal((short)0, state.SubframeClock);
    }

    [Fact]
    public void Native_thresholds_include_first_segment_bias_and_capped_final_tail()
    {
        BattleNativeRenzokukenTimeline timeline = NativeBattleRenzokukenRules.Timeline([20, 14, 40]);

        Assert.Equal(new short[] { 61, 117 }, timeline.InputThresholds);
        Assert.Equal((short)46, timeline.TotalPoseFrames);
    }

    [Fact]
    public void Native_segment_bytes_are_sign_extended_before_WORD_timing_projection()
    {
        BattleNativeRenzokukenTimeline timeline = NativeBattleRenzokukenRules.Timeline([255, 40]);

        Assert.Equal(new short[] { -23 }, timeline.InputThresholds);
        Assert.Equal((short)11, timeline.TotalPoseFrames);
    }

    [Fact]
    public void Finisher_acceptance_and_enabled_weapon_selection_preserve_original_inclusive_boundaries()
    {
        Assert.False(NativeBattleRenzokukenRules.HasFinisher(0, 0));
        Assert.True(NativeBattleRenzokukenRules.HasFinisher(1, 60));
        Assert.False(NativeBattleRenzokukenRules.HasFinisher(1, 61));
        Assert.True(NativeBattleRenzokukenRules.HasFinisher(4, 240));
        Assert.False(NativeBattleRenzokukenRules.HasFinisher(4, 241));
        Assert.Equal((byte)2, NativeBattleRenzokukenRules.Finisher(5, 3));
        Assert.Equal((byte)0, NativeBattleRenzokukenRules.Finisher(5, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleRenzokukenRules.Finisher(4, 1));
    }
}
