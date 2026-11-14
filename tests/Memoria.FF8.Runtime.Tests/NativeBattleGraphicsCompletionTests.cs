namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsCompletionTests
{
    [Fact]
    public void Task112PublishesDescriptorCompletionAndReturnsTheNativeUnlinkBit()
    {
        byte completion = 0;

        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, new BattleActorModels([]), 0, ref completion));
        Assert.Equal(255, completion);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Task112WaitsForTheExactByteOwnersAndPreservesItsDescriptor(byte effects, byte camera)
    {
        byte completion = 7;

        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(effects, effects != 0 ? null : new BattleActorModels([]), camera, ref completion));
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
        BattleActorModels models = Models(new(flags) { Kind5 = kind, Flags8 = state, ResourceFlags44 = resource });
        byte completion = 0;

        Assert.Equal(expected, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        Assert.Equal(expected == 2 ? 255 : 0, completion);
    }

    [Fact]
    public void UnknownResourceFlagsFailOnlyWhenTheNativePredicateReadsThem()
    {
        BattleActorModels models = Models(new(2) { Kind5 = 0, Flags8 = 0 });
        byte completion = 0;

        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
        models = Models(new(2) { Kind5 = 0, Flags8 = 8 });
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models, 0, ref completion));
    }

    [Fact]
    public void Task112WaitsForAnExtraOwnerBeforeObservingCamera()
    {
        BattlePartyActorModel party = new(new("party"));
        BattleEnemyActorModel extra = new(new("extra-enemy"));
        BattleEnemyActorModel later = new(new("later-enemy"));
        BattleActorModels models = new([new(party, new(0)), new(extra, new(2) { Kind5 = 1, Flags8 = 0 }), new(later, new(2))]);
        int cameraReads = 0;
        byte completion = 7;

        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleGraphicsCompletion.PollTask112(-1, null, ReadCamera, ref completion));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(256, null, ReadCamera, ref completion));
        Assert.Equal(0, NativeBattleGraphicsCompletion.PollTask112(0, models, ReadCamera, ref completion));
        Assert.Equal(0, cameraReads);
        Assert.Equal(7, completion);

        models = models.Set(extra, new(2) { Kind5 = 0, Flags8 = 0, ResourceFlags44 = 0x40 });
        Assert.Throws<InvalidDataException>(() => NativeBattleGraphicsCompletion.PollTask112(0, models, ReadCamera, ref completion));
        Assert.Equal(0, cameraReads);
        models = models.Set(later, new(0));
        Assert.Equal(2, NativeBattleGraphicsCompletion.PollTask112(0, models, ReadCamera, ref completion));
        Assert.Equal(1, cameraReads);
        Assert.Equal(255, completion);

        byte ReadCamera()
        {
            cameraReads++;
            return 0;
        }
    }

    private static BattleActorModels Models(NativeBattleGraphicsModel model)
    {
        BattleEnemyActorModel owner = new(new("enemy"));
        return new([new(owner, model)]);
    }
}
