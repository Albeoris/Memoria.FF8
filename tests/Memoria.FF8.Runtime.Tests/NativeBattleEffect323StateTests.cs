using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect323StateTests
{
    [Fact]
    public void Process_factory_initializes_two_exact_ranges_and_exclusive_root()
    {
        NativeBattleEffect323State state = Assert.IsType<NativeBattleEffect323State>(NativeBattleEffectMemory.FromColdProcess().Effect323);
        state.Validate();
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MainDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ParticleDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CasterGlobal + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaGlobal + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8 + MorphOffset, MorphLength));
    }

    [Fact]
    public void Partial_current_scalar_knownness_and_warm_cursor_survive_JSON()
    {
        NativeBattleEffect323State state = NativeBattleEffect323State.FromUnknown().WriteWord(CasterGlobal, 4).WriteDword(CursorGlobal, 123);
        state = JsonSerializer.Deserialize<NativeBattleEffect323State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal((ushort)4, state.ReadWord(CasterGlobal));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
        Assert.Throws<InvalidDataException>(() => state.Read(CasterGlobal + 4, 4));
        Assert.Equal(208, FirstPacketOffset - MorphOffset);
    }

    [Fact]
    public void Root_bank_transfer_preserves_current_payload_and_unknown_tail()
    {
        NativeBattleEffect323State state = NativeBattleEffect323State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xABCD));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = JsonSerializer.Deserialize<NativeBattleEffect323State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xABCD), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
