using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactEffectTests
{
    [Fact]
    public void PetrifyDefersItsVisualFlagUntilTheHitSequenceCanFinish()
    {
        NativeBattleContactHalf half = new(3, 5, 0, 0, 4, 99, 0);
        NativeBattleGraphicsModel model = Model();
        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, model, Rules(), 0);
        Assert.True(plan.DeferPetrify);
        Assert.Equal(0u, plan.ImmediateStatusFlags);
        NativeBattleEffectsState effects = NativeBattleContactEffects.ProduceTasks(NativeBattleEffectsState.Clear(), half, plan);
        Assert.Equal(1, effects.Head);
        Assert.Equal(0, effects.Tail);
        Assert.Equal(NativeBattleEffectKind.StatusReset506860, effects.Slots[0].Kind);
        Assert.Equal(NativeBattleEffectKind.DamageText5069B0, effects.Slots[1].Kind);
    }

    [Theory]
    [InlineData(16u, 1)]
    [InlineData(0u, 64)]
    [InlineData(0u, 256)]
    public void ExistingStatusOrCompletedResourceMakesPetrifyImmediate(uint flags, ushort resource)
    {
        NativeBattleContactHalf half = new(3, 5, 0, 0, 4, 99, 0);
        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, Model() with { Flags8 = flags, ResourceFlags44 = resource }, Rules(), 0);
        Assert.False(plan.DeferPetrify);
        Assert.Equal(16u, plan.ImmediateStatusFlags);
    }

    [Fact]
    public void CurrentCoreDefendAndChargedReplaceTheOlderPacketBits()
    {
        NativeBattleContactHalf half = new(3, 5, 0, 0, 0, 1, 0x880000);
        Assert.Equal(0u, NativeBattleContactEffects.Plan(half, Model(), Rules(), 0).ImmediateStatusFlags);
        Assert.Equal(0x200004u, NativeBattleContactEffects.Plan(half with { BattleStatuses8 = 0 }, Model(), Rules(), 0x880000).ImmediateStatusFlags);
    }

    [Fact]
    public void UnloadedRowsSkipTaskProducersAndEjectionSkipsOrdinarySequenceSelection()
    {
        NativeBattleContactHalf half = new(3, 5, 0x40, 0, 0, 1, 0);
        NativeBattleContactEffectPlan eject = NativeBattleContactEffects.Plan(half, Model(), Rules(), 0);
        Assert.True(eject.EjectionService7);
        Assert.False(eject.SelectSequence);
        NativeBattleContactEffectPlan unloaded = NativeBattleContactEffects.Plan(half, Model() with { Flags0 = 0, Flags8 = null, ResourceFlags44 = null }, Rules(), 0);
        NativeBattleEffectsState clear = NativeBattleEffectsState.Clear();
        Assert.Same(clear, NativeBattleContactEffects.ProduceTasks(clear, half, unloaded));
    }

    [Fact]
    public void SecondaryHalfUsesDirectProjectionWithoutPrimaryGuardDeferralOrRawCoreReplacement()
    {
        NativeBattleContactHalf half = new(3, 5, 0x40, 0, 4, 1, 0x890000);
        NativeBattleContactEffectPlan secondary = NativeBattleContactEffects.Plan(half, Model() with { Flags0 = 0, ModelId4 = null, Flags8 = null, ResourceFlags44 = null }, Rules(), 0, secondary: true);
        Assert.True(secondary.Loaded);
        Assert.True(secondary.Secondary);
        Assert.False(secondary.DeferPetrify);
        Assert.False(secondary.StageTargetRefresh);
        Assert.False(secondary.EjectionService7);
        Assert.True(secondary.SelectSequence);
        Assert.Equal(0x200014u, secondary.ImmediateStatusFlags);
    }

    private static NativeBattleGraphicsModel Model()
    {
        return new(3, 3) { ModelId4 = 61, Flags8 = 0, ResourceFlags44 = 1 };
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
