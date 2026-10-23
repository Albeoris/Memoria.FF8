using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect183Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect183StateTests
{
    [Fact]
    public void Cold_loader_bytes_and_unknown_import_have_distinct_scalar_packet_and_task_knownness()
    {
        NativeBattleEffect183State cold = NativeBattleEffect183State.FromColdProcess();
        NativeBattleEffect183State unknown = NativeBattleEffect183State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[4], cold.ReadPacket(PacketBase + PacketBankBytes, 4));
        Assert.Equal(new byte[RootBankLength], cold.RootBankStorage!.Read(0, RootBankLength));
        Assert.Equal(new byte[ChildBankLength], cold.ChildBankStorage!.Read(0, ChildBankLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(PacketBase, 4));
        Assert.Throws<NotSupportedException>(() => unknown.ChildBankStorage!.Read(0, 4));
    }

    [Fact]
    public void Addressed_scalar_and_packet_owners_exclude_each_other_and_task_descriptors()
    {
        NativeBattleEffect183State state = NativeBattleEffect183State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(PacketBase));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(ChildBank, 0));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(RootDescriptor, 0));
        Assert.Throws<InvalidDataException>(() => state.ReadPacket(CursorGlobal, 4));
        Assert.Throws<InvalidDataException>(() => state.WritePacket(PacketBase - 1, new byte[1]));
        Assert.Equal(ChildBank + ChildBankLength, ParametersGlobal);
        Assert.Equal(PacketBase + PacketBytes, CursorGlobal);
    }

    [Fact]
    public void Partial_packet_writes_preserve_unknown_suffix_and_independent_other_bank_after_JSON_restore()
    {
        NativeBattleEffect183State state = NativeBattleEffect183State.FromUnknown().WriteDword(CursorGlobal, PacketBase + 4).WritePacket(PacketBase, BitConverter.GetBytes(0x0C000000u)).WritePacket(PacketBase + PacketBankBytes, new byte[] { 9 });
        NativeBattleEffect183State restored = JsonSerializer.Deserialize<NativeBattleEffect183State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(PacketBase + 4, restored.ReadDword(CursorGlobal));
        Assert.Equal(BitConverter.GetBytes(0x0C000000u), restored.ReadPacket(PacketBase, 4));
        Assert.Equal(new byte[] { 9 }, restored.ReadPacket(PacketBase + PacketBankBytes, 1));
        Assert.Throws<NotSupportedException>(() => restored.ReadPacket(PacketBase + 4, 1));
        Assert.Throws<NotSupportedException>(() => restored.ReadPacket(PacketBase + PacketBankBytes + 1, 1));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Transferred_task_banks_are_nullable_and_bounded_without_scalar_mirrors()
    {
        NativeBattleEffect183State state = NativeBattleEffect183State.FromUnknown();
        (state with { RootBankStorage = null, ChildBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = state.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ChildBankStorage = state.ChildBankStorage!.MarkUnknown(ChildBankLength, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Packets = state.Packets.Write(PacketBytes, new byte[1]) }).Validate());
    }
}
