using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect173StateTests
{
    [Fact]
    public void Cold_factory_initializes_exact_sparse_banks_and_preinitialization_COMMON_queue()
    {
        NativeBattleEffect173State state = Assert.IsType<NativeBattleEffect173State>(NativeBattleEffectMemory.FromColdProcess().Effect173);
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[8], state.ReadCommonQueue().Read(0, 8));
        Assert.Equal(0u, state.ReadDword(CommonDescriptor));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2398A74, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2398A7C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2398A8C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CommonDescriptor + 8, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20F7AB8, 4));
    }

    [Fact]
    public void COMMON_queue_transfer_preserves_retained_links_and_is_exclusive_with_active_metadata()
    {
        NativeBattleEffect173State state = NativeBattleEffect173State.FromColdProcess().WriteDword(CommonDescriptor, 0x20F7AB8).WriteDword(CommonDescriptor + 4, 0x20F7BD4);
        NativeBattleEffectArena queue = state.ReadCommonQueue();
        state = state.DetachCommonQueue();
        Assert.Null(state.CommonQueueStorage);
        Assert.Throws<InvalidDataException>(() => state.Read(CommonDescriptor, 8));
        state = JsonSerializer.Deserialize<NativeBattleEffect173State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        state = state.AttachCommonQueue(queue);
        Assert.Equal(0x20F7AB8u, state.ReadDword(CommonDescriptor));
        Assert.Equal(0x20F7BD4u, state.ReadDword(CommonDescriptor + 4));
        Assert.Throws<InvalidDataException>(() => state.AttachCommonQueue(queue));
        Assert.Throws<InvalidDataException>(() => state.DetachCommonQueue().AttachCommonQueue(queue.Write(8, new byte[] { 1 })));
    }

    [Fact]
    public void Marker_only_reset_does_not_initialize_retained_face_payload_or_point_padding()
    {
        uint face = FaceRecordAddresses[0];
        NativeBattleEffect173State state = NativeBattleEffect173State.FromUnknown().WriteDword(FirstRecordsAddress, 0).WriteDword(face, 0).WriteWord(CenterAddress, 17);
        state = JsonSerializer.Deserialize<NativeBattleEffect173State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(0u, state.ReadDword(FirstRecordsAddress));
        Assert.Equal(0u, state.ReadDword(face));
        Assert.Equal((ushort)17, state.ReadWord(CenterAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(face + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CenterAddress + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CommonDescriptor));
    }

    [Fact]
    public void Fixed_bank_transfer_retains_unknown_and_warm_payload_after_JSON()
    {
        NativeBattleEffect173State state = NativeBattleEffect173State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect173State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
        }
    }

    [Fact]
    public void Face_record_banks_end_at_scalars_and_legacy_missing_owner_stays_unknown()
    {
        NativeBattleEffect173State state = NativeBattleEffect173State.FromColdProcess();
        for (int index = 0; index < FaceRecordAddresses.Length; index++)
        {
            Assert.Equal(FaceRecordLengths[index], state.Read(FaceRecordAddresses[index], FaceRecordLengths[index]).Length);
        }
        Assert.Equal(ArenaGlobal, checked(FaceRecordAddresses[^1] + (uint)FaceRecordLengths[^1]));
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleEffectMemory.FromColdProcess() with { Effect173 = null }))!.Effect173);
    }
}
