using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect322StateTests
{
    [Fact]
    public void Process_factory_initializes_two_exact_ranges_and_exclusive_root()
    {
        NativeBattleEffect322State state = Assert.IsType<NativeBattleEffect322State>(NativeBattleEffectMemory.FromColdProcess().Effect322);
        state.Validate();
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MainDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(PuffDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CasterGlobal + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaGlobal + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8 + MorphOffset, MorphLength));
    }

    [Fact]
    public void Unknown_import_preserves_partial_scalar_and_point_padding_knownness()
    {
        NativeBattleEffect322State state = NativeBattleEffect322State.FromUnknown().WriteWord(CasterGlobal, 4).WriteDword(CursorGlobal, 123);
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).Write(0, BitConverter.GetBytes((short)-123));
        state = state.WriteView(PointAddress, point, 8);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect322State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)4, state.ReadWord(CasterGlobal));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Equal(unchecked((ushort)-123), state.ReadWord(PointAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(PointAddress + 6));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
    }

    [Fact]
    public void Cold_point_becomes_current_retained_state_without_per_entry_refill()
    {
        NativeBattleEffect322State state = NativeBattleEffect322State.FromColdProcess().WriteDword(PointAddress, 0x12345678).WriteWord(PointAddress + 6, 0xBEEF);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect322State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        Assert.Equal(0x12345678u, state.ReadDword(PointAddress));
        Assert.Equal((ushort)0xBEEF, state.ReadWord(PointAddress + 6));
        Assert.Equal(ThirdPacketOffset + 4, MorphOffset);
    }

    [Fact]
    public void Root_bank_transfer_preserves_current_payload_and_unknown_tail()
    {
        NativeBattleEffect322State state = NativeBattleEffect322State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xABCD));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect322State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xABCD), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
