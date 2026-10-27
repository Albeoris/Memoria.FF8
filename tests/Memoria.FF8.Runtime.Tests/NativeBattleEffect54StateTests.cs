using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect54Resources;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect54StateTests
{
    [Fact]
    public void Cold_factory_owns_only_seven_scalar_ranges_and_four_exclusive_banks()
    {
        NativeBattleEffect54State state = Assert.IsType<NativeBattleEffect54State>(NativeBattleEffectMemory.FromColdProcess().Effect54);
        state.Validate();
        Assert.True(state.ResourceSourcePending);
        Assert.Equal(7, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(new byte[Pools[index].Stride * Pools[index].Capacity], state.ReadBank(index).Read(0, Pools[index].Stride * Pools[index].Capacity));
            Assert.Throws<InvalidDataException>(() => state.Read(Pools[index].Bank, 4));
            Assert.Throws<InvalidDataException>(() => state.Read(Pools[index].Descriptor, 4));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(BaseAddress + 10, 1));
    }

    [Fact]
    public void Cold_bind_imports_only_69_DWORD_cells_and_warm_JSON_keeps_current_values()
    {
        NativeBattleEffect54Resources resources = Sources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2000000, [9, 8, 7]);
        (NativeBattleEffect54State state, NativeBattleFileStaging bound) = NativeBattleEffect54State.FromColdProcess().BindSources(staging, resources);
        Assert.False(state.ResourceSourcePending);
        Assert.Equal(69, MutableDwordAddresses.Length);
        foreach (uint address in MutableDwordAddresses)
        {
            Assert.Equal(resources.OriginalMutableDword(address), bound.Read(address, 4));
        }
        Assert.All(bound.ReadSnapshot(SourceAddress + 12, 4).Known, known => Assert.False(known));
        bound = bound.Write(SourceAddress, BitConverter.GetBytes(1u));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect54State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        bound = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(bound)))!);
        (state, bound) = state.BindSources(bound, resources);
        Assert.Equal(1u, BitConverter.ToUInt32(resources.ReadResource(bound, SourceAddress, 4)));
        Assert.Equal(new byte[] { 9, 8, 7 }, bound.Read(0x2000000, 3));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SourceAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(bound, SourceAddress + 1));
    }

    [Fact]
    public void Unknown_origin_conflicting_cold_or_missing_warm_owner_cannot_refill()
    {
        NativeBattleEffect54Resources resources = Sources();
        NativeBattleFileStaging unknown = NativeBattleFileStaging.Unknown();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect54State.FromUnknown().BindSources(unknown, resources));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect54State.FromColdProcess().BindSources(unknown.Write(SourceAddress, [0]), resources));
        Assert.Throws<InvalidDataException>(() => (NativeBattleEffect54State.FromColdProcess() with { ResourceSourcePending = false }).BindSources(unknown, resources));
        Assert.Throws<NotSupportedException>(() => resources.ReadResource(unknown, SourceAddress, 4));
        Assert.Throws<InvalidDataException>(() => resources.OriginalMutableDword(SourceAddress + 1));
    }

    [Fact]
    public void Bank_knownness_and_publication_preserve_exclusive_physical_ownership()
    {
        NativeBattleEffect54State state = NativeBattleEffect54State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(0).Write(14, [1]);
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect54State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!).AttachBank(0, root);
        Assert.Equal(new byte[] { 1 }, state.ReadBank(0).Read(14, 1));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(15, 1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, root));
        state.Validate();
        NativeBattleEffect54Resources resources = Sources();
        (_, NativeBattleFileStaging staging) = NativeBattleEffect54State.FromColdProcess().BindSources(NativeBattleFileStaging.Unknown(), resources);
        byte[] bank = NativeBattleEffectNodeBankFixups.Relocate(resources.ReadBank(staging), SourceAddress);
        BitConverter.GetBytes(1u).CopyTo(bank, 0);
        staging = resources.PublishBank(staging, bank.ToImmutableArray());
        Assert.Equal(1u, BitConverter.ToUInt32(staging.Read(SourceAddress, 4)));
        bank[12] = 99;
        Assert.Throws<InvalidDataException>(() => resources.PublishBank(staging, bank.ToImmutableArray()));
    }

    private static NativeBattleEffect54Resources Sources()
    {
        byte[] bytes = new byte[SourceLength];
        for (int index = 0; index < MutableDwordAddresses.Length; index++)
        {
            BitConverter.GetBytes((uint)(index + 10)).CopyTo(bytes, checked((int)(MutableDwordAddresses[index] - SourceAddress)));
        }
        BitConverter.GetBytes(0u).CopyTo(bytes, 0);
        for (int index = 0; index < 2; index++)
        {
            BitConverter.GetBytes(DescriptorAddresses[index] - SourceAddress).CopyTo(bytes, 4 + index * 4);
            BitConverter.GetBytes(SpriteAddresses[index] - SourceAddress).CopyTo(bytes, 0x44 + index * 4);
        }
        return new(null!, null!, new("test-source", bytes.ToImmutableArray()), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
    }
}
