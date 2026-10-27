using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect209StateTests
{
    [Fact]
    public void Cold_factory_initializes_exact_grid_scalars_and_two_exclusive_banks()
    {
        NativeBattleEffect209State state = Assert.IsType<NativeBattleEffect209State>(NativeBattleEffectMemory.FromColdProcess().Effect209);
        state.Validate();
        Assert.Equal(5, OwnedRanges.Length);
        Assert.Equal(13776, GridLength);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(2, state.FixedBanks.Count);
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ChildDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(SavedColor44Global + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x209FAB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Grid_initialization_preserves_unknown_projected_coordinates_through_JSON()
    {
        NativeBattleEffect209State state = NativeBattleEffect209State.FromUnknown().WriteWord(GridAddress + 4, unchecked((ushort)-164)).WriteWord(GridAddress + 6, unchecked((ushort)-112)).WriteWord(GridAddress + 8, 0).WriteWord(GridAddress + 10, 3);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect209State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(unchecked((ushort)-164), state.ReadWord(GridAddress + 4));
        Assert.Equal((ushort)3, state.ReadWord(GridAddress + 10));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(GridAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(GridAddress + GridStride + 8));
    }

    [Fact]
    public void Physical_bank_transfer_retains_warm_completion_WORD_and_unknown_payload()
    {
        NativeBattleEffect209State state = NativeBattleEffect209State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(index, bank);
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect209State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        for (int index = 0; index < Pools.Length; index++)
        {
            Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank(index).Read(14, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(12, 2));
        }
    }

    [Fact]
    public void Retained_colors_grid_and_legacy_missing_owner_are_not_reinitialized()
    {
        NativeBattleEffect209State state = NativeBattleEffect209State.FromUnknown().WriteDword(SavedColor40Global, 0x12345678).WriteDword(StageAfterColor44Global, 0xAABBCCDD).WriteDword(GridAddress + GridLength - 4, 0xCAFEFACE);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect209State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        Assert.Equal(0x12345678u, state.ReadDword(SavedColor40Global));
        Assert.Equal(0xAABBCCDDu, state.ReadDword(StageAfterColor44Global));
        Assert.Equal(0xCAFEFACEu, state.ReadDword(GridAddress + GridLength - 4));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(StageAfterColor40Global));
        Assert.Null(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffectMemory.FromColdProcess() with { Effect209 = null })))!).Effect209);
    }
}
