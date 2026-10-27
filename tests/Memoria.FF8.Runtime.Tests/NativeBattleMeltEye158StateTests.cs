using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltEye158StateTests
{
    [Fact]
    public void Cold_loader_bytes_and_imported_unknown_bytes_remain_distinct()
    {
        NativeBattleMeltEye158State cold = NativeBattleMeltEye158State.FromColdProcess();
        NativeBattleMeltEye158State unknown = NativeBattleMeltEye158State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattleMeltEye158Profile.CursorGlobal));
        Assert.Equal(new byte[8], cold.ReadPacket(NativeBattleMeltEye158Profile.PacketBase, 8));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleMeltEye158Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadPacket(NativeBattleMeltEye158Profile.PacketBase, 1));
    }

    [Fact]
    public void Entry_clears_only_both_slot_head_arrays_and_retains_warm_suffixes_curves_packets()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromUnknown();
        foreach ((uint bank, int count) in new[] { (NativeBattleMeltEye158Profile.SlotBankA, 80), (NativeBattleMeltEye158Profile.SlotBankB, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                uint address = bank + (uint)(index * 24);
                state = state.WriteDword(address, 123).WriteWord(address + 4, (ushort)(index + 1));
            }
        }
        state = state.WriteDword(NativeBattleMeltEye158Profile.CurveBank + 15 * 440 - 4, 0xABCDEF12).WritePacket(NativeBattleMeltEye158Profile.PacketBase + 0x8000, [1, 2, 3, 4]);
        NativeBattleMeltEye158State cleared = state.ClearSlotHeads();
        foreach ((uint bank, int count) in new[] { (NativeBattleMeltEye158Profile.SlotBankA, 80), (NativeBattleMeltEye158Profile.SlotBankB, 100) })
        {
            for (int index = 0; index < count; index++)
            {
                uint address = bank + (uint)(index * 24);
                Assert.Equal(0u, cleared.ReadDword(address));
                Assert.Equal((ushort)(index + 1), cleared.ReadWord(address + 4));
                Assert.Throws<NotSupportedException>(() => cleared.ReadWord(address + 6));
            }
        }
        Assert.Equal(0xABCDEF12u, cleared.ReadDword(NativeBattleMeltEye158Profile.CurveBank + 15 * 440 - 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, cleared.ReadPacket(NativeBattleMeltEye158Profile.PacketBase + 0x8000, 4));
    }

    [Fact]
    public void Independent_packet_halves_partial_knownness_and_next_write_survive_JSON()
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromUnknown().WriteDword(NativeBattleMeltEye158Profile.CursorGlobal, NativeBattleMeltEye158Profile.PacketBase + 0x8000).WritePacket(NativeBattleMeltEye158Profile.PacketBase, [11]).WritePacket(NativeBattleMeltEye158Profile.PacketBase + 0x8000, [22]);
        NativeBattleMeltEye158State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMeltEye158State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 11 }, restored.ReadPacket(NativeBattleMeltEye158Profile.PacketBase, 1));
        Assert.Equal(new byte[] { 22 }, restored.ReadPacket(NativeBattleMeltEye158Profile.PacketBase + 0x8000, 1));
        Assert.Throws<NotSupportedException>(() => restored.ReadPacket(NativeBattleMeltEye158Profile.PacketBase + 1, 1));
        Assert.Equal(NativeBattleMeltEye158Profile.PacketBase + 0x8000, restored.ReadDword(NativeBattleMeltEye158Profile.CursorGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WritePacket(NativeBattleMeltEye158Profile.PacketBase + 1, [33])), NativeBattleTestSnapshots.Json(restored.WritePacket(NativeBattleMeltEye158Profile.PacketBase + 1, [33])));
    }

    [Theory]
    [InlineData(0x23D7B10u)]
    [InlineData(0x23D7B20u)]
    [InlineData(0x23D8C10u)]
    [InlineData(0x23D8C20u)]
    [InlineData(0x23DB3F8u)]
    [InlineData(0x23DB5BCu)]
    [InlineData(0x23EB5C0u)]
    public void Physical_pools_packets_and_unreached_gaps_have_no_duplicate_scalar_owner(uint address)
    {
        NativeBattleMeltEye158State state = NativeBattleMeltEye158State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
