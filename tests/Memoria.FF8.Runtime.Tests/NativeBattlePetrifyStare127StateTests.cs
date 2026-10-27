using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattlePetrifyStare127Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePetrifyStare127StateTests
{
    [Fact]
    public void Cold_scalar_zeroes_and_imported_unknown_are_distinct()
    {
        NativeBattlePetrifyStare127State cold = NativeBattlePetrifyStare127State.FromColdProcess();
        cold.Validate();
        foreach (uint address in new[] { StageDepthGlobal, CasterGlobal, InitializerGlobal, WorkingGlobal, ParametersGlobal, CursorGlobal })
        {
            Assert.Equal(0u, cold.ReadDword(address));
            Assert.Throws<NotSupportedException>(() => NativeBattlePetrifyStare127State.FromUnknown().ReadDword(address));
        }
    }

    [Theory]
    [InlineData(0x2521288u)]
    [InlineData(0x2521298u)]
    [InlineData(0x25212A0u)]
    [InlineData(0x25212B0u)]
    [InlineData(0x252126Cu)]
    [InlineData(0x25212C0u)]
    public void Task_bank_descriptors_and_outside_addresses_have_no_duplicate_scalar_owner(uint address)
    {
        NativeBattlePetrifyStare127State state = NativeBattlePetrifyStare127State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 1));
    }

    [Fact]
    public void Partial_import_retains_warm_values_and_unknowns_through_JSON()
    {
        NativeBattlePetrifyStare127State state = NativeBattlePetrifyStare127State.FromUnknown().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + FirstPacketOffset);
        NativeBattlePetrifyStare127State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattlePetrifyStare127State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(ArenaAddress, restored.ReadDword(WorkingGlobal));
        Assert.Equal(ArenaAddress + FirstPacketOffset, restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CasterGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.WriteDword(CursorGlobal, 0xABCDEF)), NativeBattleTestSnapshots.Json(restored.WriteDword(CursorGlobal, 0xABCDEF)));
    }
}
