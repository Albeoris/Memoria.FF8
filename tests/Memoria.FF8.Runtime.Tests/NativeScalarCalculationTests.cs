using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeScalarCalculationTests
{
    [Theory]
    [InlineData(6, 0)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    public void UnanimousTimerComparisonsDoNotChooseAPhase(int kind, int expected)
    {
        NativeScalarValue value = Value(59, 60);
        NativeScalarValue result = NativeScalarCalculation.Resolve(kind, value, NativeScalarValue.Exact(10));
        Assert.Equal(expected, result.RequireExact());
        Assert.Null(result.Origin);
        Assert.Equal(new[] { 59, 60 }, value.Values);
    }

    [Fact]
    public void MixedOutcomesAndUnprovedSameReadCorrelationRefuse()
    {
        NativeScalarValue value = Value(9, 10);
        Assert.Throws<NotSupportedException>(() => NativeScalarCalculation.Resolve(9, value, NativeScalarValue.Exact(10)));
        Assert.Throws<NotSupportedException>(() => NativeScalarCalculation.Resolve(6, value, value));
        Assert.Throws<NotSupportedException>(() => NativeScalarCalculation.Resolve(0, value, NativeScalarValue.Exact(1)));
        Assert.Equal(new[] { 9, 10 }, value.Values);
    }

    [Fact]
    public void SignedDwordWidthsBitwiseOrAndNativeShiftCountsArePreserved()
    {
        Assert.Equal(1, NativeScalarCalculation.Resolve(9, NativeScalarValue.Exact(-1), NativeScalarValue.Exact(0)).RequireExact());
        Assert.Equal(0, NativeScalarCalculation.Resolve(7, NativeScalarValue.Exact(int.MinValue), NativeScalarValue.Exact(int.MaxValue)).RequireExact());
        Assert.Equal(int.MinValue, NativeScalarCalculation.Resolve(0, NativeScalarValue.Exact(int.MaxValue), NativeScalarValue.Exact(1)).RequireExact());
        Assert.Equal(int.MinValue, NativeScalarCalculation.Resolve(5, null, NativeScalarValue.Exact(int.MinValue)).RequireExact());
        Assert.Equal(-1, NativeScalarCalculation.Resolve(13, Value(59, 60), NativeScalarValue.Exact(-1)).RequireExact());
        Assert.Equal(0, NativeScalarCalculation.Resolve(12, Value(59, 60), NativeScalarValue.Exact(0)).RequireExact());
        Assert.Equal(-1, NativeScalarCalculation.Resolve(16, NativeScalarValue.Exact(int.MinValue), NativeScalarValue.Exact(63)).RequireExact());
        Assert.Equal(3, NativeScalarCalculation.Resolve(17, NativeScalarValue.Exact(3), NativeScalarValue.Exact(32)).RequireExact());
        Assert.Throws<DivideByZeroException>(() => NativeScalarCalculation.Resolve(3, NativeScalarValue.Exact(1), NativeScalarValue.Exact(0)));
        Assert.Throws<OverflowException>(() => NativeScalarCalculation.Resolve(4, NativeScalarValue.Exact(int.MinValue), NativeScalarValue.Exact(-1)));
    }

    private static NativeScalarValue Value(int first, int second)
    {
        NativeExamTimerState timer = NativeExamTimerState.FromReachedSet(first) with { Alternatives = [new NativeTimerAlternative(0, 0, first), new NativeTimerAlternative(1, 1, second)] };
        return NativeScalarValue.FromTimer(new NativeTimerReadOrigin(1, 999, new string('A', 64), 0, 0, 1, 2, timer));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void BothIdDivConsumersRequireARepresentableSignedQuotient(int kind)
    {
        Assert.Throws<OverflowException>(() => NativeScalarCalculation.Resolve(kind, NativeScalarValue.Exact(int.MinValue), NativeScalarValue.Exact(-1)));
        Assert.Throws<DivideByZeroException>(() => NativeScalarCalculation.Resolve(kind, NativeScalarValue.Exact(1), NativeScalarValue.Exact(0)));
        Assert.Equal(kind == 3 ? -2 : -1, NativeScalarCalculation.Resolve(kind, NativeScalarValue.Exact(-7), NativeScalarValue.Exact(3)).RequireExact());
    }
}
