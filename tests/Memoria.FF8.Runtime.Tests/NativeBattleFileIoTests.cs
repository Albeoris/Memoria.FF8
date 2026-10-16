using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFileIoTests
{
    [Theory]
    [InlineData(0, 0, null, 0)]
    [InlineData(20479, 0, null, 0)]
    [InlineData(20480, 0, null, 1)]
    [InlineData(40960, 0, null, 2)]
    [InlineData(-20479, 0, null, 0)]
    [InlineData(-20480, 0, null, -1)]
    [InlineData(int.MaxValue, 0, null, -26215)]
    [InlineData(int.MinValue, 0, null, 26215)]
    [InlineData(20480, 7, 0, 2)]
    [InlineData(20480, 7, 2, 2)]
    [InlineData(20480, 7, 4, 7)]
    [InlineData(20480, -1, 4, -1)]
    public void Countdown_uses_signed_high_product_mode_and_truncated_WORD(int length, short delayOverride, int? outerPhase, short expected)
    {
        Assert.Equal(expected, NativeBattleFileIoState.CalculateCountdown482610(length, delayOverride, outerPhase));
    }

    [Fact]
    public void Zero_is_tested_before_decrement_and_only_first_occupied_slot_advances()
    {
        NativeBattleFileIoState state = NativeBattleFileIoState.FromColdProcess().Queue482610(40960, 0x508470, null).Queue482610(20480, 0x505E10, null);
        List<NativeBattleFileIoCompletion> completions = [];
        NativeBattleFileIoState Dispatch(NativeBattleFileIoState current, NativeBattleFileIoCompletion completion)
        {
            completions.Add(completion);
            Assert.Equal((short)-1, current.Rows[completion.Slot].Status);
            Assert.Equal((short)0, current.Rows[completion.Slot].Countdown);
            Assert.Equal(0x482870u, current.Rows[completion.Slot].SchedulerCallback);
            return current;
        }
        state = state.Advance482590(Dispatch);
        state = state.Advance482590(Dispatch);
        Assert.Empty(completions);
        Assert.Equal((short)0, state.Rows[0].Countdown);
        Assert.Equal((short)1, state.Rows[1].Countdown);
        state = state.Advance482590(Dispatch);
        Assert.Equal(new NativeBattleFileIoCompletion(0, 0x508470, 40960), Assert.Single(completions));
        Assert.Equal((short)-1, state.Rows[0].Countdown);
        Assert.Equal(0u, state.Rows[0].SchedulerCallback);
        Assert.Equal((short)0, state.Rows[0].Status);
        Assert.Equal((short)1, state.ActiveCount);
        Assert.Equal((short)1, state.Rows[1].Countdown);
        state = state.Advance482590(Dispatch);
        Assert.Single(completions);
        state = state.Advance482590(Dispatch);
        Assert.Equal(2, completions.Count);
        Assert.Equal((short)0, state.ActiveCount);
        Assert.Equal((short)0, state.CurrentSlot);
    }

    [Fact]
    public void Enqueue_during_completion_keeps_executing_slot_occupied_and_does_not_advance_child()
    {
        NativeBattleFileIoState state = NativeBattleFileIoState.FromColdProcess().Queue482610(12, 0x508470, null);
        state = state.Advance482590((current, completion) =>
        {
            Assert.Equal(0, completion.Slot);
            Assert.Equal((short)1, current.ActiveCount);
            return current.Queue482610(20480, 0x505E10, null);
        });
        Assert.Equal(0u, state.Rows[0].SchedulerCallback);
        Assert.Equal((short)-1, state.Rows[0].Countdown);
        Assert.Equal(0x482870u, state.Rows[1].SchedulerCallback);
        Assert.Equal((short)1, state.Rows[1].Countdown);
        Assert.Equal((short)1, state.ActiveCount);
    }

    [Fact]
    public void Full_callback_table_overwrites_row_zero_without_increment_and_preserves_status()
    {
        NativeBattleFileIoState state = NativeBattleFileIoState.FromColdProcess();
        for (int index = 0; index < 16; index++)
        {
            state = state.Queue482610(0, checked((uint)(0x508470 + index)), null);
        }
        Assert.Equal((short)16, state.ActiveCount);
        state = state.Advance482590((current, _) => current.Queue482610(40960, 0x505E10, null));
        Assert.Equal((short)15, state.ActiveCount);
        Assert.Equal(0u, state.Rows[0].SchedulerCallback);
        Assert.Equal(40960, state.Rows[0].FileResult);
        Assert.Equal(0x505E10u, state.Rows[0].CompletionCallback);
        Assert.Equal((short)1, state.Rows[0].Countdown);
        Assert.Equal((short)0, state.Rows[0].Status);
        Assert.All(state.Rows.Skip(1), row => Assert.Equal(0x482870u, row.SchedulerCallback));
    }

    [Fact]
    public void Signed_countdown_wraps_without_early_completion()
    {
        NativeBattleFileIoState state = (NativeBattleFileIoState.FromColdProcess() with { DelayOverride = short.MinValue }).Queue482610(1, 0x508470, 4);
        state = state.Advance482590((_, _) => throw new InvalidOperationException("A negative WORD is not zero."));
        Assert.Equal(short.MaxValue, state.Rows[0].Countdown);
        Assert.Equal((short)1, state.ActiveCount);
    }

    [Fact]
    public void Reset_writes_only_callback_slots_status_and_count_and_unknowns_fail_on_actual_read()
    {
        NativeBattleFileIoState state = (NativeBattleFileIoState.FromColdProcess() with { DelayOverride = 7, CurrentSlot = 9 }).Queue482610(123456, 0x508470, 4);
        NativeBattleFileIoState reset = state.Reset482560();
        Assert.Equal(state.Rows[0] with { SchedulerCallback = 0, Status = 0 }, reset.Rows[0]);
        Assert.Equal((short)7, reset.DelayOverride);
        Assert.Equal((short)9, reset.CurrentSlot);
        Assert.Equal((short)0, reset.ActiveCount);
        NativeBattleFileIoState unknown = NativeBattleFileIoState.Unknown().Reset482560();
        Assert.All(unknown.Rows, row =>
        {
            Assert.Equal(0u, row.SchedulerCallback);
            Assert.Equal((short)0, row.Status);
            Assert.Null(row.Countdown);
            Assert.Null(row.FileResult);
            Assert.Null(row.CompletionCallback);
        });
        Assert.Null(unknown.DelayOverride);
        Assert.Null(unknown.CurrentSlot);
        Assert.Throws<NotSupportedException>(() => unknown.Queue482610(1, 0x508470, 4));
        Assert.Throws<NotSupportedException>(() => NativeBattleFileIoState.CalculateCountdown482610(1, 1, null));
        Assert.Same(unknown, unknown.Queue482610(1, 0, null));
        Assert.Equal((short)0, unknown.Advance482590((current, _) => current).CurrentSlot);
    }

    [Fact]
    public void Mid_delay_JSON_continuation_matches_exact_signed_completion_and_retained_rows()
    {
        NativeBattleFileIoState state = NativeBattleFileIoState.FromColdProcess().Queue482610(40960, 0x508470, null).Queue482610(-20479, 0x508470, null);
        state = state.Advance482590((current, _) => current);
        string json = JsonSerializer.Serialize(state);
        NativeBattleFileIoState restored = JsonSerializer.Deserialize<NativeBattleFileIoState>(json)!;
        List<int> results = [];
        List<int> restoredResults = [];
        for (int index = 0; index < 3; index++)
        {
            state = state.Advance482590((current, completion) =>
            {
                results.Add(completion.Result);
                return current;
            });
            restored = restored.Advance482590((current, completion) =>
            {
                restoredResults.Add(completion.Result);
                return current;
            });
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        }
        Assert.Equal(new[] { 40960, -20479 }, results);
        Assert.Equal(results, restoredResults);
        Assert.Equal((short)0, state.ActiveCount);
    }
}
