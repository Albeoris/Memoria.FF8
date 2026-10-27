using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect140StateTests
{
    [Fact]
    public void Cold_state_owns_only_selected_sparse_scalars_and_inactive_root()
    {
        NativeBattleEffect140State state = NativeBattleEffect140State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x2517B14, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CompletionGlobal + 1, 3));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(ModelSkeletonAddress, 4));
        Assert.False(state.StaticSourcesBound);
    }

    [Fact]
    public void Partial_matrix_write_retains_unknown_padding_through_JSON()
    {
        NativeBattleEffect140State state = NativeBattleEffect140State.FromUnknown().WriteWord(ViewAddress, 4096).WriteDword(CursorGlobal, 123);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect140State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        state.Validate();
        Assert.Equal((ushort)4096, state.ReadWord(ViewAddress));
        Assert.Equal(123u, state.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(ViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(PauseGlobal));
    }

    [Fact]
    public void Inactive_root_transfer_is_exclusive_and_preserves_retained_unknown_bytes()
    {
        NativeBattleEffect140State state = NativeBattleEffect140State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank().Write(12, BitConverter.GetBytes((ushort)0xCAFE));
        state = state.DetachBank();
        Assert.Throws<InvalidDataException>(() => state.ReadBank());
        state = state.AttachBank(bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(bank));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect140State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        Assert.Equal(BitConverter.GetBytes((ushort)0xCAFE), state.ReadBank().Read(12, 2));
        Assert.Throws<NotSupportedException>(() => state.ReadBank().Read(14, 2));
    }

    [Fact]
    public void Unknown_import_cannot_publish_original_mutable_sources()
    {
        NativeBattleEffect140State state = NativeBattleEffect140State.FromUnknown();
        Assert.Throws<NotSupportedException>(() => state.BindSources(NativeBattleFileStaging.Unknown(), null!));
    }
}
