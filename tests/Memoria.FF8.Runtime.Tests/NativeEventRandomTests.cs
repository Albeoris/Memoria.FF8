using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeEventRandomTests
{
    [Fact]
    public void AuthenticatedColdGeneratorMatchesNativeIndependentByteSequence()
    {
        NativeEventRandomState random = NativeEventRandomState.FromColdProcess();
        Assert.Null(random.DrawPoints);
        int[] expected = [198, 126, 129, 107, 75, 251, 226, 251, 84, 246, 189, 223, 124, 28, 225, 135];
        foreach (int value in expected)
        {
            random = random.Draw(out int result);
            Assert.Equal(value, result & 255);
            Assert.InRange(result, 0, 32767);
        }
        Assert.Equal(0xC487EB71u, random.State);
        NativeEventRandomState overflow = new NativeEventRandomState(uint.MaxValue, 0, null).Draw(out int overflowingResult);
        Assert.Equal(0xBE39E1CCu, overflow.State);
        Assert.Equal(15929, overflowingResult);
    }

    [Fact]
    public void ExecutedNewGameClearsSavedBankAndPreservesProcessGenerator()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        NativeEventRandomState initial = session.EventRandom!;
        Assert.True(initial.DrawPoints!.Depletion.All(value => value == 0));
        NativeEventRandomState moved = initial.Draw(out _);
        session.SetEventRandom(moved with { DrawPoints = moved.DrawPoints!.Write(116, 4, -1).Write(180, 2, 1234) });
        session.StartNewGame();
        Assert.Equal(moved.State, session.EventRandom!.State);
        Assert.Equal((ushort)0, session.EventRandom.DrawPoints!.RegenerationCounter180);
        Assert.True(session.EventRandom.DrawPoints.Depletion.All(value => value == 0));
        Assert.False(session.ReplayJournal!.IsComplete);
    }

    [Fact]
    public void ConditionalPackedAgingConsumesOnlyAscendingStatesOneAndTwo()
    {
        NativeEventRandomState initial = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame();
        // Native indices0/1/2/3 hold1/2/0/3. First draw even leaves1; second even leaves2.
        NativeDrawPointMemory bank = initial.DrawPoints!.Write(116, 1, 0xC9).Write(180, 2, 0x27FF);
        NativeEventRandomState first = (initial with { DrawPoints = bank }).AdvanceMovement(1);
        Assert.Equal(0x967EB0E7u, first.State);
        Assert.Equal(0xC9, first.DrawPoints!.Read(116, 1));
        Assert.Equal((ushort)0, first.DrawPoints.RegenerationCounter180);
        NativeEventRandomState second = (first with { DrawPoints = first.DrawPoints.Write(180, 2, 0x2800) }).AdvanceMovement(0);
        Assert.Equal(0xC46B9B3Du, second.State);
        Assert.Equal(0xC4, second.DrawPoints!.Read(116, 1));
        NativeEventRandomState noEligible = (initial with { DrawPoints = initial.DrawPoints!.Write(116, 1, 0xCC).Write(180, 2, 0xFFFF) }).AdvanceMovement(0);
        Assert.Equal(1u, noEligible.State);
        Assert.Equal(0xCC, noEligible.DrawPoints!.Read(116, 1));
        Assert.Equal((ushort)0, noEligible.DrawPoints.RegenerationCounter180);
    }

    [Fact]
    public void NativeWordWrapPrecedesThresholdAndResetDiscardsOvershoot()
    {
        NativeEventRandomState initial = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame();
        NativeEventRandomState wrapped = (initial with { DrawPoints = initial.DrawPoints!.Write(180, 2, 65535) }).AdvanceMovement(2);
        Assert.Equal((ushort)1, wrapped.DrawPoints!.RegenerationCounter180);
        Assert.Equal(1u, wrapped.State);
        NativeEventRandomState reset = (initial with { DrawPoints = initial.DrawPoints!.Write(180, 2, 0x27FF) }).AdvanceMovement(5792);
        Assert.Equal((ushort)0, reset.DrawPoints!.RegenerationCounter180);
        Assert.Equal(1u, reset.State);
        NativeDrawPointMemory overlap = initial.DrawPoints!.Write(178, 4, unchecked((int)0xABCD1234));
        Assert.Equal(unchecked((int)0xABCD1234), overlap.Read(178, 4));
        Assert.Equal((ushort)0xABCD, overlap.RegenerationCounter180);
        Assert.Throws<NotSupportedException>(() => overlap.Read(179, 4));
        Assert.Throws<NotSupportedException>(() => overlap.Write(115, 2, 1));
    }

    [Fact]
    public void CurrentShapeRoundTripsAndMissingProducerRefuses()
    {
        NativeEventRandomState live = NativeEventRandomState.FromColdProcess().ClearSavedStateForNewGame();
        live = live with { DrawPoints = live.DrawPoints!.Write(116, 1, 2).Write(180, 2, 0x27FF) };
        NativeEventRandomState restored = JsonSerializer.Deserialize<NativeEventRandomState>(JsonSerializer.Serialize(live))!;
        Assert.Equal(live, restored);
        Assert.Equal(live.AdvanceMovement(3), restored.AdvanceMovement(3));
        Assert.Throws<NotSupportedException>(() => NativeEventRandomState.FromColdProcess().AdvanceMovement(0));
        foreach (string property in new[] { "State", "FieldCallbackFlagsCF10", "DrawPoints" })
        {
            JsonNode missing = JsonNode.Parse(JsonSerializer.Serialize(live))!;
            missing.AsObject().Remove(property);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeEventRandomState>(missing.ToJsonString()));
        }
    }

    [Fact]
    public void CanonicalMemoryAliasRefusesBeforeAtomicOwnersChange()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.State.Variables[116] = 99;
        GameState before = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), eventRandom: session.EventRandom!.Draw(out _)));
        Assert.Same(before, session.State);
        Assert.True(session.WorldControls!.SalarySuppressed);
        Assert.Equal(1u, session.EventRandom!.State);
        session.State.Variables.Remove(116);
        session.State.Variables[1000] = 99;
        session.SetNativeEventOwners(session.WorldControls.WithSalaryEnabled(true), eventRandom: session.EventRandom.Draw(out _));
        Assert.False(session.WorldControls!.SalarySuppressed);
        Assert.Equal(0x41C67EA6u, session.EventRandom!.State);
        GameState initialized = session.State;
        Assert.Throws<InvalidDataException>(() => session.SetEventRandom(session.EventRandom with { DrawPoints = null }));
        Assert.Same(initialized, session.State);
        Assert.Throws<InvalidDataException>(() => session.SetNativeEventOwners(session.WorldControls.WithSalaryEnabled(false), eventRandom: session.EventRandom with { DrawPoints = null }));
        Assert.Same(initialized, session.State);
    }
}
