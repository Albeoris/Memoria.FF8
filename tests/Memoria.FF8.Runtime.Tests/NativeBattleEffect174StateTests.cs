using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect174StateTests
{
    [Fact]
    public void Cold_factory_initializes_exact_sparse_owners_and_both_inactive_banks()
    {
        NativeBattleEffect174State state = Assert.IsType<NativeBattleEffect174State>(NativeBattleEffectMemory.FromColdProcess().Effect174);
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(25, OwnedRanges.Length);
        Assert.Equal(832, SavedBonesLength);
        Assert.Throws<InvalidDataException>(() => state.Read(SavedBonesAddress + SavedBonesLength, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(HistoryAddress + HistoryLength, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(SavedActorsAddress + 2, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Marker_writes_preserve_unknown_bone_history_payload_and_saved_actor_padding()
    {
        NativeBattleEffect174State state = NativeBattleEffect174State.FromUnknown().WriteDword(FirstRecordsAddress, 0).WriteDword(HistoryAddress, 0).WriteWord(SavedActorsAddress, 0x1020).WriteDword(SavedActorsAddress + 4, 0x12345678);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect174State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(0u, state.ReadDword(FirstRecordsAddress));
        Assert.Equal(0u, state.ReadDword(HistoryAddress));
        Assert.Equal((ushort)0x1020, state.ReadWord(SavedActorsAddress));
        Assert.Equal(0x12345678u, state.ReadDword(SavedActorsAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(HistoryAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(SavedBonesAddress));
        Assert.Throws<InvalidDataException>(() => state.ReadWord(SavedActorsAddress + 2));
    }

    [Fact]
    public void Physical_bank_transfer_retains_warm_cleanup_WORD_and_unknown_suffix_after_JSON()
    {
        NativeBattleEffect174State state = NativeBattleEffect174State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect174State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(14, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(12, 2));
        }
    }

    [Fact]
    public void History_stride_does_not_admit_unwritten_192_byte_gaps_or_legacy_missing_owner()
    {
        NativeBattleEffect174State state = NativeBattleEffect174State.FromColdProcess();
        for (int index = 0; index < HistoryCount; index++)
        {
            uint row = HistoryAddress + (uint)(index * HistoryStride);
            Assert.Equal(836, state.Read(row, HistoryLength).Length);
            Assert.Throws<InvalidDataException>(() => state.Read(row + HistoryLength, HistoryStride - HistoryLength));
        }
        Assert.Null(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectMemory.FromColdProcess() with { Effect174 = null })))!).Effect174);
    }
}
