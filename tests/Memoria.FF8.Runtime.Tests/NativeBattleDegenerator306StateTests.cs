using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleDegenerator306Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306StateTests
{
    [Fact]
    public void Loader_cold_globals_and_unknown_warm_globals_are_distinct()
    {
        NativeBattleDegenerator306State cold = NativeBattleDegenerator306State.FromColdProcess();
        NativeBattleDegenerator306State unknown = NativeBattleDegenerator306State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        NativeBattleDegenerator306State selected = unknown.WriteDword(WorkingGlobal, ArenaAddress);
        Assert.Equal(ArenaAddress, selected.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => selected.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Entry_slot_reset_clears_only_each_active_DWORD_and_preserves_unknown_tail()
    {
        NativeBattleDegenerator306State state = NativeBattleDegenerator306State.FromUnknown().WriteWord(SlotBank + 6, 0xFEDC).ClearSlotHeads();
        for (int index = 0; index < SlotCount; index++)
        {
            uint row = SlotBank + (uint)(index * SlotStride);
            Assert.Equal(0u, state.ReadDword(row));
            Assert.Throws<NotSupportedException>(() => state.ReadWord(row + 4));
        }
        Assert.Equal((ushort)0xFEDC, state.ReadWord(SlotBank + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(SlotBank + 8));
        state.Validate();
    }

    [Theory]
    [InlineData(0x2242598u)]
    [InlineData(0x22425B8u)]
    [InlineData(0x2243348u)]
    [InlineData(0x2243358u)]
    public void Task_banks_and_descriptors_have_no_second_Global_owner(uint address)
    {
        NativeBattleDegenerator306State state = NativeBattleDegenerator306State.FromColdProcess();
        Assert.Throws<InvalidDataException>(() => state.WriteDword(address, 0));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(address));
    }

    [Fact]
    public void Binding_pointer_and_partial_slots_survive_JSON_without_numeric_pointer_substitution()
    {
        NativeBattleResourcePointer pointer = new(null, 0xCE4560, 0x1634);
        NativeBattleDegenerator306State state = NativeBattleDegenerator306State.FromUnknown().WriteResourcePointer(BindingSave + 8, pointer).WriteWord(SlotBank + 12, 0x8040).ClearSlotHeads();
        NativeBattleDegenerator306State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDegenerator306State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(pointer, restored.ReadResourcePointer(BindingSave + 8));
        Assert.Equal((ushort)0x8040, restored.ReadWord(SlotBank + 12));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(NativeBattleTestSnapshots.Json(state.ClearSlotHeads()), NativeBattleTestSnapshots.Json(restored.ClearSlotHeads()));
    }
}
