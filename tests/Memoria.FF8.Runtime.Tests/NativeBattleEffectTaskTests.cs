namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskTests
{
    private static readonly BattleEnemyActorModel Target = new(new("target"));
    private static readonly BattleEnemyActorModel Other = new(new("other"));
    private static readonly BattleActorModels DefaultModels = new([new(Target, new(3) { Flags8 = 0, ResourceFlags44 = 1 }), new(Other, new(3) { Flags8 = 0, ResourceFlags44 = 1 })]);

    [Fact]
    public void DamageTextHasTenCallbacksAndItsPublishedCountLagsAllocation()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).PrependDamageText(Target, 0, 12345, DefaultModels.Roster);
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Null(state.Tail);
        Assert.Equal(new byte[] { 57, 58, 59, 60, 61, 0 }, state.Slots[0].Glyphs);
        for (int callback = 1; callback < 10; callback++)
        {
            state = state.Advance(DefaultModels, _ => new(160, 100)).State;
            Assert.Equal((byte)1, state.PublishedCount);
            Assert.Equal((byte)callback, state.Slots[0].Frame);
        }
        state = state.Advance(DefaultModels, _ => new(160, 100)).State;
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal((ushort)0, state.Slots[0].Flags0);
        Assert.Null(state.Slots[0].ModelOwner);
        Assert.Equal((byte)10, state.Slots[0].Frame);
        Assert.Equal(new byte[] { 57, 58, 59, 60, 61, 0 }, state.Slots[0].Glyphs);
    }

    [Theory]
    [InlineData(2u, null, true)]
    [InlineData(8u, null, true)]
    [InlineData(16u, null, true)]
    [InlineData(0u, 64, true)]
    [InlineData(0u, 256, true)]
    [InlineData(0u, 1, false)]
    public void StatusResetPreservesNativeFlagAndResourceShortCircuit(uint flags, int? resource, bool completes)
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).AppendStatusReset(Target, DefaultModels.Roster);
        BattleActorModels models = DefaultModels;
        models = models.Set(Target, models.Get(Target) with { Flags8 = flags, ResourceFlags44 = resource is int word ? checked((ushort)word) : null });
        NativeBattleEffectsStep step = state.Advance(models);
        Assert.Equal(completes ? new BattleModelOwner[] { Target } : [], step.ResetModelStatuses);
        Assert.Equal(completes ? (byte)0 : (byte)1, step.State.PublishedCount);
    }

    [Fact]
    public void EmptyListPrependThenAppendPreservesTheOriginalOrphanedPhysicalSlot()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).PrependDamageText(Target, 0, 1, DefaultModels.Roster).AppendStatusReset(Other, DefaultModels.Roster);
        Assert.Equal(1, state.Head);
        Assert.Equal(1, state.Tail);
        Assert.Equal((ushort)1, state.Slots[0].Flags0);
        state = state.Advance(DefaultModels, _ => new(160, 100)).State;
        Assert.Equal((byte)1, state.PublishedCount);
        Assert.Equal((byte)0, state.Slots[0].Frame);
        Assert.Equal((ushort)1, state.Slots[0].Flags0);
    }

    [Fact]
    public void FullBankSkipsDamageTextButRejectsRequiredStatusAllocation()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena);
        for (int task = 0; task < 20; task++)
        {
            state = state.AppendStatusReset(Target, DefaultModels.Roster);
        }
        Assert.Same(state, state.PrependDamageText(Target, 0, 1, DefaultModels.Roster));
        Assert.Throws<InvalidOperationException>(() => state.AppendStatusReset(Target, DefaultModels.Roster));
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Equal((byte)20, state.Advance(DefaultModels, _ => new(160, 100)).State.PublishedCount);
    }

    [Fact]
    public void PrependOrderSurvivorTailAndInProcessRestoreRetainCallbackFrames()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena).AppendStatusReset(Target, DefaultModels.Roster).PrependDamageText(Other, 0x100, 9, DefaultModels.Roster);
        Assert.Equal(1, state.Head);
        Assert.Equal(0, state.Tail);
        state = state.Advance(DefaultModels, _ => new(160, 100)).State;
        NativeBattleEffectsState restored = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(state));
        for (int frame = 0; frame < 12; frame++)
        {
            state = state.Advance(DefaultModels, _ => new(160, 100)).State;
            restored = restored.Advance(DefaultModels).State;
        }
        Assert.Equal(state.Slots.AsEnumerable(), restored.Slots.AsEnumerable());
        Assert.Equal(state.PublishedCount, restored.PublishedCount);
        Assert.Equal(0, state.Head);
        Assert.Equal(0, state.Tail);
        Assert.Equal((byte)1, state.PublishedCount);
        Assert.Equal(0x408040u, state.Slots[1].Color);
    }
}
