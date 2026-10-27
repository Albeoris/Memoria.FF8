using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskPoolTests
{
    [Fact]
    public void OdinAndGilgameshInitializeTheirOwnProvedPhysicalBanks()
    {
        NativeBattleGuardianEffectPools odin = NativeBattleGuardianEffectPools.Odin();
        NativeBattleGuardianEffectPools gilgamesh = NativeBattleGuardianEffectPools.Gilgamesh();
        Assert.Equal(0x24FD6C0u, odin.Main.DescriptorAddress);
        Assert.Equal(0x64DD50u, odin.Main.Slots[0].Callback);
        Assert.Equal(0x6474C0u, odin.Children.Slots[0].Callback);
        Assert.Null(odin.Particles);
        Assert.Equal(2, odin.Main.Slots.Length);
        Assert.Equal((ushort)16, odin.Main.Stride);
        Assert.Equal(100, odin.Children.Slots.Length);
        Assert.Equal((ushort)36, odin.Children.Stride);
        Assert.Equal(0x21FF4A8u, gilgamesh.Main.DescriptorAddress);
        Assert.Equal(0x596B70u, gilgamesh.Main.Slots[0].Callback);
        Assert.Equal(0x592300u, gilgamesh.Children.Slots[0].Callback);
        Assert.Equal(4, gilgamesh.Particles!.Slots.Length);
        Assert.Null(gilgamesh.Particles.Head);
        Assert.All(odin.Children.Slots, slot => Assert.All(NativeBattleStorageSnapshots.Capture(slot).Payload, value => Assert.Equal((byte)0, value)));
    }

    [Fact]
    public void CallbackReturnBit2ReleasesOnlyFlagsWordAndReusesRetainedSuffix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleGuardianEffectPools.Odin().Children;
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 93).UpdateHeader(pool.Slots[0].WriteWord(12, 93).Flags, 7, pool.Slots[0].WriteWord(12, 93).Next, pool.Slots[0].WriteWord(12, 93).Callback));
        NativeBattleEffectTaskStep step = pool.Advance((state, _) => new(state, 6));
        Assert.Equal(0, step.Survivors);
        Assert.Null(step.Pool.Head);
        Assert.Null(step.Pool.Tail);
        Assert.Equal((ushort)0, step.Pool.Slots[0].Flags);
        Assert.Equal((ushort)7, step.Pool.Slots[0].State);
        Assert.Equal((ushort)93, step.Pool.Slots[0].ReadWord(12));
        (pool, int index) = step.Pool.Append(0x647900);
        Assert.Equal(0, index);
        Assert.Equal((ushort)0, pool.Slots[0].State);
        Assert.Equal((ushort)93, pool.Slots[0].ReadWord(12));
        Assert.Equal(0x647900u, pool.Slots[0].Callback);
    }

    [Fact]
    public void ChildAppendedByCallbackIsTraversedInThatSameOriginalPass()
    {
        NativeBattleEffectTaskPool pool = NativeBattleGuardianEffectPools.Odin().Main;
        List<int> order = [];
        NativeBattleEffectTaskStep step = pool.Advance((state, index) =>
        {
            order.Add(index);
            if (index == 0)
            {
                (state, _) = state.Append(0x6474C0);
            }
            return new(state, index == 0 ? 0 : 2);
        });
        Assert.Equal(new[] { 0, 1 }, order);
        Assert.Equal(1, step.Survivors);
        Assert.Equal(0, step.Pool.Head);
        Assert.Equal(0, step.Pool.Tail);
        Assert.Null(step.Pool.Slots[0].Next);
    }

    [Fact]
    public void RemovingHeadAndMiddlePreservesTheActualSurvivingTail()
    {
        NativeBattleEffectTaskPool pool = NativeBattleGuardianEffectPools.Gilgamesh().Particles!;
        for (int index = 0; index < 4; index++)
        {
            (pool, _) = pool.Append(0x592300);
        }
        NativeBattleEffectTaskStep step = pool.Advance((state, index) => new(state, index is 0 or 2 ? 2 : 0));
        Assert.Equal(2, step.Survivors);
        Assert.Equal(1, step.Pool.Head);
        Assert.Equal(3, step.Pool.Tail);
        Assert.Equal(3, step.Pool.Slots[1].Next);
        Assert.Null(step.Pool.Slots[3].Next);
    }

    [Fact]
    public void FullOriginalBankCannotGrowIntoAnotherModuleArena()
    {
        NativeBattleEffectTaskPool pool = NativeBattleGuardianEffectPools.Odin().Main;
        (pool, _) = pool.Append(0x6474C0);
        Assert.Throws<InvalidOperationException>(() => pool.Append(0x647900));
        Assert.Equal(2, pool.Slots.Length);
    }

    [Fact]
    public void IndependentPoolsAndRetainedCallbacksRoundTripWithoutAParallelTimer()
    {
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        pools = pools with { Children = pools.Children.WriteSlot(0, pools.Children.Slots[0].WriteWord(12, 42)) };
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(pools));
        NativeBattleGuardianEffectPools restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGuardianEffectPools>(json)!);
        restored.Main.Validate();
        restored.Children.Validate();
        restored.Particles!.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal((ushort)42, restored.Children.Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, restored.Main.Slots[0].ReadWord(12));
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTaskPool>("{}")));
    }

    [Fact]
    public void InvalidPhysicalLinksAndDescriptorReplacementAreRejected()
    {
        NativeBattleEffectTaskPool pool = NativeBattleGuardianEffectPools.Odin().Main;
        Assert.Throws<InvalidDataException>(() => NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(pool)).UpdateLinks(pool.Head, 1).Validate());
        Assert.Throws<InvalidDataException>(() => (pool.WriteSlot(0, pool.Slots[0].UpdateHeader(pool.Slots[0].Flags, pool.Slots[0].State, 0, pool.Slots[0].Callback))).Validate());
        Assert.Throws<InvalidOperationException>(() => _ = pool with { BankAddress = 4 });
    }
}
