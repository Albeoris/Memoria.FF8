using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldTextStateTests
{
    [Fact]
    public void OpeningTakesEightTicksAndSpeedAccumulatorConsumesFiveGlyphsInFourTicks()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([64, 64, 64, 64, 64, 0], 5120);
        for (int index = 0; index < 7; index++)
        {
            state = state.Advance();
            Assert.Equal(0, state.Position);
            Assert.False(state.TextComplete);
        }
        state = state.Advance();
        Assert.Equal(4096, state.Openness);
        Assert.Equal(1, state.Position);
        Assert.Equal(1024, state.Accumulator);
        state = state.Advance().Advance().Advance();
        Assert.Equal(5, state.Position);
        Assert.Equal(0, state.Accumulator);
        Assert.False(state.TextComplete);
        Assert.True(state.Advance().TextComplete);
    }

    [Fact]
    public void ColorAndNewlineContinueImmediatelyWhileKeyConsumesOneGlyphSlot()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([6, 34, 2, 5, 36, 64, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.Equal(5, state.Position);
        Assert.Equal(0, state.Accumulator);
        state = state.Advance();
        Assert.Equal(6, state.Position);
        Assert.False(state.TextComplete);
        Assert.True(state.Advance().TextComplete);
    }

    [Fact]
    public void InstantSpeedConsumesRemainingStreamInTheSameTick()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([8, 33, 64, 64, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.True(state.TextComplete);
        Assert.Equal(0, state.Speed);
        Assert.Equal(4, state.Position);
        Assert.Equal(state, state.Advance());
    }

    [Fact]
    public void SpeedZeroParameterEndsBeforeChangingTheSpeedOrReadingAnotherToken()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([8, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.True(state.TextComplete);
        Assert.Equal(1, state.Position);
        Assert.Equal(4096, state.Speed);
        state.Validate();
        NativeWorldTextState restored = JsonSerializer.Deserialize<NativeWorldTextState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(state, state.Advance());
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Throws<InvalidDataException>(() => NativeWorldTextState.Create([8, 0, 64, 0], 4096));
    }

    [Theory]
    [InlineData(32, 4096)]
    [InlineData(33, 0)]
    [InlineData(35, 2048)]
    public void ValidSpeedParametersPreserveTheirNativeAccumulatorContract(byte parameter, short expected)
    {
        NativeWorldTextState state = NativeWorldTextState.Create([8, parameter, 64, 64, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.Equal(expected, state.Speed);
        Assert.Equal(parameter == 33, state.TextComplete);
    }

    [Fact]
    public void PauseCountsActualTicksAndResumesWithoutInventingAGlyphBudget()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([9, 34, 64, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.Equal(15, state.Phase);
        Assert.Equal(1, state.PauseTicks);
        state = state.Advance().Advance();
        Assert.Equal(1, state.Phase);
        Assert.Equal(2, state.Position);
        Assert.Equal(3, state.Advance().Position);
    }

    [Fact]
    public void PageRequiresReleaseThenConfirmAndRestoresItsExactContinuation()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([1, 64, 0], 4096);
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance();
        }
        Assert.Equal(9, state.Phase);
        Assert.Equal(9, state.Advance(64).Phase);
        state = state.Advance();
        Assert.Equal(10, state.Phase);
        NativeWorldTextState restored = JsonSerializer.Deserialize<NativeWorldTextState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        state = state.Advance(64);
        restored = restored.Advance(64);
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        Assert.Equal(1, state.Position);
        Assert.Equal(1, state.Phase);
        Assert.Equal(2, state.Advance().Position);
        Assert.True(state.Advance().Advance().TextComplete);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void PageInputReachesOnlyFocusedChannelAndRestoresAcrossReleaseAndConfirm(byte separator)
    {
        NativeWorldMessage first = new(1, 1, 0, 0, 0, 8, 8, new string('A', 64), false) { Text = NativeWorldTextState.Create([64, separator, 65, 0], 0) };
        NativeWorldMessage second = first with { WindowId = 2, Channel = 1 };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(first).Show(second).AdvanceText(8, 64);
        Assert.Equal((byte)1, ui.PageInputChannel);
        Assert.All(ui.Windows, window => Assert.False(window.TextComplete));
        ui = ui.AdvanceText(2, 64);
        Assert.Equal(9, ui.FocusedPageMessage!.Text!.Phase);
        ui = ui.AdvanceText(1);
        Assert.Equal(10, ui.FocusedPageMessage!.Text!.Phase);
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(ui))!;
        restored.Validate();
        ui = ui.AdvanceText(2, 64);
        restored = restored.AdvanceText(2, 64);
        Assert.Equal(JsonSerializer.Serialize(ui), JsonSerializer.Serialize(restored));
        Assert.False(ui.Windows.Single(window => window.Channel == 0).TextComplete);
        Assert.True(ui.Windows.Single(window => window.Channel == 1).TextComplete);
        ui = ui.Close(2);
        Assert.Equal((byte)0, ui.PageInputChannel);
        Assert.Equal((byte)0, ui.FocusedPageMessage!.Channel);
        ui = ui.AdvanceText(2, 64);
        Assert.False(Assert.Single(ui.Windows).TextComplete);
        ui = ui.AdvanceText(1).AdvanceText(2, 64);
        Assert.True(Assert.Single(ui.Windows).TextComplete);
        Assert.Empty(ui.Close(1).Windows);
        Assert.Null(ui.Close(1).PageInputChannel);
    }

    [Fact]
    public void CurrentPageChangesOnlyAfterConfirmAndPreservesInheritedColor()
    {
        NativeWorldTextState state = NativeWorldTextState.Create([6, 34, 64, 1, 65, 6, 35, 66, 7, 67, 0], 0);
        Assert.Equal(new byte[] { 6, 34, 64 }, state.CurrentPageBytes());
        for (int index = 0; index < 8; index++)
        {
            state = state.Advance(64);
        }
        Assert.Equal(new byte[] { 6, 34, 64 }, state.CurrentPageBytes());
        state = state.Advance().Advance(64);
        Assert.Equal(new byte[] { 6, 34, 65, 6, 35, 66 }, state.CurrentPageBytes());
        state = state.Advance().Advance().Advance(64);
        Assert.Equal(new byte[] { 6, 35, 67 }, state.CurrentPageBytes());
        Assert.True(state.Advance().TextComplete);
    }

    [Fact]
    public void ReturningFocusRequiresFreshReleaseEvenWithoutAnInterveningTick()
    {
        NativeWorldMessage first = new(1, 1, 0, 0, 0, 8, 8, new string('A', 64), false) { Text = NativeWorldTextState.Create([1, 64, 0], 0) };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(first).AdvanceText(9);
        Assert.Equal(10, ui.FocusedPageMessage!.Text!.Phase);
        ui = ui.Show(first with { WindowId = 2, Channel = 1 }).Close(2);
        Assert.Equal(9, ui.FocusedPageMessage!.Text!.Phase);
        Assert.False(Assert.Single(ui.AdvanceText(2, 64).Windows).TextComplete);
        Assert.True(Assert.Single(ui.AdvanceText(1).AdvanceText(2, 64).Windows).TextComplete);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void PageDetectionDoesNotInterpretControlOperandsAsPageBoundaries(byte operand)
    {
        NativeWorldTextState state = new([6, operand, 64, 0], 0, 4096, 0, 0, 0, 0, 0, false);
        Assert.False(state.HasPages);
        Assert.Throws<InvalidDataException>(() => state.Validate());
        Assert.True((state with { Bytes = [6, 34, operand, 64, 0] }).HasPages);
    }

    [Fact]
    public void OrdinaryMessageDoesNotStealPageFocusAndChannelReplacementClearsIt()
    {
        NativeWorldMessage paged = new(1, 1, 0, 0, 0, 8, 8, new string('A', 64), false) { Text = NativeWorldTextState.Create([1, 64, 0], 0) };
        NativeWorldMessage ordinary = paged with { WindowId = 2, Channel = 1, Text = NativeWorldTextState.Create([64, 0], 0) };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(paged).Show(ordinary);
        Assert.Equal((byte)0, ui.PageInputChannel);
        Assert.Equal(1, ui.FocusedPageMessage!.WindowId);
        ui = ui.Show(ordinary with { WindowId = 3, Channel = 0 });
        Assert.Null(ui.PageInputChannel);
        ui.Validate();
        Assert.Throws<InvalidDataException>(() => (ui with { PageInputChannel = 0 }).Validate());
        NativeWorldTextState invalidWait = NativeWorldTextState.Create([64, 0], 4096) with { Phase = 10 };
        Assert.Throws<InvalidDataException>(() => invalidWait.Validate());
    }

    [Fact]
    public void UnrepresentedSubstitutionsAndMalformedControlsRefuseAtCreation()
    {
        Assert.Throws<NotSupportedException>(() => NativeWorldTextState.Create([14, 45, 0], 4096));
        Assert.Throws<NotSupportedException>(() => NativeWorldTextState.Create([232, 0], 4096));
        Assert.Throws<InvalidDataException>(() => NativeWorldTextState.Create([6, 0], 4096));
        Assert.Throws<InvalidDataException>(() => NativeWorldTextState.Create([64, 0, 64, 0], 4096));
    }

    [Fact]
    public void PhysicalChannelReopenRetainsClosingOpennessButResetsItsGlyphRunner()
    {
        NativeWorldMessage message = new(9, 49, 0, 0, 0, 32, 16, new string('A', 64), false) { Text = NativeWorldTextState.Create([64, 64, 0], 4096) };
        NativeWorldEventUi ui = NativeWorldEventUi.Create().Show(message).AdvanceText(8);
        Assert.Equal(1, Assert.Single(ui.Windows).Text!.Position);
        ui = ui.Close(9).AdvanceText(2);
        Assert.Equal((short)3072, Assert.Single(ui.ClosingChannels).Openness!.Value);
        NativeWorldEventUi restored = JsonSerializer.Deserialize<NativeWorldEventUi>(JsonSerializer.Serialize(ui))!;
        restored.Validate();
        ui = restored.Show(message with { MessageId = 50 });
        Assert.Empty(ui.ClosingChannels);
        Assert.Equal(3072, Assert.Single(ui.Windows).Text!.Openness);
        Assert.Equal(0, Assert.Single(ui.Windows).Text!.Position);
        Assert.False(ui.MatchesTextComplete(9, 1));
        Assert.Equal(1, Assert.Single(ui.AdvanceText(2).Windows).Text!.Position);
        Assert.Empty(ui.Close(9).AdvanceText(8).ClosingChannels);
    }

    [Fact]
    public void HistoricalClosingOpennessRemainsUnknownUntilItsNativeUpperBoundExpires()
    {
        NativeWorldMessage historical = new(9, 49, 0, 0, 0, 32, 16, new string('A', 64), null);
        NativeWorldMessage prepared = historical with { MessageId = 50, TextComplete = false, Text = NativeWorldTextState.Create([64, 0], 4096) };
        NativeWorldEventUi closing = NativeWorldEventUi.Create().Show(historical).Close(9);
        Assert.Throws<NotSupportedException>(() => NativeWorldEventUi.Create().Show(historical).Show(prepared));
        Assert.Throws<NotSupportedException>(() => closing.AdvanceText(7).Show(prepared));
        Assert.Equal(0, Assert.Single(closing.AdvanceText(8).Show(prepared).Windows).Text!.Openness);
    }
}

