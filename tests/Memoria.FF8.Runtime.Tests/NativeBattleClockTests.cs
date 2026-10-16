using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleClockTests
{
    [Theory]
    [InlineData(1, true, false, false, 0, 1)]
    [InlineData(0, true, false, false, 0, 0)]
    [InlineData(1, false, false, false, 0, 0)]
    [InlineData(1, true, true, false, 0, 0)]
    [InlineData(1, true, false, true, 0, 0)]
    [InlineData(1, true, false, false, 1, 0)]
    public void NativeAdmissionResetsDebAndRequiresEveryOriginalGate(byte active, bool uiAllowsAtb, bool scriptWaitHeld, bool allocationPaused, uint actionPause, byte expected)
    {
        NativeBattleClockState before = new(active, 1, actionPause);
        NativeBattleClockState after = NativeBattleClockRules.Admit(before, uiAllowsAtb, scriptWaitHeld, allocationPaused);
        Assert.Equal(expected, after.AtbAdmitted);
        Assert.Equal(before.Active, after.Active);
        Assert.Equal(before.ActionPause, after.ActionPause);
        Assert.Equal((byte)1, before.AtbAdmitted);
        Assert.Equal(after, JsonSerializer.Deserialize<NativeBattleClockState>(JsonSerializer.Serialize(after)));
    }

    [Fact]
    public void UnknownOrInvalidNativeClockFieldsCannotBeSeededByDeserialization()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleClockState>("{\"Active\":1,\"AtbAdmitted\":0}"));
        Assert.Throws<InvalidDataException>(() => NativeBattleClockRules.Admit(new(1, 0, 2), true, false, false));
    }
}
