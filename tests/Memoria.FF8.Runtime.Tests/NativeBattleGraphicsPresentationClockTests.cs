using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsPresentationClockTests
{
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
