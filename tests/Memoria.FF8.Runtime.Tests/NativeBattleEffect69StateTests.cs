using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69StateTests
{
    [Fact]
    public void Cold_state_owns_only_sparse_globals_copied_view_and_two_inactive_banks()
    {
        NativeBattleEffect69State state = NativeBattleEffect69State.FromColdProcess();
        state.Validate();
        Assert.Equal(2, state.FixedBanks.Count);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.All(state.Read(address, length), value => Assert.Equal((byte)0, value));
        }
        Assert.Equal(new byte[0x10], state.ReadBank(0).Read(0, 0x10));
        Assert.Equal(new byte[0x8F0], state.ReadBank(1).Read(0, 0x8F0));
        Assert.Throws<InvalidDataException>(() => state.Read(0x255663C, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(MainBank, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x1D98B3C, 8));
    }

    [Fact]
    public void Unknown_import_keeps_partial_view_padding_unknown_and_retains_private_seed()
    {
        NativeBattleEffect69State state = NativeBattleEffect69State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RandomGlobal));
        NativeBattleEffectArena view = state.ReadView(ViewAddress, ViewLength).Write(0, BitConverter.GetBytes((short)-32768)).Write(20, BitConverter.GetBytes(int.MinValue));
        state = state.WriteView(ViewAddress, view, ViewLength).WriteDword(RandomGlobal, 0x7FFF).WriteDword(ArenaGlobal, 0x20DFAB8);
        Assert.Equal((ushort)0x8000, state.ReadWord(ViewAddress));
        Assert.Equal(0x80000000u, state.ReadDword(ViewAddress + 20));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CursorGlobal));
        Assert.Equal(0x7FFFu, state.ReadDword(RandomGlobal));
        Assert.Equal(0x20DFAB8u, state.ReadDword(ArenaGlobal));
        Assert.Equal(8, state.Read(RandomGlobal, 8).Length);
        state.Validate();
    }

    [Fact]
    public void Physical_bank_transfer_is_exclusive_and_preserves_retained_payload()
    {
        NativeBattleEffect69State state = NativeBattleEffect69State.FromUnknown();
        NativeBattleEffectArena main = state.ReadBank(MainBank).Write(12, BitConverter.GetBytes((ushort)7)).Write(0x8EE, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(MainBank));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(MainBank));
        state = state.AttachBank(MainBank, main);
        Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(1).Read(0x8EE, 2)));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(1).Read(14, 2));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(1, main));
        Assert.Throws<InvalidDataException>(() => state.DetachBank(2));
        NativeBattleEffect69State detached = state.DetachBank(1);
        Assert.Throws<InvalidDataException>(() => detached.AttachBank(1, main.Write(0x8F0, BitConverter.GetBytes((ushort)1))));
    }

    [Fact]
    public void Json_retains_current_seed_copied_view_knownness_and_transferred_bank_owner()
    {
        NativeBattleEffect69State state = NativeBattleEffect69State.FromUnknown().WriteDword(RandomGlobal, 12345).WriteWord(ViewAddress + 30, 0xA55A).DetachBank(0);
        string json = JsonSerializer.Serialize(state);
        NativeBattleEffect69State restored = JsonSerializer.Deserialize<NativeBattleEffect69State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(12345u, restored.ReadDword(RandomGlobal));
        Assert.Equal((ushort)0xA55A, restored.ReadWord(ViewAddress + 30));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ViewAddress + 28));
        Assert.Null(restored.FixedBanks[RootBank]);
    }
}
