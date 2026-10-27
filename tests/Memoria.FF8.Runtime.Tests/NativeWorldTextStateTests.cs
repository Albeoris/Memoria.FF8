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

