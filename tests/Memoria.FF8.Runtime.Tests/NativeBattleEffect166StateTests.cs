using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect166StateTests
{
    [Fact]
    public void Cold_state_owns_only_selected_sparse_bytes_and_inactive_banks()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffect166State state = Assert.IsType<NativeBattleEffect166State>(memory.Effect166);
        Assert.True(state.ColdSourceOrigin);
        Assert.False(state.StaticPacketBound);
        Assert.Null(state.CapturePacket24);
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x23BF03C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x23C325C, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x23C0524, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x1D9A828, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Marker_only_record_writes_do_not_initialize_retained_payload_or_matrix_padding()
    {
        NativeBattleEffect166State state = NativeBattleEffect166State.FromUnknown();
        state = state.WriteDword(FirstRecordsAddress, 0).WriteDword(SecondRecordsAddress, 0).WriteWord(MatrixAddress, 4096);
        NativeBattleEffect166State restored = JsonSerializer.Deserialize<NativeBattleEffect166State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(0u, restored.ReadDword(FirstRecordsAddress));
        Assert.Equal(0u, restored.ReadDword(SecondRecordsAddress));
        Assert.Equal((ushort)4096, restored.ReadWord(MatrixAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(FirstRecordsAddress + 4));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MatrixAddress + 18));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(AnchorAddress));
    }

    [Fact]
    public void Bank_transfer_is_exclusive_and_retains_unknown_and_warm_task_bytes_after_JSON()
    {
        NativeBattleEffect166State state = NativeBattleEffect166State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = JsonSerializer.Deserialize<NativeBattleEffect166State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
        }
    }

    [Fact]
    public void Saved_actor_fields_retain_word_padding_without_claiming_record_gaps()
    {
        NativeBattleEffect166State state = NativeBattleEffect166State.FromUnknown().WriteWord(SavedActorsAddress, 0x1234).WriteDword(SavedActorsAddress + 4, 0xABCDEF01);
        state = JsonSerializer.Deserialize<NativeBattleEffect166State>(JsonSerializer.Serialize(state))!;
        Assert.Equal((ushort)0x1234, state.ReadWord(SavedActorsAddress));
        Assert.Equal(0xABCDEF01u, state.ReadDword(SavedActorsAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(SavedActorsAddress + 2));
        Assert.Throws<InvalidDataException>(() => state.Read(SavedActorsAddress + 20, 8));
        Assert.Throws<InvalidDataException>(() => state.Read(SavedActorsAddress + SavedActorCount * SavedActorStride, 1));
    }

    [Fact]
    public void Unknown_packet_history_and_missing_bound_owner_fail_without_template_refill()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect166State.FromUnknown().BindCapturePacket(null!));
        NativeBattleEffect166State missing = NativeBattleEffect166State.FromColdProcess() with { StaticPacketBound = true };
        Assert.Throws<NotSupportedException>(() => missing.BindCapturePacket(null!));
        Assert.Throws<InvalidDataException>(() => missing.Validate());
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect166State.FromColdProcess().ReadCapturePacket());
    }

    [Fact]
    public void Environment_writer_owns_only_its_actual_prefix_and_paths_have_decimal_stride()
    {
        NativeBattleEffect166State state = NativeBattleEffect166State.FromColdProcess();
        Assert.Equal(820, PathStride);
        Assert.Equal(0x23C2760u, PathAddress + PathCount * PathStride);
        Assert.Equal(36, state.Read(EnvironmentRestoreAddress, EnvironmentRestoreLength).Length);
        Assert.Throws<InvalidDataException>(() => state.Read(EnvironmentRestoreAddress + EnvironmentRestoreLength, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(EnvironmentRestoreAddress, 64));
        Assert.Equal(0x23C3248u, RibbonAddress + RibbonStride * RibbonCount);
    }
}
