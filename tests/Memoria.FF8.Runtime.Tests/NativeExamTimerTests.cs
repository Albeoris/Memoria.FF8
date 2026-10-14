using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeExamTimerTests
{
    [Fact]
    public void ExplicitColdProcessTracksPreSetCallbacksAndWarmNewGamePreservesItsClockAndUi()
    {
        GameSession session = GameSession.CreateColdProcess();
        Assert.Equal(new NativeTimerAlternative(0, 0, 0), Assert.Single(session.ExamTimer!.Alternatives));
        session.StartNewGame();
        Assert.True(session.ReplayJournal!.IsComplete);
        NativeExamTimerState running = session.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 100).AdvanceCallbacks(NativeTimerCallbackKind.World, 3).AdvanceCallbacks(NativeTimerCallbackKind.MovieField, 2);
        Assert.False(running.TimerMemoryActivated);
        uint phase = unchecked(214u * 2191);
        Assert.Equal(new NativeTimerAlternative(phase, phase, 0), Assert.Single(running.Alternatives));
        NativeExamTimerState set = running.SetRemaining(60).Display(250, 10);
        Assert.Equal(new NativeTimerAlternative(phase, phase, 60), Assert.Single(set.Alternatives));
        NativeExamTimerState ticked = set.AdvanceCallbacks(NativeTimerCallbackKind.Menu, 1);
        Assert.Equal(new NativeTimerAlternative(unchecked(phase + 2191) & 0xFFFF, unchecked(phase + 2191) & 0xFFFF, 59), Assert.Single(ticked.Alternatives));
        session.SetExamTimer(ticked);
        session.StartNewGame();
        Assert.Equal(ticked.ClockCalls, session.ExamTimer!.ClockCalls);
        Assert.Equal(ticked.Alternatives[0].FirstPhase, Assert.Single(session.ExamTimer.Alternatives).FirstPhase);
        Assert.Equal(0, session.ExamTimer.RequireRemaining());
        Assert.True(session.ExamTimer.DisplayEnabled);
        Assert.Equal((ushort)250, session.ExamTimer.DisplayX);
        Assert.Equal((byte)10, session.ExamTimer.DisplayY);
        Assert.False(session.ExamTimer.TimerMemoryActivated);
        Assert.Equal(0x18u, session.WorldControls!.EventFlags104);
        Assert.False(session.ReplayJournal!.IsComplete);
        Assert.Contains("earlier process history", session.ReplayJournal.UnavailableReason);
    }

    [Fact]
    public void GenericCheckpointConstructionNeverCreatesAProcessClockAndUnknownNewGameIsAtomic()
    {
        GameState state = new();
        GameSession session = new(state);
        Assert.Null(session.ExamTimer);
        Assert.Throws<NotSupportedException>(session.StartNewGame);
        Assert.Same(state, session.State);
        NativeExamTimerState clock = NativeExamTimerState.FromColdProcess();
        string json = JsonSerializer.Serialize(clock);
        var missing = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        missing.AsObject().Remove("TimerMemoryActivated");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeExamTimerState>(missing.ToJsonString()));
    }

    [Fact]
    public void ReachedSetRetainsAllPossiblePriorPhasesAndDisplayDoesNotResetThem()
    {
        NativeExamTimerState initial = NativeExamTimerState.FromReachedSet(600);
        initial.Validate();
        Assert.Equal(new NativeTimerAlternative(0, uint.MaxValue, 600), Assert.Single(initial.Alternatives));
        NativeExamTimerState displayed = initial.Display(250, 10);
        Assert.Equal(initial.Alternatives, displayed.Alternatives);
        Assert.Equal((ushort)250, displayed.DisplayX);
        Assert.Equal((byte)10, displayed.DisplayY);
        Assert.True(displayed.SaveGlobalMask4);
        Assert.Equal("09:59", displayed.FieldDisplayText());
        Assert.Equal(displayed.Alternatives, displayed.KillDisplay().Alternatives);
        Assert.False(displayed.KillDisplay().SaveGlobalMask4);
    }

    [Fact]
    public void UnknownPhaseProducesCorrelatedPossibilitiesWithoutGuessingRemaining()
    {
        NativeExamTimerState state = NativeExamTimerState.FromReachedSet(600).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1);
        Assert.Equal(2, state.ClockCalls);
        Assert.Equal(599, state.Alternatives.Min(item => item.Remaining));
        Assert.Equal(600, state.Alternatives.Max(item => item.Remaining));
        Assert.Throws<NotSupportedException>(() => state.RequireRemaining());
        Assert.Equal("09:58–09:59", state.FieldDisplayText());
        Assert.All(state.Alternatives.Where(item => item.Remaining == 599), item => Assert.True(item.LastPhase < NativeExamTimerState.Threshold));
    }

    [Fact]
    public void ExactIntervalInterpreterMatchesIndependentNativeScalarClock()
    {
        foreach (int remaining in new[] { 0, 1, 600, -1, int.MinValue })
        {
            foreach (uint phase in new uint[] { 0, 65535, 128881, 131071, 131072, uint.MaxValue - 2191, uint.MaxValue })
            {
                NativeExamTimerState state = NativeExamTimerState.FromReachedSet(remaining) with { Alternatives = [new NativeTimerAlternative(phase, phase, remaining)] };
                uint expectedPhase = phase;
                int expectedRemaining = remaining;
                for (int callback = 0; callback < 100; callback++)
                {
                    for (int call = 0; call < 2; call++)
                    {
                        expectedPhase = unchecked(expectedPhase + 2191);
                        if ((expectedPhase & 0xFFFE0000) != 0 && expectedRemaining != 0)
                        {
                            expectedRemaining = unchecked(expectedRemaining - 1);
                            expectedPhase &= 0xFFFF;
                        }
                    }
                    state = state.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1);
                    Assert.Equal(new NativeTimerAlternative(expectedPhase, expectedPhase, expectedRemaining), Assert.Single(state.Alternatives));
                }
            }
        }
    }

    [Fact]
    public void IntervalImageMatchesEnumerationAcrossWrapAndTruncation()
    {
        foreach ((uint first, uint last) in new[] { (128800u, 131300u), (4294965000u, uint.MaxValue), (65530u, 132000u) })
        {
            NativeExamTimerState initial = NativeExamTimerState.FromReachedSet(2) with { Alternatives = [new NativeTimerAlternative(first, last, 2)] };
            NativeExamTimerState advanced = initial.AdvanceCallbacks(NativeTimerCallbackKind.Menu, 1);
            HashSet<(uint Phase, int Remaining)> expected = [];
            for (ulong value = first; value <= last; value++)
            {
                uint phase = unchecked((uint)value + 2191);
                int remaining = 2;
                if ((phase & 0xFFFE0000) != 0)
                {
                    remaining--;
                    phase &= 0xFFFF;
                }
                expected.Add((phase, remaining));
            }
            HashSet<(uint Phase, int Remaining)> actual = [];
            foreach (NativeTimerAlternative alternative in advanced.Alternatives)
            {
                for (ulong value = alternative.FirstPhase; value <= alternative.LastPhase; value++)
                {
                    actual.Add(((uint)value, alternative.Remaining));
                }
            }
            Assert.True(expected.SetEquals(actual));
        }
    }

    [Fact]
    public void ZeroRemainingDoesNotConvergeUnknownPhaseAndPauseDoesNotAdvance()
    {
        NativeExamTimerState state = NativeExamTimerState.FromReachedSet(0);
        NativeExamTimerState advanced = state.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 100);
        Assert.Equal(state.Alternatives, advanced.Alternatives);
        Assert.Equal(200, advanced.ClockCalls);
        Assert.Same(state, state.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 100, paused: true));
        Assert.Same(state, state.AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 100, clockDisabled: true));
        Assert.Equal(state.Alternatives.Select(item => item with { Remaining = 1200 }), advanced.SetRemaining(1200).Alternatives);
    }

    [Fact]
    public void JsonRoundTripPreservesCorrelationsAndMissingStateCannotSeedPhase()
    {
        NativeExamTimerState initial = NativeExamTimerState.FromReachedSet(600).Display(250, 10).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 120);
        NativeExamTimerState restored = JsonSerializer.Deserialize<NativeExamTimerState>(JsonSerializer.Serialize(initial))!;
        restored.Validate();
        Assert.Equal(initial.Alternatives, restored.Alternatives);
        Assert.Equal(initial.ClockCalls, restored.ClockCalls);
        Assert.Equal(initial.FieldDisplayText(), restored.FieldDisplayText());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeExamTimerState>("{}"));
        Assert.Throws<InvalidDataException>(() => (restored with { Alternatives = [new NativeTimerAlternative(2, 1, 0)] }).Validate());
    }
}
