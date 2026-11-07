using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuake38HeightLookupTests
{
    [Fact]
    public void All_unsigned_byte_selectors_have_their_actual_initialized_physical_owner()
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        ImmutableArray<NativeBattleEffectTaskPool> pools = [Pool()];

        for (int zone = 0; zone <= byte.MaxValue; zone++)
        {
            Assert.Equal((short)0, NativeBattleQuake38HeightLookup.Read8D9C76(state, pools, (byte)zone));
        }
    }

    [Theory]
    [InlineData(0, 0x278C912u)]
    [InlineData(9, 0x278C9A2u)]
    [InlineData(10, 0x278C9B2u)]
    public void Header_selectors_read_the_current_signed_word_without_requiring_task_storage(byte zone, uint address)
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess().SetWord(address, -1234);

        Assert.Equal((short)-1234, NativeBattleQuake38HeightLookup.Read8D9C76(state, [], zone));
        state.SetWord(address, 2345);
        Assert.Equal((short)2345, NativeBattleQuake38HeightLookup.Read8D9C76(state, [], zone));
    }

    [Fact]
    public void First_debris_selector_reads_the_callback_header_word_in_the_live_bank()
    {
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        pool.Slots[0].WriteWord(42, 1234);
        NativeBattleEffectHierarchyAppend appended = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x008DA900, 176);
        NativeBattleEffectHierarchy hierarchy = appended.Hierarchy;

        Assert.Equal(0x278C9B8u, appended.Address);
        Assert.Equal((short)0x008D, NativeBattleQuake38HeightLookup.Read8D9C76(state, hierarchy.Pools, 11));
        Assert.Equal((short)0, NativeBattleQuake38HeightLookup.Read8D9C76(state, hierarchy.Pools, 13));
        NativeBattleModulePoolTask task = hierarchy.Read(appended.Address).UpdateHeader(1, 0, null, 0x12345678).WriteWord(42, 2345);
        hierarchy = hierarchy.Write(appended.Address, task);
        Assert.Equal((short)0x1234, NativeBattleQuake38HeightLookup.Read8D9C76(state, hierarchy.Pools, 11));
        Assert.Equal((short)2345, NativeBattleQuake38HeightLookup.Read8D9C76(state, hierarchy.Pools, 13));
    }

    [Fact]
    public void Selector_255_reads_slot22_word42_and_keeps_independent_cold_continuation()
    {
        NativeBattleEffectTaskPool pool = Pool();
        pool.Slots[22].WriteWord(42, unchecked((ushort)-12345));
        Assert.Equal(0, pool.Slots[22].Flags & 1);
        Owners owners = new(NativeBattleQuake38State.FromColdProcess().SetWord(0x278C9B2, 77), [pool]);
        Owners restored = NativeBattleTestSnapshots.RestoreJson<Owners>(NativeBattleTestSnapshots.Json(owners));

        Assert.Equal((short)-12345, NativeBattleQuake38HeightLookup.Read8D9C76(owners.State, owners.Pools, 255));
        Assert.Equal((short)-12345, NativeBattleQuake38HeightLookup.Read8D9C76(restored.State, restored.Pools, 255));
        Assert.Equal((short)77, NativeBattleQuake38HeightLookup.Read8D9C76(restored.State, restored.Pools, 10));
        restored.Pools[0].Slots[22].WriteWord(42, 23456);
        Assert.Equal((short)23456, NativeBattleQuake38HeightLookup.Read8D9C76(restored.State, restored.Pools, 255));
        Assert.Equal((short)-12345, NativeBattleQuake38HeightLookup.Read8D9C76(owners.State, owners.Pools, 255));
        owners.Pools[0].Slots[22].WriteWord(42, 23456);
        Assert.Equal(NativeBattleTestSnapshots.Json(owners), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Unknown_debris_bytes_remain_unsupported_instead_of_becoming_cold_zero()
    {
        NativeBattleEffectTaskPool pool = Pool();
        pool.Slots[22].WriteWord(42, 1234).MarkPayloadUnknown(43, 1);

        Assert.Throws<NotSupportedException>(() => NativeBattleQuake38HeightLookup.Read8D9C76(NativeBattleQuake38State.FromColdProcess(), [pool], 255));
    }

    [Fact]
    public void Missing_or_malformed_debris_owner_is_rejected()
    {
        NativeBattleQuake38State state = NativeBattleQuake38State.FromColdProcess();
        NativeBattleEffectTaskPool wrong = NativeBattleEffectTaskPool.Initialize(0x278E538, 0x278C9B8, 176, 39);

        Assert.Throws<NotSupportedException>(() => NativeBattleQuake38HeightLookup.Read8D9C76(state, [], 11));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuake38HeightLookup.Read8D9C76(state, [wrong], 255));
    }

    private static NativeBattleEffectTaskPool Pool() => NativeBattleEffectTaskPool.Initialize(0x278E538, 0x278C9B8, 176, 40);
    private sealed record Owners(NativeBattleQuake38State State, ImmutableArray<NativeBattleEffectTaskPool> Pools);
}
