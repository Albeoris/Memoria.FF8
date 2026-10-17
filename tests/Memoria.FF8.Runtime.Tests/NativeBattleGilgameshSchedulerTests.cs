using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshSchedulerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Returned_task_waits_for_both_child_banks_and_increments_its_word(bool keepChild, bool keepParticle)
    {
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        (NativeBattleEffectTaskPool particles, _) = pools.Particles!.Append(0x592B00);
        pools = pools with { Particles = particles, Main = pools.Main with { Slots = pools.Main.Slots.SetItem(0, pools.Main.Slots[0].WriteWord(12, ushort.MaxValue)) } };
        List<uint> order = [];
        NativeBattleGuardianPoolStep step = NativeBattleGilgameshScheduler.Advance(pools, (state, descriptor, slot) =>
        {
            order.Add(descriptor);
            Assert.Equal(0, slot);
            Assert.Equal(ushort.MaxValue, state.Main.Slots[0].ReadWord(12));
            bool keep = descriptor == 0x21FFD58 ? keepChild : keepParticle;
            return new(state, keep ? 0 : 2);
        });
        Assert.Equal(new uint[] { 0x21FFD58, 0x21FF458 }, order);
        Assert.Equal((ushort)0, step.Pools.Main.Slots[0].ReadWord(12));
        Assert.Equal(keepChild || keepParticle ? 1 : 0, step.Survivors);
        Assert.Equal(keepChild || keepParticle ? (ushort)1 : (ushort)0, step.Pools.Main.Slots[0].Flags);
    }

    [Fact]
    public void Child_publication_reaches_the_particle_bank_during_the_same_callback()
    {
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        List<uint> order = [];
        NativeBattleGuardianPoolStep step = NativeBattleGilgameshScheduler.Advance(pools, (state, descriptor, _) =>
        {
            order.Add(descriptor);
            if (descriptor == 0x21FFD58)
            {
                (NativeBattleEffectTaskPool particles, _) = state.Particles!.Append(0x592B00);
                state = state with { Particles = particles };
            }
            return new(state, 2);
        });
        Assert.Equal(new uint[] { 0x21FFD58, 0x21FF458 }, order);
        Assert.Equal(0, step.Survivors);
        Assert.Null(step.Pools.Main.Head);
        Assert.Null(step.Pools.Children.Head);
        Assert.Null(step.Pools.Particles!.Head);
        int calls = 0;
        step = NativeBattleGilgameshScheduler.Advance(step.Pools, (state, _, _) =>
        {
            calls++;
            return new(state, 0);
        });
        Assert.Equal(0, calls);
        Assert.Equal((ushort)1, step.Pools.Main.Slots[0].ReadWord(12));
    }

    [Fact]
    public void Actual_live_pool_state_restores_without_reinitializing_a_child_or_counter()
    {
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        pools = NativeBattleGilgameshScheduler.Advance(pools, (state, _, _) => new(state, 0)).Pools;
        string json = JsonSerializer.Serialize(pools);
        NativeBattleGuardianEffectPools restored = JsonSerializer.Deserialize<NativeBattleGuardianEffectPools>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeBattleGuardianPoolStep first = NativeBattleGilgameshScheduler.Advance(pools, (state, _, _) => new(state, 2));
        NativeBattleGuardianPoolStep second = NativeBattleGilgameshScheduler.Advance(restored, (state, _, _) => new(state, 2));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal((ushort)2, second.Pools.Main.Slots[0].ReadWord(12));
        Assert.Equal(0, second.Survivors);
    }

    [Fact]
    public void Missing_or_replaced_owners_and_unimplemented_callbacks_fail_at_their_boundary()
    {
        NativeBattleGuardianEffectPools pools = NativeBattleGuardianEffectPools.Gilgamesh();
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshScheduler.Advance(pools with { Particles = null }, (state, _, _) => new(state, 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshScheduler.Advance(NativeBattleGuardianEffectPools.Odin(), (state, _, _) => new(state, 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshScheduler.Advance(pools, (state, _, _) => new(state with { Children = state.Children with { BankAddress = 4 } }, 0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshScheduler.Advance(pools, (_, _, _) => throw new NotSupportedException("Native592300 requires its implemented child controller.")));
        NativeBattleGuardianEffectPools unsupported = pools with { Main = pools.Main with { Slots = pools.Main.Slots.SetItem(0, pools.Main.Slots[0] with { Callback = 0x1234 }) } };
        int callbacks = 0;
        Assert.Throws<NotSupportedException>(() => NativeBattleGilgameshScheduler.Advance(unsupported, (state, _, _) =>
        {
            callbacks++;
            return new(state, 0);
        }));
        Assert.Equal(0, callbacks);
        Assert.Equal((ushort)0, pools.Main.Slots[0].ReadWord(12));
    }
}
