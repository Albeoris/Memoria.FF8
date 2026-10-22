using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTriggerSoundTests
{
    [Theory]
    [InlineData(14, false, 1)]
    [InlineData(15, true, 20)]
    [InlineData(20, true, 25)]
    public void Manual_edge_requests_sound_only_for_an_accepted_trigger(int distance, bool expectedSound, short expectedTiming)
    {
        BattleNativeRenzokukenTimeline timeline = NativeBattleRenzokukenRules.Timeline([16, 9]);
        int clock = timeline.InputThresholds[0] + distance;
        BattleNativeRenzokukenInputState input = new(6, checked((short)(clock / 4 * 4)), checked((short)(clock % 4)), 0, 0);
        BattleNativeRenzokukenInputStep step = NativeBattleRenzokukenRules.AdvanceInput(timeline, input, false, true, false, null);
        Assert.Equal(expectedSound, step.PlayTriggerSound);
        Assert.Equal(expectedTiming, step.State.TriggerTiming);
        Assert.False(NativeBattleRenzokukenRules.AdvanceInput(timeline, step.State, false, true, false, null).PlayTriggerSound);
    }

    [Fact]
    public void Native_auto_failure_still_requests_the_original_shot_sound()
    {
        BattleNativeRenzokukenTimeline timeline = NativeBattleRenzokukenRules.Timeline([16, 9]);
        int clock = timeline.InputThresholds[0] + 25;
        NativeBattleUiRandomState random = Enumerable.Range(0, 0x8000).Select(seed => new NativeBattleUiRandomState((uint)seed)).First(candidate =>
        {
            candidate.Draw(out ushort roll);
            return roll <= 0x2000;
        });
        BattleNativeRenzokukenInputState input = new(6, checked((short)(clock / 4 * 4)), checked((short)(clock % 4)), 0, 0);
        BattleNativeRenzokukenInputStep step = NativeBattleRenzokukenRules.AdvanceInput(timeline, input, false, false, true, random);
        Assert.Equal((short)1, step.State.TriggerTiming);
        Assert.True(step.PlayTriggerSound);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 42)]
    public void Shot_sound_selects_the_original_actor_slot_and_retains_bone_anchor_and_pan(byte model, int expectedIndex)
    {
        ImmutableArray<uint> table = Enumerable.Range(0, 1120).Select(value => (uint)value).ToImmutableArray();
        NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(model, new(10, 20, 30), 201, table);
        Assert.Equal(table[expectedIndex], sound.Sound);
        Assert.Equal(0u, sound.Flags);
        Assert.Equal(127u, sound.Volume);
        Assert.Equal((byte)201, sound.Pan);
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 0), sound.Anchor);
        Assert.Throws<NotSupportedException>(() => NativeBattleTriggerAudio.Resolve501640(model, default, 128, null));
    }

    [Fact]
    public void Pending_shot_retains_its_AB_owner_through_snapshot_serialization()
    {
        NativeBattleGraphicsState state = new([], [], 0, 0) { PendingTriggerSoundActorIndex = 2 };
        NativeBattleGraphicsState restored = JsonSerializer.Deserialize<NativeBattleGraphicsState>(JsonSerializer.Serialize(state))!;
        Assert.Equal((byte)2, restored.PendingTriggerSoundActorIndex);
    }
}
