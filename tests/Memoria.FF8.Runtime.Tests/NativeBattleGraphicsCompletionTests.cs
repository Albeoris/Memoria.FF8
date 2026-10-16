namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsCompletionTests
{
    [Fact]
    public void Task112PublishesDescriptorCompletionAndReturnsTheNativeUnlinkBit()
    {
        byte completion = 0;

        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, Models(), 0, ref completion));
        Assert.Equal(255, completion);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Task112WaitsForTheExactByteOwnersAndPreservesItsDescriptor(byte effects, byte camera)
    {
        byte completion = 7;

        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(effects, effects != 0 ? null : Models(), camera, ref completion));
        Assert.Equal(7, completion);
    }

    [Theory]
    [InlineData(2, 0, 0, 0, 0)]
    [InlineData(2, 1, 0, 64, 0)]
    [InlineData(0, 0, 0, 0, 2)]
    [InlineData(10, 0, 0, 0, 2)]
    [InlineData(2, 0, 8, 0, 2)]
    [InlineData(2, 0, 16, 0, 2)]
    [InlineData(2, 0, 0, 64, 2)]
    public void Task112UsesTheOriginalGraphicsModelPredicate(ushort flags, byte kind, uint state, ushort resource, int expected)
    {
        NativeBattleGraphicsModelCompletion[] models = Models();
        models[6] = new(flags, kind, state, resource);
        byte completion = 0;

        Assert.Equal(expected, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        Assert.Equal(expected == 2 ? 255 : 0, completion);
    }

    [Fact]
    public void UnknownResourceFlagsFailOnlyWhenTheNativePredicateReadsThem()
    {
        NativeBattleGraphicsModelCompletion[] models = Models();
        models[0] = new(2, 0, 0, null);
        byte completion = 0;

        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        models[0] = models[0] with { Flags8 = 8 };
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
    }

    private static NativeBattleGraphicsModelCompletion[] Models()
    {
        return Enumerable.Repeat(new NativeBattleGraphicsModelCompletion(0, 0, 0, null), 7).ToArray();
    }
}
