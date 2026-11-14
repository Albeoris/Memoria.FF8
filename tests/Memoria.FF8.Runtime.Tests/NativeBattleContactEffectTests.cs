using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactEffectTests
{
    private static readonly BattleEnemyActorModel Target = new(new("target"));

    [Fact]
    public void PetrifyDefersItsVisualFlagUntilTheHitSequenceCanFinish()
    {
        NativeBattleContactHalf half = new(Target, 5, 0, 0, 4, 99, 0);
        BattleActorModels models = Models();
        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, models, Rules(), 0);
        Assert.True(plan.DeferPetrify);
        Assert.Equal(0u, plan.ImmediateStatusFlags);
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena), half, plan, models.Roster);
        Assert.Equal(1, effects.Head);
        Assert.Equal(0, effects.Tail);
        Assert.Equal(NativeBattleEffectKind.StatusReset506860, effects.Slots[0].Kind);
        Assert.Equal(NativeBattleEffectKind.DamageText5069B0, effects.Slots[1].Kind);
        Assert.Equal(Target, effects.Slots[0].ModelOwner);
        Assert.Equal(Target, effects.Slots[1].ModelOwner);
    }

    [Theory]
    [InlineData(16u, 1)]
    [InlineData(0u, 64)]
    [InlineData(0u, 256)]
    public void ExistingStatusOrCompletedResourceMakesPetrifyImmediate(uint flags, ushort resource)
    {
        NativeBattleContactHalf half = new(Target, 5, 0, 0, 4, 99, 0);
        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, Models(Model() with { Flags8 = flags, ResourceFlags44 = resource }), Rules(), 0);
        Assert.False(plan.DeferPetrify);
        Assert.Equal(16u, plan.ImmediateStatusFlags);
    }

    [Fact]
    public void CurrentCoreDefendAndChargedReplaceTheOlderPacketBits()
    {
        NativeBattleContactHalf half = new(Target, 5, 0, 0, 0, 1, 0x880000);
        Assert.Equal(0u, NativeBattleContactEffects.Plan(half, Models(), Rules(), 0).ImmediateStatusFlags);
        Assert.Equal(0x200004u, NativeBattleContactEffects.Plan(half with { BattleStatuses8 = 0 }, Models(), Rules(), 0x880000).ImmediateStatusFlags);
    }

    [Fact]
    public void UnloadedRowsSkipTaskProducersAndEjectionSkipsOrdinarySequenceSelection()
    {
        NativeBattleContactHalf half = new(Target, 5, 0x40, 0, 0, 1, 0);
        NativeBattleContactEffectPlan eject = NativeBattleContactEffects.Plan(half, Models(), Rules(), 0);
        Assert.True(eject.EjectionService7);
        Assert.False(eject.SelectSequence);
        NativeBattleContactEffectPlan unloaded = NativeBattleContactEffects.Plan(half, Models(Model() with { Flags0 = 0, Flags8 = null, ResourceFlags44 = null }), Rules(), 0);
        NativeBattleEffectsState clear = NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena);
        Assert.Same(clear, NativeBattleContactEffects.ProduceTasks(clear, half, unloaded, Models().Roster));
    }

    [Fact]
    public void SecondaryHalfUsesDirectProjectionWithoutPrimaryGuardDeferralOrRawCoreReplacement()
    {
        NativeBattleContactHalf half = new(Target, 5, 0x40, 0, 4, 1, 0x890000);
        NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, Models(Model() with { Flags0 = 0, ModelId4 = null, Flags8 = null, ResourceFlags44 = null }), Rules(), 0, secondary: true);
        Assert.True(secondary.Loaded);
        Assert.True(secondary.Secondary);
        Assert.False(secondary.DeferPetrify);
        Assert.False(secondary.StageTargetRefresh);
        Assert.False(secondary.EjectionService7);
        Assert.True(secondary.SelectSequence);
        Assert.Equal(0x200014u, secondary.ImmediateStatusFlags);
    }

    [Fact]
    public void StageRefreshStillPrecedesThePrimaryLoadedGuard()
    {
        NativeBattleContactHalf half = new(Target, 0, 0, 0, 4, 0, 0);
        BattleActorModels models = Models(new NativeBattleGraphicsModel(0) { ModelId4 = 140 });

        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, models, Rules(), 0);

        Assert.True(plan.StageTargetRefresh);
        Assert.False(plan.Loaded);
        Assert.Null(plan.ImmediateStatusFlags);
    }

    [Fact]
    public void ExpandableTasksUseTheRecipientEvenWhenTheContactCarriesCover()
    {
        var extra = new BattleEnemyActorModel(new("expanded target"));
        var cover = new BattlePartyActorModel(new("cover"));
        BattleActorModels models = Models().Add(extra, Model()).Add(cover, new NativeBattleGraphicsModel(0));
        NativeBattleContactHalf half = new(extra, 22, 0, 8, 0, 123, 0) { CoverSource = cover };

        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, models, Rules(), 0);
        NativeBattleEffectsState state = NativeBattleContactEffects.ProduceTasks(NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena), half, plan, models.Roster);

        Assert.Equal((ushort)0x0800, plan.ContactFlags);
        Assert.Equal(extra, state.Slots[state.Head!.Value].ModelOwner);
        BattleModelOwner? observed = null;
        _ = state.Advance(models, owner =>
        {
            observed = owner;
            return new(160, 100);
        });
        Assert.Equal(extra, observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetiredRecipientCannotReadItsReplacementModel(bool secondary)
    {
        var replacement = new BattleEnemyActorModel(new("replacement"));
        BattleActorModels models = Models().Remove(Target).Add(replacement, new NativeBattleGraphicsModel(3));
        NativeBattleContactHalf half = new(Target, 0, 0, 0, 4, 0, 0);

        Assert.Throws<KeyNotFoundException>(() => NativeBattleContactEffects.Plan(half, models, Rules(), 0, secondary));
    }

    private static BattleActorModels Models(NativeBattleGraphicsModel? model = null)
    {
        return new([new(Target, model ?? Model())]);
    }

    private static NativeBattleGraphicsModel Model()
    {
        return new(3) { ModelId4 = 61, Flags8 = 0, ResourceFlags44 = 1 };
    }

    private static NativeBattleGraphicsRules Rules()
    {
        sbyte[] saved = Enumerable.Repeat((sbyte)-1, 16).ToArray();
        sbyte[] battle = Enumerable.Repeat((sbyte)-1, 32).ToArray();
        saved[2] = 4;
        battle[19] = 21;
        battle[23] = 2;
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved.ToImmutableArray(), battle.ToImmutableArray());
    }
}
