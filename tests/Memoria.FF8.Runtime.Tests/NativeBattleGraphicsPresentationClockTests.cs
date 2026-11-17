using static Memoria.FF8.Tests.NativeBattleTypedFixture;

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
        BattlePresentationCallbackStep clock = new(0, 0, false);
        NativeBattleStageSpecializedState stage = NativeBattleStageSpecializedState.FromColdProcess();
        long previous = 0;
        long callbacks = 0;
        int pulses = 0;
        for (int frame = 1; frame <= displayRate * 10; frame++)
        {
            long timestamp = (long)frame * TimeSpan.TicksPerSecond / displayRate;
            clock = BattlePresentationTiming.Advance(clock.Subticks, clock.HalfSubtick, TimeSpan.FromTicks(timestamp - previous));
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
        Assert.Equal(TimeSpan.FromSeconds(2), BattlePresentationTiming.ElapsedForCallbacks(31));
        Assert.Equal(TimeSpan.FromTicks(645_162), BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.Equal(TimeSpan.Zero, BattlePresentationTiming.ElapsedForCallbacks(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattlePresentationTiming.ElapsedForCallbacks(-1));
    }

    [Fact]
    public void Ordinary_battle_admits_31_callbacks_in_two_seconds()
    {
        Assert.Equal(new BattlePresentationCallbackStep(31, 0, false), BattlePresentationTiming.Advance(0, false, TimeSpan.FromSeconds(2)));
        Assert.Equal(new BattlePresentationCallbackStep(15, 5_000_000, false), BattlePresentationTiming.Advance(0, false, TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void Shot_countdown_reads_native_service_polls_at_any_host_update_rate(int updatesPerSecond)
    {
        NativeBattleShotWidgetState widget = NativeBattleShotWidgetState.Register(400)
            .Advance(frame33: false, fired: false, closeRequested: false, ammunition: 3).Widget;
        Assert.Equal((short)399, widget.RemainingTimer);
        BattlePresentationCallbackStep clock = default;
        long callbacks = 0;
        const int polls = BattlePresentationTiming.NativeUiServicePollsPerCallback;
        for (int update = 0; update < 2 * updatesPerSecond; update++)
        {
            long before = update * TimeSpan.TicksPerSecond / updatesPerSecond;
            long after = (update + 1) * TimeSpan.TicksPerSecond / updatesPerSecond;
            clock = BattlePresentationTiming.Advance(clock.Subticks, clock.HalfSubtick, TimeSpan.FromTicks(after - before));
            callbacks += clock.Callbacks;
            for (long callback = 0; callback < clock.Callbacks; callback++)
            {
                for (int poll = 0; poll < polls; poll++)
                {
                    widget = widget.Advance(frame33: poll == polls - 1, fired: false, closeRequested: false, ammunition: 3).Widget;
                }
            }
        }
        Assert.Equal(31, callbacks);
        Assert.Equal((short)276, widget.RemainingTimer);

        for (int poll = 0; poll < 2 * polls; poll++)
        {
            widget = widget.Advance(frame33: false, fired: false, closeRequested: false, ammunition: 3).Widget;
        }
        Assert.Equal((short)273, widget.RemainingTimer);
        Assert.Equal((byte)0, widget.PublicationLatch);
    }

    [Fact]
    public void Odd_tick_partition_and_private_capture_preserve_fractional_phase()
    {
        NativeBattleGraphicsState graphics = new(new BattleActorModels([]), NativeBattleModelBoundary.FromRetainedMemory(null), 0, 0, Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalEffects());
        long callbacks = 0;
        for (int index = 0; index < 1000; index++)
        {
            BattlePresentationCallbackStep step = BattlePresentationTiming.Advance(graphics.PresentationSubticks, graphics.PresentationHalfSubtick, TimeSpan.FromTicks(333_333));
            callbacks += step.Callbacks;
            graphics = graphics with { PresentationSubticks = step.Subticks, PresentationHalfSubtick = step.HalfSubtick };
            if (index == 500)
            {
                Assert.True(graphics.PresentationHalfSubtick);
                graphics = NativeBattleStorageSnapshots.Fork(graphics);
            }
        }
        Assert.Equal(new BattlePresentationCallbackStep(callbacks, graphics.PresentationSubticks, graphics.PresentationHalfSubtick), BattlePresentationTiming.Advance(0, false, TimeSpan.FromTicks(333_333_000)));
    }

    [Fact]
    public void Train_pulses_retain_one_pixel_amplitude_at_native_callback_rate()
    {
        NativeBattleStageSpecializedState stage = NativeBattleStageSpecializedState.FromColdProcess();
        long callbacks = BattlePresentationTiming.Advance(0, false, TimeSpan.FromSeconds(2)).Callbacks;
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
