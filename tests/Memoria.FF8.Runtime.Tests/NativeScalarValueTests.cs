using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeScalarValueTests
{
    [Fact]
    public void ExactValuesKeepLegacyIntegerJsonAndUncertaintyKeepsItsSource()
    {
        Assert.Equal("-7", JsonSerializer.Serialize(NativeScalarValue.Exact(-7)));
        Assert.Equal(-7, JsonSerializer.Deserialize<NativeScalarValue>("-7")!.RequireExact());
        NativeScalarValue value = Value(599, 600);
        Assert.False(value.IsExact);
        Assert.Throws<NotSupportedException>(() => value.RequireExact());
        NativeScalarValue restored = JsonSerializer.Deserialize<NativeScalarValue>(JsonSerializer.Serialize(value))!;
        Assert.Equal(value.Values, restored.Values);
        Assert.Equal(value.Origin!.ProgramHash, restored.Origin!.ProgramHash);
        Assert.Equal(value.Origin.Timer.Alternatives, restored.Origin.Timer.Alternatives);
    }

    [Fact]
    public void NativeWidthProjectionCanBecomeExactWithoutChoosingATimerPhase()
    {
        NativeScalarValue value = Value(255, 511);
        Assert.Equal(255, value.ToWidth(1).RequireExact());
        Assert.Equal(new[] { 255, 511 }, value.ToWidth(2).Values);
        NativeScalarValue signed = Value(32767, 32768).ToWidth(2, true);
        Assert.Equal(new[] { -32768, 32767 }, signed.Values);
        Assert.Equal(signed.Values, signed.ToWidth(4).Values);
        Assert.Equal(new[] { 0, 255 }, signed.ToWidth(1).Values);
    }

    [Fact]
    public void InvalidProjectionProvenanceAndMemoryAliasesRefuse()
    {
        NativeScalarValue value = Value(599, 600);
        Assert.Throws<InvalidDataException>(() => (value with { Values = [599] }).Validate());
        Assert.Throws<InvalidDataException>(() => (value with { Origin = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (value with { Values = [600, 599] }).Validate());
        Dictionary<int, NativeMemoryValue> memory = new() { [432] = new(2, value.ToWidth(2)) };
        NativeMemoryValue.Validate(memory, new Dictionary<int, int>());
        Assert.Throws<InvalidDataException>(() => NativeMemoryValue.Validate(memory, new Dictionary<int, int> { [432] = 599 }));
        memory[433] = new(1, value.ToWidth(1));
        Assert.Throws<InvalidDataException>(() => NativeMemoryValue.Validate(memory, new Dictionary<int, int>()));
    }

    private static NativeScalarValue Value(int first, int second)
    {
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(first) with { Alternatives = [new NativeTimerAlternative(0, 0, first), new NativeTimerAlternative(1, 1, second)] };
        return NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 999, new string('A', 64), 0, 0, 1, 2, timer));
    }
}
