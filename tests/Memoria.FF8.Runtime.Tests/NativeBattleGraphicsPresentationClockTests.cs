using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsPresentationClockTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    public void Display_rate_does_not_change_native_time_or_train_pulses(int displayRate)
    {
        NativeBattleGraphicsPresentationStep clock = new(0, 0, false);
        NativeBattleStageSpecializedState stage = NativeBattleStageSpecializedState.FromColdProcess();
        long previous = 0;
        long callbacks = 0;
        int pulses = 0;
        for (int frame = 1; frame <= displayRate * 10; frame++)
        {
            long timestamp = (long)frame * TimeSpan.TicksPerSecond / displayRate;
            clock = NativeBattleGraphicsPresentationClock.Advance(clock.Subticks, clock.HalfSubtick, TimeSpan.FromTicks(timestamp - previous));
            previous = timestamp;
            callbacks += clock.Callbacks;
            for (long index = 0; index < clock.Callbacks; index++)
            {
                NativeBattleStageSpecializedStep step = NativeBattleStageSpecialized.Advance(stage, 0x519FC0, 1, 0, 0, 0, () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
                stage = step.State;
                pulses += step.CameraShake == -1 ? 1 : 0;
            }
        }
        Assert.Equal(155, callbacks);
        Assert.Equal(155, stage.Counter19);
        Assert.Equal(77, pulses);
        Assert.Equal(0, clock.Subticks);
        Assert.False(clock.HalfSubtick);
    }

    [Fact]
    public void Callback_duration_conversion_uses_the_same_fixed_rate()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(31));
        Assert.Equal(TimeSpan.FromTicks(645_161), NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1));
        Assert.Equal(TimeSpan.Zero, NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(-1));
    }

    [Fact]
    public void Ordinary_battle_admits_31_callbacks_in_two_seconds()
    {
        Assert.Equal(new NativeBattleGraphicsPresentationStep(31, 0, false), NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromSeconds(2)));
        Assert.Equal(new NativeBattleGraphicsPresentationStep(15, 5_000_000, false), NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Odd_tick_partition_and_json_restore_preserve_fractional_phase()
    {
        NativeBattleGraphicsState graphics = new([], [], 0, 0);
        long callbacks = 0;
        for (int index = 0; index < 1000; index++)
        {
            NativeBattleGraphicsPresentationStep step = NativeBattleGraphicsPresentationClock.Advance(graphics.PresentationSubticks, graphics.PresentationHalfSubtick, TimeSpan.FromTicks(333_333));
            callbacks += step.Callbacks;
            graphics = graphics with { PresentationSubticks = step.Subticks, PresentationHalfSubtick = step.HalfSubtick };
            if (index == 500)
            {
                Assert.True(graphics.PresentationHalfSubtick);
                graphics = JsonSerializer.Deserialize<NativeBattleGraphicsState>(JsonSerializer.Serialize(graphics))!;
            }
        }
        Assert.Equal(new NativeBattleGraphicsPresentationStep(callbacks, graphics.PresentationSubticks, graphics.PresentationHalfSubtick), NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(333_333_000)));
    }

    [Fact]
    public void Train_pulses_retain_one_pixel_amplitude_at_native_callback_rate()
    {
        NativeBattleStageSpecializedState stage = NativeBattleStageSpecializedState.FromColdProcess();
        long callbacks = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromSeconds(2)).Callbacks;
        int pulses = 0;
        for (int index = 0; index < callbacks; index++)
        {
            NativeBattleStageSpecializedStep step = NativeBattleStageSpecialized.Advance(stage, 0x519FC0, 1, 0, 0, 0, () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
            stage = step.State;
            Assert.Equal(index % 2 == 0 ? 0 : -1, step.CameraShake);
            if (step.CameraShake != 0)
            {
                pulses++;
            }
        }
        Assert.Equal(15, pulses);
    }
}
