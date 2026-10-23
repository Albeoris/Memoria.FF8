using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath157Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath157StateTests
{
    [Fact]
    public void Cold_BSS_and_unknown_import_keep_scalar_and_inactive_bank_knownness_distinct()
    {
        NativeBattleBreath157State cold = NativeBattleBreath157State.FromColdProcess();
        NativeBattleBreath157State unknown = NativeBattleBreath157State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkspaceGlobal));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkspaceGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(TravelPointsAddress + 6));
        Assert.Throws<NotSupportedException>(() => unknown.RootBankStorage!.Read(0, 2));
    }

    [Fact]
    public void Partial_particle_flag_clear_preserves_retained_payload_and_unknown_suffix()
    {
        NativeBattleBreath157State state = NativeBattleBreath157State.FromUnknown().WriteWord(RecordAddress + 22, 1234).WriteDword(RecordAddress, 0);
        Assert.Equal(0u, state.ReadDword(RecordAddress));
        Assert.Equal((ushort)1234, state.ReadWord(RecordAddress + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(RecordAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RecordAddress + 24));
    }

    [Fact]
    public void Sample_XYZ_write_retains_unknown_W_and_roundtrips()
    {
        NativeBattleEffectArena sample = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 0, 2, 0, 3, 0 });
        NativeBattleBreath157State state = NativeBattleBreath157State.FromUnknown().WriteView(TravelPointsAddress, sample, 8);
        NativeBattleBreath157State restored = JsonSerializer.Deserialize<NativeBattleBreath157State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0 }, restored.Read(TravelPointsAddress, 6));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(TravelPointsAddress + 6));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Canonical_COMMON_pointer_is_a_scalar_without_a_packet_mirror()
    {
        NativeBattleBreath157State state = NativeBattleBreath157State.FromColdProcess().WriteDword(WorkspaceGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep + 52);
        Assert.Equal(ArenaAddress, state.ReadDword(WorkspaceGlobal));
        Assert.Equal(ArenaAddress + PacketStep + 52, state.ReadDword(CursorGlobal));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(ArenaAddress));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ArenaAddress + PacketStep, 0));
    }

    [Fact]
    public void Inactive_banks_transfer_ownership_and_reject_oversized_storage()
    {
        NativeBattleBreath157State state = NativeBattleBreath157State.FromUnknown() with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength).Write(30, BitConverter.GetBytes((ushort)1234)) };
        NativeBattleBreath157State restored = JsonSerializer.Deserialize<NativeBattleBreath157State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal((ushort)1234, BitConverter.ToUInt16(restored.RootBankStorage!.Read(30, 2)));
        Assert.Throws<NotSupportedException>(() => restored.RootBankStorage.Read(2, 2));
        (restored with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (restored with { RootBankStorage = restored.RootBankStorage.Write(RootBankLength, new byte[2]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (restored with { ChildBankStorage = restored.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
    }

    [Fact]
    public void Physical_extents_close_without_padding_or_shared_owner_overlap()
    {
        Assert.Equal(RootDescriptor, RootBank + RootBankLength);
        Assert.Equal(TargetPositionAddress, RecordAddress + RecordStride * RecordCount);
        Assert.Equal(ChildDescriptor, TargetPositionAddress + 8);
        Assert.Equal(PoseBAddress, ChildBank + ChildBankLength);
        Assert.Equal(DirectionAAddress, PoseAAddress + 32);
        Assert.Equal(DirectionBAddress, PositionAddress + 8);
        Assert.Equal(WorkspaceGlobal, TravelPointsAddress + TravelPointCount * 8);
        Assert.Equal(ScratchMacAddress, ScratchRotationAddress + 20);
        Assert.Equal(ScratchMacAddress + 12, BaseAddress + GlobalsLength);
    }

    [Fact]
    public void Scratch_rotation_has_no_entry_seed_and_retains_warm_bytes_and_unknown_padding()
    {
        NativeBattleBreath157State cold = NativeBattleBreath157State.FromColdProcess();
        Assert.Equal(new byte[20], cold.Read(ScratchRotationAddress, 20));
        NativeBattleBreath157State warm = NativeBattleBreath157State.FromUnknown().WriteWord(ScratchRotationAddress + 18, 0xCAFE).WriteDword(WorkspaceGlobal, ArenaAddress);
        Assert.Equal((ushort)0xCAFE, warm.ReadWord(ScratchRotationAddress + 18));
        Assert.Throws<NotSupportedException>(() => warm.ReadDword(ScratchRotationAddress));
        NativeBattleBreath157State restored = JsonSerializer.Deserialize<NativeBattleBreath157State>(JsonSerializer.Serialize(warm))!;
        restored.Validate();
        Assert.Equal((ushort)0xCAFE, restored.ReadWord(ScratchRotationAddress + 18));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ScratchRotationAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ScratchMacAddress));
    }

    [Theory]
    [InlineData(0x23EB5ECu)]
    [InlineData(0x23EB5F8u)]
    [InlineData(0x23EB618u)]
    [InlineData(0x23EBF90u)]
    [InlineData(0x23EBFA0u)]
    [InlineData(0x23ECDE4u)]
    [InlineData(0x23ECE40u)]
    public void Scalar_access_excludes_actual_pools_descriptors_and_unused_gaps(uint address)
    {
        NativeBattleBreath157State state = NativeBattleBreath157State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
