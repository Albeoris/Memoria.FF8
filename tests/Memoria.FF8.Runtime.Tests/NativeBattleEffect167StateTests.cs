using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect167StateTests
{
    [Fact]
    public void Cold_process_factory_initializes_exact_sparse_owners_and_both_inactive_banks()
    {
        NativeBattleEffect167State state = Assert.IsType<NativeBattleEffect167State>(NativeBattleEffectMemory.FromColdProcess().Effect167);
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(2, state.FixedBanks.Count);
        Assert.Throws<InvalidDataException>(() => state.Read(FirstControlAddress + 80, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(SecondControlAddress + 80, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ChildDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Marker_only_writes_preserve_unknown_history_payload_and_endpoint_W()
    {
        NativeBattleEffect167State state = NativeBattleEffect167State.FromUnknown().WriteDword(FirstRecordsAddress, 0).WriteDword(SecondHistoryAddress, 0).WriteWord(EndpointsAddress, 123);
        state = JsonSerializer.Deserialize<NativeBattleEffect167State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(0u, state.ReadDword(FirstRecordsAddress));
        Assert.Equal(0u, state.ReadDword(SecondHistoryAddress));
        Assert.Equal((ushort)123, state.ReadWord(EndpointsAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(FirstRecordsAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(EndpointsAddress + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(FirstSamplesAddress));
    }

    [Fact]
    public void Physical_bank_transfer_remains_exclusive_and_retains_warm_knownness_after_JSON()
    {
        NativeBattleEffect167State state = NativeBattleEffect167State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect167State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
        }
    }

    [Fact]
    public void Spline_arrays_share_their_actual_endpoints_without_claiming_descriptor_gaps()
    {
        NativeBattleEffect167State state = NativeBattleEffect167State.FromColdProcess().WriteDword(FirstSamplesAddress + 252, 0x12345678).WriteDword(SecondSamplesAddress, 0x87654321);
        state = JsonSerializer.Deserialize<NativeBattleEffect167State>(JsonSerializer.Serialize(state))!;
        Assert.Equal(BitConverter.GetBytes(0x12345678u).Concat(BitConverter.GetBytes(0x87654321u)), state.Read(FirstSamplesAddress + 252, 8));
        Assert.Equal(0x23BE118u, SecondSamplesAddress + 256);
        Assert.Throws<InvalidDataException>(() => state.Read(EndpointsAddress + 16, 1));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleEffectMemory.FromColdProcess() with { Effect167 = null }))!.Effect167);
    }
}
