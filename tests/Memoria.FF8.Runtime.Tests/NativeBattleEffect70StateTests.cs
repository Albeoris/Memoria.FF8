using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70StateTests
{
    [Fact]
    public void Cold_state_owns_only_sparse_BSS_and_the_inactive_fixed_root_bank()
    {
        NativeBattleEffect70State state = NativeBattleEffect70State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.All(state.Read(address, length), value => Assert.Equal((byte)0, value));
        }
        Assert.Equal(new byte[16], state.ReadRootBank().Read(0, 16));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2544E94, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2544F00, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaAddress + MainBankOffset, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x2793E58, 32));
    }

    [Fact]
    public void Unknown_retained_scalars_and_private_view_require_actual_bytes()
    {
        NativeBattleEffect70State state = NativeBattleEffect70State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RetainedFirstGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RetainedSecondGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(PreviousDepthGlobal));
        state = state.WriteDword(RetainedFirstGlobal, 0x80000001).WriteWord(ViewAddress + 18, 0xBEEF);
        Assert.Equal(0x80000001u, state.ReadDword(RetainedFirstGlobal));
        Assert.Equal((ushort)0xBEEF, state.ReadWord(ViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ViewAddress + 20));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RetainedSecondGlobal));
        state.Validate();
    }

    [Fact]
    public void Root_bank_transfer_is_exclusive_and_retains_unwritten_payload_knownness()
    {
        NativeBattleEffect70State state = NativeBattleEffect70State.FromUnknown();
        NativeBattleEffectArena root = state.ReadRootBank().Write(12, BitConverter.GetBytes((ushort)7));
        state = state.DetachRootBank();
        Assert.Throws<InvalidDataException>(() => state.ReadRootBank());
        Assert.Throws<InvalidDataException>(() => state.DetachRootBank());
        state = state.AttachRootBank(root);
        Assert.Equal(BitConverter.GetBytes((ushort)7), state.ReadRootBank().Read(12, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadRootBank().Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachRootBank(root));
        Assert.Throws<InvalidDataException>(() => state.DetachRootBank().AttachRootBank(root.Write(16, [(byte)1])));
    }

    [Fact]
    public void Json_preserves_current_views_saved_flags_and_exclusive_root_ownership()
    {
        NativeBattleEffect70State state = NativeBattleEffect70State.FromUnknown().WriteDword(ArenaGlobal, ArenaAddress).WriteDword(SavedFlagsAddress + 6 * 4, 0x89AB).DetachRootBank();
        NativeBattleEffectArena view = state.ReadView(ViewAddress, ViewLength).Write(0, BitConverter.GetBytes((short)-32768)).Write(20, BitConverter.GetBytes(int.MinValue));
        state = state.WriteView(ViewAddress, view, ViewLength);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state));
        NativeBattleEffect70State restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect70State>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Null(restored.RootBankStorage);
        Assert.Equal(ArenaAddress, restored.ReadDword(ArenaGlobal));
        Assert.Equal(0x89ABu, restored.ReadDword(SavedFlagsAddress + 6 * 4));
        Assert.Equal((ushort)0x8000, restored.ReadWord(ViewAddress));
        Assert.Equal(0x80000000u, restored.ReadDword(ViewAddress + 20));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ViewAddress + 18));
        Assert.Throws<InvalidDataException>(() => restored.Read(SavedFlagsAddress + 28, 1));
    }
}
