using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeRenzokukenRuleTests
{
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
