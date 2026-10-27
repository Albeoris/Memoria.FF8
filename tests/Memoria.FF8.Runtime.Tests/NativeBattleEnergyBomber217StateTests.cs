using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnergyBomber217StateTests
{
    [Fact]
    public void Cold_scalar_bytes_are_known_and_imported_unknown_bytes_are_not_reset()
    {
        NativeBattleEnergyBomber217State cold = NativeBattleEnergyBomber217State.FromColdProcess();
        NativeBattleEnergyBomber217State unknown = NativeBattleEnergyBomber217State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal));
        Assert.Equal(0u, cold.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(NativeBattleEnergyBomber217Profile.InitializerGlobal));
    }

    [Fact]
    public void Partial_warm_scalar_state_and_next_write_survive_JSON()
    {
        NativeBattleEnergyBomber217State state = NativeBattleEnergyBomber217State.FromUnknown().WriteDword(NativeBattleEnergyBomber217Profile.WorkingGlobal, NativeBattleEnergyBomber217Profile.ArenaAddress).WriteWord(NativeBattleEnergyBomber217Profile.CursorGlobal, 0xA55A);
        NativeBattleEnergyBomber217State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEnergyBomber217State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Equal(NativeBattleEnergyBomber217Profile.ArenaAddress, restored.ReadDword(NativeBattleEnergyBomber217Profile.WorkingGlobal));
        Assert.Equal((ushort)0xA55A, restored.ReadWord(NativeBattleEnergyBomber217Profile.CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(NativeBattleEnergyBomber217Profile.CursorGlobal));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state.WriteWord(NativeBattleEnergyBomber217Profile.CursorGlobal + 2, 0x1234))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.WriteWord(NativeBattleEnergyBomber217Profile.CursorGlobal + 2, 0x1234))));
        Assert.Equal(NativeBattleEnergyBomber217Profile.PacketStep, NativeBattleEnergyBomber217Profile.SecondPacketOffset - NativeBattleEnergyBomber217Profile.FirstPacketOffset);
        Assert.Equal(NativeBattleEnergyBomber217Profile.FirstPacketOffset, NativeBattleEnergyBomber217Profile.MorphOffset + NativeBattleEnergyBomber217Profile.MorphBytes);
    }

    [Theory]
    [InlineData(0x2508800u)]
    [InlineData(0x2508810u)]
    [InlineData(0x2508820u)]
    [InlineData(0x2508830u)]
    [InlineData(0x2508840u)]
    [InlineData(0x2508870u)]
    public void Physical_task_bytes_and_outside_bytes_have_no_second_scalar_owner(uint address)
    {
        NativeBattleEnergyBomber217State state = NativeBattleEnergyBomber217State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }
}
