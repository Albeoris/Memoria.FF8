using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319StateTests
{
    [Fact]
    public void Process_factory_initializes_exact_sparse_scalars_and_exclusive_root_bank()
    {
        NativeBattleEffect319State state = Assert.IsType<NativeBattleEffect319State>(NativeBattleEffectMemory.FromColdProcess().Effect319);
        state.Validate();
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x25053B0, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ArenaGlobal + 4, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(0x20DFAB8, 4));
    }

    [Fact]
    public void Width_specific_knownness_survives_JSON_without_fabricating_current_pose_or_pointer_bytes()
    {
        NativeBattleEffect319State state = NativeBattleEffect319State.FromUnknown().WriteWord(CasterGlobal, 0xCAFE).WriteDword(CursorGlobal, 123).WriteDword(ArenaGlobal, 456);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect319State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)0xCAFE, state.ReadWord(CasterGlobal));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Equal(456u, state.ReadDword(ArenaGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(InitializerGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParametersGlobal));
    }

    [Fact]
    public void Root_transfer_preserves_warm_payload_and_unknown_suffix_exclusively()
    {
        NativeBattleEffect319State state = NativeBattleEffect319State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)0xBEEF));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect319State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal(BitConverter.GetBytes((ushort)0xBEEF), state.ReadBank(0).Read(14, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(16, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
