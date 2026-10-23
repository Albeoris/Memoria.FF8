using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSandStorm155Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSandStorm155StateTests
{
    [Fact]
    public void Cold_BSS_and_unknown_import_preserve_distinct_carrier_and_unowned_bank_knownness()
    {
        NativeBattleSandStorm155State cold = NativeBattleSandStorm155State.FromColdProcess();
        NativeBattleSandStorm155State unknown = NativeBattleSandStorm155State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(0u, cold.ReadDword(SelectedVerticesAddress));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(SelectedVerticesAddress + 6));
        Assert.Throws<NotSupportedException>(() => unknown.RootBankStorage!.Read(0, 2));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(8, 4));
    }

    [Fact]
    public void Packet_banks_and_cursor_have_separate_single_physical_owners()
    {
        NativeBattleSandStorm155State state = NativeBattleSandStorm155State.FromUnknown().WriteDword(PacketBase, 0x09000000).WriteDword(SecondPacketAddress, 0x0C000000).WriteDword(CursorGlobal, SecondPacketAddress + 52);
        state.Validate();
        Assert.Equal(0x09000000u, state.ReadDword(PacketBase));
        Assert.Equal(0x0C000000u, state.ReadDword(SecondPacketAddress));
        Assert.Equal(SecondPacketAddress + 52, state.ReadDword(CursorGlobal));
        Assert.Equal(0x09000000u, BitConverter.ToUInt32(state.Packets.Read(0, 4)));
        Assert.All(state.Globals.Regions, region => Assert.True(region.Offset + region.Data.Length <= PacketBase - BaseAddress || region.Offset >= CursorGlobal - BaseAddress));
    }

    [Fact]
    public void Selected_XYZ_write_retains_unknown_W_and_survives_JSON()
    {
        NativeBattleEffectArena vertex = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 0, 2, 0, 3, 0 });
        NativeBattleSandStorm155State state = NativeBattleSandStorm155State.FromUnknown().WriteView(SelectedVerticesAddress, vertex, 8);
        NativeBattleSandStorm155State restored = JsonSerializer.Deserialize<NativeBattleSandStorm155State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0 }, restored.Read(SelectedVerticesAddress, 6));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(SelectedVerticesAddress + 6));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Unowned_task_banks_preserve_payload_knownness_and_can_be_consumed_without_a_mirror()
    {
        NativeBattleSandStorm155State stored = NativeBattleSandStorm155State.FromUnknown() with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength).Write(14, BitConverter.GetBytes((ushort)1234)) };
        NativeBattleSandStorm155State restored = JsonSerializer.Deserialize<NativeBattleSandStorm155State>(JsonSerializer.Serialize(stored))!;
        restored.Validate();
        Assert.Equal((ushort)1234, BitConverter.ToUInt16(restored.RootBankStorage!.Read(14, 2)));
        Assert.Throws<NotSupportedException>(() => restored.RootBankStorage.Read(2, 2));
        NativeBattleSandStorm155State active = restored with { RootBankStorage = null, ChildBankStorage = null };
        active.Validate();
        Assert.Null(active.RootBankStorage);
        Assert.Null(active.ChildBankStorage);
        Assert.Throws<InvalidDataException>(() => active.ReadWord(RootBank + 14));
    }

    [Fact]
    public void Carrier_sample_vertex_sort_and_packet_extents_are_exact()
    {
        Assert.Equal(RecordBAddress, RecordAAddress + RecordStride * RecordCount);
        Assert.Equal(ChildDescriptor, RecordBAddress + RecordStride * RecordCount);
        Assert.Equal(ControlPointsAddress, ChildBank + ChildBankLength);
        Assert.Equal(TargetCenterAddress, ControlPointsAddress + ControlPointCount * 8);
        Assert.Equal(SelectedVerticesAddress, SamplesAddress + SampleCount * SampleStride);
        Assert.Equal(SortedRowsAddress, SelectedVerticesAddress + VertexCount * VertexStride);
        Assert.Equal(PacketBase, SortedRowsAddress + VertexCount * SortStride);
        Assert.Equal(SecondPacketAddress, PacketBase + PacketStep);
        Assert.Equal(CursorGlobal, PacketBase + PacketLength);
    }

    [Fact]
    public void Oversized_packet_and_unowned_task_storage_are_rejected()
    {
        NativeBattleSandStorm155State state = NativeBattleSandStorm155State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketLength, new byte[4]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[2]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
    }

    [Theory]
    [InlineData(0x23F0804u)]
    [InlineData(0x23F0808u)]
    [InlineData(0x23F0818u)]
    [InlineData(0x23F1AE8u)]
    [InlineData(0x23F1AF8u)]
    [InlineData(0x23F2944u)]
    public void Scalar_reads_and_writes_exclude_tasks_descriptors_and_unused_gaps(uint address)
    {
        NativeBattleSandStorm155State state = NativeBattleSandStorm155State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
