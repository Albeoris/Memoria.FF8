using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect210StateTests
{
    [Fact]
    public void Cold_factory_retains_exact_sparse_globals_and_exclusive_pool_owners()
    {
        NativeBattleEffect210State state = Assert.IsType<NativeBattleEffect210State>(NativeBattleEffectMemory.FromColdProcess().Effect210);
        state.Validate();
        Assert.Equal(6, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Single(state.FixedBanks);
        Assert.Equal(6, state.CommonQueueStorage.Count);
        Assert.Throws<InvalidDataException>(() => state.Read(ActDescriptor + 8, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ViewAddress + 32, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Partial_view_writes_preserve_unknown_padding_and_retained_source_values()
    {
        NativeBattleEffect210State state = NativeBattleEffect210State.FromUnknown().WriteWord(ViewAddress, 4096).WriteDword(ViewAddress + 20, 512).WriteDword(FrozenGlobal, 7);
        state = JsonSerializer.Deserialize<NativeBattleEffect210State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal((ushort)4096, state.ReadWord(ViewAddress));
        Assert.Equal(512u, state.ReadDword(ViewAddress + 20));
        Assert.Equal(7u, state.ReadDword(FrozenGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ViewAddress + 2));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RetainedPairAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(CenterAddress + 6));
    }

    [Fact]
    public void Each_COMMON_queue_transfers_exclusively_and_preserves_retained_orphan_links()
    {
        NativeBattleEffect210State state = NativeBattleEffect210State.FromUnknown();
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            NativeBattleEffectArena queue = state.ReadCommonQueue(shape.Descriptor).Write(0, BitConverter.GetBytes(0xCAFEBABEu));
            state = state.DetachCommonQueue(shape.Descriptor);
            Assert.Throws<InvalidDataException>(() => state.ReadDword(shape.Descriptor));
            state = state.AttachCommonQueue(shape.Descriptor, queue);
            Assert.Throws<InvalidDataException>(() => state.AttachCommonQueue(shape.Descriptor, queue));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect210State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        foreach (NativeBattleCureFamilyPool shape in CommonPools)
        {
            Assert.Equal(0xCAFEBABEu, state.ReadDword(shape.Descriptor));
            Assert.Throws<NotSupportedException>(() => state.ReadDword(shape.Descriptor + 4));
        }
    }

    [Fact]
    public void Retained_pair_and_root_bank_preserve_warm_bytes_without_refill()
    {
        NativeBattleEffect210State state = NativeBattleEffect210State.FromUnknown().WriteWord(RetainedPairAddress + 6, 0xBEEF);
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        state = JsonSerializer.Deserialize<NativeBattleEffect210State>(JsonSerializer.Serialize(state))!;
        Assert.Equal((ushort)0xBEEF, state.ReadWord(RetainedPairAddress + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RetainedPairAddress));
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(0).Read(14, 2));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleEffectMemory.FromColdProcess() with { Effect210 = null }))!.Effect210);
    }
}
