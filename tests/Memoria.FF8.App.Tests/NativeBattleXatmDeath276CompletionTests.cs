using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleXatmDeath276CompletionTests
{
    [Fact]
    public void Completed_native_root_retains_only_quiescent_event_rows_without_mutating_their_saved_state()
    {
        var (pools, tables) = Fixture();
        var saved = NativeBattleStorageSnapshots.Capture(pools);
        string before = JsonSerializer.Serialize(saved);
        Assert.True(NativeBattleXatmDeath276CompletionAssertions.PoolsReleased(saved, tables));
        Assert.Equal(before, JsonSerializer.Serialize(saved));
        Assert.Equal(new uint[] { 144, 132, 106 }, pools[6].Slots.Where(task => task.Flags != 0).Select(task => task.ReadDword(12)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7)]
    public void Root_arc_or_director_work_still_prevents_completion(int index)
    {
        var (pools, tables) = Fixture();
        NativeBattleEffectTaskPool pool = pools[index];
        (pool, _) = pool.Append(0x6851F0);
        Assert.False(NativeBattleXatmDeath276CompletionAssertions.PoolsReleased(pools.SetItem(index, pool), tables));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(22)]
    [InlineData(25)]
    public void An_event_with_remaining_native_work_still_prevents_completion(uint tick)
    {
        var (pools, tables) = Fixture();
        NativeBattleEffectTaskPool pool = pools[6];
        pool = pool.WriteSlot(0, pool.Slots[0].WriteDword(12, tick));
        Assert.False(NativeBattleXatmDeath276CompletionAssertions.PoolsReleased(pools.SetItem(6, pool), tables));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Unrecognized_event_callback_resource_or_retirement_policy_is_not_quiescent(int change)
    {
        var (pools, tables) = Fixture();
        NativeBattleEffectTaskPool pool = pools[6];
        if (change == 0)
        {
            pool = pool.WriteSlot(0, pool.Slots[0].UpdateHeader(pool.Slots[0].Flags, pool.Slots[0].State, pool.Slots[0].Next, 0x686000));
        }
        else if (change == 1)
        {
            pool = pool.WriteSlot(0, pool.Slots[0].WriteDword(16, 0x12345678));
        }
        else if (change == 2)
        {
            byte[] bytes = tables[0].Data.ToArray();
            bytes[0x15] = 0;
            tables = tables.SetItem(0, tables[0] with { Data = bytes.ToImmutableArray() });
        }
        else
        {
            pool = NativeBattleEffectTaskPool.Initialize(0x12345678, 0x20006000, 20, 5);
        }
        Assert.False(NativeBattleXatmDeath276CompletionAssertions.PoolsReleased(pools.SetItem(6, pool), tables));
    }

    private static (ImmutableArray<NativeBattleEffectTaskPool> Pools, ImmutableArray<NativeBattleXatmDeath276Table> Tables) Fixture()
    {
        uint[] descriptors = [0x2508318, 0x25082A8, 0x25082B8, 0x25082C8, 0x25082D8, 0x25082E8, 0x25082F8, 0x2508308];
        ImmutableArray<NativeBattleEffectTaskPool> pools = descriptors.Select((descriptor, index) => NativeBattleEffectTaskPool.Initialize(descriptor, 0x20000000u + (uint)(index * 0x1000), 20, checked((ushort)(index == 6 ? 5 : 1)))).ToImmutableArray();
        uint[] addresses = [0x10D56A8, 0x10D55A8, 0x10D5528];
        uint[] ticks = [144, 132, 106];
        var tables = ImmutableArray.CreateBuilder<NativeBattleXatmDeath276Table>();
        NativeBattleEffectTaskPool events = pools[6];
        for (int index = 0; index < addresses.Length; index++)
        {
            byte[] data = new byte[0x80];
            data[0] = 15;
            data[2] = 25;
            data[3] = 22;
            data[8] = 20;
            data[0x15] = 2;
            tables.Add(new(addresses[index], data.ToImmutableArray(), 0, []));
            (events, int slot) = events.Append(0x685CF0);
            events = events.WriteSlot(slot, events.Slots[slot].WriteDword(12, ticks[index]).WriteDword(16, addresses[index]));
        }
        return (pools.SetItem(6, events), tables.ToImmutable());
    }
}
