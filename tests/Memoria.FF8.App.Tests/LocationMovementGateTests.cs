using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LocationMovementGateTests
{
    [Fact]
    public void ArrivalSuppressesHeldKeyboardAndAnalogMovementButPreservesUi()
    {
        LocationMovementGate gate = new();
        gate.Observe("arrival", TimeSpan.FromSeconds(10), false);
        InputFrame input = new(0.7f, -1, true, false, false, true, SecondaryVertical: 0.8f);
        InputFrame filtered = gate.Filter(input);
        Assert.Equal(0, filtered.Horizontal);
        Assert.Equal(0, filtered.Vertical);
        Assert.Equal(0, filtered.SecondaryVertical);
        Assert.True(filtered.Confirm);
        Assert.True(filtered.Save);
        Assert.Equal((ushort)0x40, gate.FilterButtons(0xF040));
        Advance(gate, "arrival", false, 4);
        Assert.Equal(input, gate.Filter(input));
        Assert.Equal((ushort)0xF040, gate.FilterButtons(0xF040));
    }

    [Fact]
    public void FadeMustFinishBeforeReactionWindowStarts()
    {
        LocationMovementGate gate = new();
        gate.Observe("arrival", TimeSpan.Zero, true);
        Advance(gate, "arrival", true, 8);
        Advance(gate, "arrival", false, 3);
        Assert.True(gate.Blocked);
        Advance(gate, "arrival", false, 1);
        Assert.False(gate.Blocked);
        gate.Observe("arrival", TimeSpan.Zero, true);
        Assert.False(gate.Blocked);
    }

    [Fact]
    public void StaticOrRestoredFadeCannotLockPlayerIndefinitely()
    {
        LocationMovementGate gate = new();
        gate.Observe("restored", TimeSpan.Zero, true);
        Advance(gate, "restored", true, 21);
        Assert.False(gate.Blocked);
    }

    [Fact]
    public void NewLocationBlocksAgainEvenWithinSameHostFrame()
    {
        LocationMovementGate gate = new();
        gate.Observe((1, false), TimeSpan.Zero, false);
        Advance(gate, (1, false), false, 4);
        Assert.False(gate.Blocked);
        gate.Observe((2, false), TimeSpan.Zero, false);
        Assert.True(gate.Blocked);
        Advance(gate, (2, false), false, 4);
        gate.Observe((2, true), TimeSpan.Zero, false);
        Assert.True(gate.Blocked);
    }

    private static void Advance(LocationMovementGate gate, object location, bool fading, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            gate.Observe(location, TimeSpan.FromSeconds(0.1), fading);
        }
    }
}
