using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleGatlingGun305Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGatlingGun305StateTests
{
    [Fact]
    public void Cold_and_unknown_warm_packet_owners_have_distinct_knownness()
    {
        NativeBattleGatlingGun305State cold = NativeBattleGatlingGun305State.FromColdProcess();
        NativeBattleGatlingGun305State unknown = NativeBattleGatlingGun305State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(new byte[8], cold.ReadPacket(PacketBase, 8));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(PacketBase, 8));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        NativeBattleGatlingGun305State changed = unknown.WritePacket(PacketBase + 4, [1, 2, 3, 4]).WriteDword(CursorGlobal, PacketBase + 8);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, changed.ReadPacket(PacketBase + 4, 4));
        Assert.Equal(PacketBase + 8, changed.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => changed.ReadPacket(PacketBase, 4));
    }

    [Fact]
    public void Entry_clears_only_both_slot_heads_and_preserves_binding_and_packets()
    {
        NativeBattleResourcePointer pointer = new(null, 0xCEB5FC, 0x1634);
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromUnknown().WriteResourcePointer(BindingSave + 8, pointer).WriteWord(SlotBankA + 6, 0xA987).WriteWord(SlotBankB + 22, 0xFEDC).WritePacket(PacketBase + PacketBankBytes, [9, 8, 7, 6]).WriteDword(CursorGlobal, PacketBase + PacketBankBytes + 4).ClearSlotHeads();
        foreach (uint bank in new[] { SlotBankA, SlotBankB })
        {
            for (int index = 0; index < SlotCount; index++)
            {
                uint address = bank + (uint)(index * SlotStride);
                Assert.Equal(0u, state.ReadDword(address));
                Assert.Throws<NotSupportedException>(() => state.ReadWord(address + 4));
            }
        }
        Assert.Equal((ushort)0xA987, state.ReadWord(SlotBankA + 6));
        Assert.Equal((ushort)0xFEDC, state.ReadWord(SlotBankB + 22));
        Assert.Equal(pointer, state.ReadResourcePointer(BindingSave + 8));
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, state.ReadPacket(PacketBase + PacketBankBytes, 4));
        Assert.Equal(PacketBase + PacketBankBytes + 4, state.ReadDword(CursorGlobal));
        state.Validate();
    }

    [Theory]
    [InlineData(0x2245658u)]
    [InlineData(0x2245668u)]
    [InlineData(0x2246958u)]
    [InlineData(0x2246968u)]
    [InlineData(0x2247790u)]
    [InlineData(0x224F790u)]
    public void Native_task_and_packet_bytes_cannot_have_a_second_scalar_owner(uint address)
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 0));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
    }

    [Fact]
    public void Partial_fixed_bank_and_scalar_snapshot_restore_preserves_independent_next_writes()
    {
        NativeBattleGatlingGun305State state = NativeBattleGatlingGun305State.FromUnknown().WriteDword(CursorGlobal, PacketBase + 12).WritePacket(PacketBase + 8, [1, 2, 3, 4]);
        string json = JsonSerializer.Serialize(state);
        NativeBattleGatlingGun305State restored = JsonSerializer.Deserialize<NativeBattleGatlingGun305State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(JsonSerializer.Serialize(state.WritePacket(PacketBase + 12, [5, 6, 7, 8]).WriteDword(CursorGlobal, PacketBase + 16)), JsonSerializer.Serialize(restored.WritePacket(PacketBase + 12, [5, 6, 7, 8]).WriteDword(CursorGlobal, PacketBase + 16)));
        Assert.Throws<InvalidDataException>(() => restored.ReadPacket(PacketBase + PacketBytes, 1));
        Assert.Throws<InvalidDataException>(() => restored.WritePacket(PacketBase - 1, [0]));
    }
}
