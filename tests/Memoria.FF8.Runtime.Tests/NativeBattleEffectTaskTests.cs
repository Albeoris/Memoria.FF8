using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskTests
{
    [Fact]
    public void DamageTextHasTenCallbacksAndItsPublishedCountLagsAllocation()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().PrependDamageText(3, 0, 12345);
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Null(state.Tail);
        Assert.Equal(new byte[] { 57, 58, 59, 60, 61, 0 }, state.Slots[0].Glyphs);
        for (int callback = 1; callback < 10; callback++)
        {
            state = state.Advance(Models(), _ => new(160, 100)).State;
            Assert.Equal((byte)1, state.PublishedCount);
            Assert.Equal((byte)callback, state.Slots[0].Frame);
        }
        state = state.Advance(Models(), _ => new(160, 100)).State;
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal((ushort)0, state.Slots[0].Flags0);
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
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().AppendStatusReset(3);
        NativeBattleGraphicsModel[] models = Models();
        models[3] = models[3] with { Flags8 = flags, ResourceFlags44 = resource is int word ? checked((ushort)word) : null };
        NativeBattleEffectsStep step = state.Advance(models);
        Assert.Equal(completes ? new byte[] { 3 } : [], step.ResetModelStatuses);
        Assert.Equal(completes ? (byte)0 : (byte)1, step.State.PublishedCount);
    }

    [Fact]
    public void EmptyListPrependThenAppendPreservesTheOriginalOrphanedPhysicalSlot()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().PrependDamageText(3, 0, 1).AppendStatusReset(4);
        Assert.Equal(1, state.Head);
        Assert.Equal(1, state.Tail);
        Assert.Equal((ushort)1, state.Slots[0].Flags0);
        state = state.Advance(Models(), _ => new(160, 100)).State;
        Assert.Equal((byte)1, state.PublishedCount);
        Assert.Equal((byte)0, state.Slots[0].Frame);
        Assert.Equal((ushort)1, state.Slots[0].Flags0);
    }

    [Fact]
    public void FullBankSkipsDamageTextButRejectsRequiredStatusAllocation()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear();
        for (int task = 0; task < 20; task++)
        {
            state = state.AppendStatusReset(3);
        }
        Assert.Same(state, state.PrependDamageText(3, 0, 1));
        Assert.Throws<InvalidOperationException>(() => state.AppendStatusReset(3));
        Assert.Equal((byte)0, state.PublishedCount);
        Assert.Equal((byte)20, state.Advance(Models(), _ => new(160, 100)).State.PublishedCount);
    }

    [Fact]
    public void PrependOrderSurvivorTailAndStandaloneRestoreRetainCallbackFrames()
    {
        NativeBattleEffectsState state = NativeBattleEffectsState.Clear().AppendStatusReset(3).PrependDamageText(4, 0x100, 9);
        Assert.Equal(1, state.Head);
        Assert.Equal(0, state.Tail);
        state = state.Advance(Models(), _ => new(160, 100)).State;
        NativeBattleEffectsState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectsState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        for (int frame = 0; frame < 12; frame++)
        {
            state = state.Advance(Models(), _ => new(160, 100)).State;
            restored = restored.Advance(Models()).State;
        }
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(0, state.Head);
        Assert.Equal(0, state.Tail);
        Assert.Equal((byte)1, state.PublishedCount);
        Assert.Equal(0x408040u, state.Slots[1].Color);
    }

    private static NativeBattleGraphicsModel[] Models()
    {
        return Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 3) { Flags8 = 0, ResourceFlags44 = 1 }).ToArray();
    }
}
