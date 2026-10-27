using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285StateTests
{
    [Fact]
    public void Process_factory_initializes_only_five_sparse_ranges_and_exclusive_root()
    {
        NativeBattleEffect285State state = Assert.IsType<NativeBattleEffect285State>(NativeBattleEffectMemory.FromColdProcess().Effect285);
        state.Validate();
        Assert.Equal(5, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(MainDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ParticleDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CentroidGlobal + 2, 2));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaGlobal + 4, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, MorphLength));
    }

    [Fact]
    public void Partial_scalar_and_normalized_matrix_padding_knownness_survives_JSON()
    {
        NativeBattleEffect285State state = NativeBattleEffect285State.FromUnknown().WriteWord(CasterGlobal, 3).WriteWord(CentroidGlobal, 123).WriteDword(CursorGlobal, 456);
        NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().MarkUnknown(18, 2).Write(0, BitConverter.GetBytes((short)4096)).Write(20, BitConverter.GetBytes(789));
        state = state.WriteView(ViewAddress, matrix, 32);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect285State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)3, state.ReadWord(CasterGlobal));
        Assert.Equal((ushort)123, state.ReadWord(CentroidGlobal));
        Assert.Equal(456u, state.ReadDword(CursorGlobal));
        Assert.Equal(789u, state.ReadDword(ViewAddress + 20));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.Read(ViewAddress + 18, 2));
        Assert.Throws<InvalidDataException>(() => state.ReadDword(CentroidGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(SavedFlagsAddress));
    }

    [Fact]
    public void Root_transfer_retains_payload_and_unknown_tail_without_COMMON_mirror()
    {
        NativeBattleEffect285State state = NativeBattleEffect285State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect285State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
