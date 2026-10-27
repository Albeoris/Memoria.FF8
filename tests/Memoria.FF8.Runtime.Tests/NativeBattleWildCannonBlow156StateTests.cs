using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWildCannonBlow156StateTests
{
    [Fact]
    public void Cold_BSS_and_unknown_import_keep_scalar_and_inactive_bank_knownness_distinct()
    {
        NativeBattleWildCannonBlow156State cold = NativeBattleWildCannonBlow156State.FromColdProcess();
        NativeBattleWildCannonBlow156State unknown = NativeBattleWildCannonBlow156State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkspaceGlobal));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkspaceGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(SamplesAddress + 6));
        Assert.Throws<NotSupportedException>(() => unknown.RootBankStorage!.Read(0, 2));
    }

    [Fact]
    public void Partial_particle_flag_clear_preserves_retained_payload_and_unknown_suffix()
    {
        NativeBattleWildCannonBlow156State state = NativeBattleWildCannonBlow156State.FromUnknown().WriteWord(RecordAAddress + 22, 1234).WriteDword(RecordAAddress, 0);
        Assert.Equal(0u, state.ReadDword(RecordAAddress));
        Assert.Equal((ushort)1234, state.ReadWord(RecordAAddress + 22));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(RecordAAddress + 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RecordAAddress + 24));
    }

    [Fact]
    public void Sample_XYZ_write_retains_unknown_W_and_roundtrips()
    {
        NativeBattleEffectArena sample = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 0, 2, 0, 3, 0 });
        NativeBattleWildCannonBlow156State state = NativeBattleWildCannonBlow156State.FromUnknown().WriteView(SamplesAddress, sample, 8);
        NativeBattleWildCannonBlow156State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWildCannonBlow156State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0 }, restored.Read(SamplesAddress, 6));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(SamplesAddress + 6));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Canonical_COMMON_pointer_is_a_scalar_without_a_packet_mirror()
    {
        NativeBattleWildCannonBlow156State state = NativeBattleWildCannonBlow156State.FromColdProcess().WriteDword(WorkspaceGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketStep + 52);
        Assert.Equal(ArenaAddress, state.ReadDword(WorkspaceGlobal));
        Assert.Equal(ArenaAddress + PacketStep + 52, state.ReadDword(CursorGlobal));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(ArenaAddress));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ArenaAddress + PacketStep, 0));
    }

    [Fact]
    public void Inactive_banks_transfer_ownership_and_reject_oversized_storage()
    {
        NativeBattleWildCannonBlow156State state = NativeBattleWildCannonBlow156State.FromUnknown() with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength).Write(30, BitConverter.GetBytes((ushort)1234)) };
        NativeBattleWildCannonBlow156State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWildCannonBlow156State>(NativeBattleTestSnapshots.Json(state))!;
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
        Assert.Equal(RecordBAddress, RecordAAddress + RecordStride * RecordACount);
        Assert.Equal(RecordCAddress, RecordBAddress + RecordStride * RecordBCount);
        Assert.Equal(0x23EF3F8u, RecordCAddress + RecordStride * RecordCCount);
        Assert.Equal(ControlPointsAddress, ChildBank + ChildBankLength);
        Assert.Equal(TargetPointsAddress, ControlPointsAddress + 4 * 32);
        Assert.Equal(MatricesAddress, TargetPointsAddress + 4 * 8);
        Assert.Equal(ParametersGlobal, MatricesAddress + 4 * 32);
        Assert.Equal(SamplesAddress, StartPointsAddress + 4 * 8);
        Assert.Equal(WorkspaceGlobal, SamplesAddress + 4 * 32 * 8);
        Assert.Equal(CursorGlobal + 4, BaseAddress + GlobalsLength);
    }

    [Theory]
    [InlineData(0x23ECE44u)]
    [InlineData(0x23ECE48u)]
    [InlineData(0x23ECE68u)]
    [InlineData(0x23EF3F8u)]
    [InlineData(0x23EF478u)]
    [InlineData(0x23EF488u)]
    [InlineData(0x23F03C4u)]
    public void Scalar_access_excludes_actual_pools_descriptors_and_unused_gaps(uint address)
    {
        NativeBattleWildCannonBlow156State state = NativeBattleWildCannonBlow156State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
