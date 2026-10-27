using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEerieSoundWave130Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEerieSoundWave130StateTests
{
    [Fact]
    public void Cold_scalar_zeroes_and_imported_unknown_are_distinct()
    {
        NativeBattleEerieSoundWave130State cold = NativeBattleEerieSoundWave130State.FromColdProcess();
        cold.Validate();
        foreach (uint address in new[] { CasterGlobal, InitializerGlobal, WorkingGlobal, ParametersGlobal, CursorGlobal })
        {
            Assert.Equal(0u, cold.ReadDword(address));
            Assert.Throws<NotSupportedException>(() => NativeBattleEerieSoundWave130State.FromUnknown().ReadDword(address));
        }
    }

    [Theory]
    [InlineData(0x25210B8u)]
    [InlineData(0x25210C8u)]
    [InlineData(0x25210D0u)]
    [InlineData(0x2521100u)]
    [InlineData(0x25210A4u)]
    [InlineData(0x2521110u)]
    public void Task_bank_descriptors_and_outside_addresses_have_no_duplicate_scalar_owner(uint address)
    {
        NativeBattleEerieSoundWave130State state = NativeBattleEerieSoundWave130State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Partial_import_retains_warm_values_and_unknowns_through_JSON()
    {
        NativeBattleEerieSoundWave130State state = NativeBattleEerieSoundWave130State.FromUnknown().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        NativeBattleEerieSoundWave130State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEerieSoundWave130State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        restored.Validate();
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + FirstPacketOffset, restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CasterGlobal));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state.WriteDword(CursorGlobal, 0xABCDEF))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored.WriteDword(CursorGlobal, 0xABCDEF))));
    }
}
