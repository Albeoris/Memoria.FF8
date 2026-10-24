using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect316StateTests
{
    [Fact]
    public void Cold_process_owns_only_sparse_scalars_and_exclusive_root_bank()
    {
        NativeBattleEffect316State state = Assert.IsType<NativeBattleEffect316State>(NativeBattleEffectMemory.FromColdProcess().Effect316);
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        foreach (uint address in new uint[] { BaseAddress + 12, MainDescriptor, RootDescriptor, RootBank, WorkGlobal + 4, 0x20DFAB8 + ProjectedRowsOffset })
        {
            Assert.Throws<InvalidDataException>(() => state.Read(address, 4));
        }
    }

    [Fact]
    public void Unknown_import_preserves_partial_RGB_and_other_scalar_unknownness()
    {
        NativeBattleEffect316State state = NativeBattleEffect316State.FromUnknown().Write(ColorGlobal, [3, 5, 7]).WriteDword(CasterGlobal, 3);
        state = JsonSerializer.Deserialize<NativeBattleEffect316State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(new byte[] { 3, 5, 7 }, state.Read(ColorGlobal, 3));
        Assert.Equal(3u, state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ColorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(RandomGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(WorkGlobal));
    }

    [Fact]
    public void Warm_random_and_RGB_history_survives_JSON_without_cold_refill()
    {
        NativeBattleEffect316State state = NativeBattleEffect316State.FromColdProcess().WriteDword(RandomGlobal, 0x1234).WriteDword(ColorGlobal, 0xABCDEF12);
        state = JsonSerializer.Deserialize<NativeBattleEffect316State>(JsonSerializer.Serialize(state))!;
        Assert.Equal(0x1234u, state.ReadDword(RandomGlobal));
        Assert.Equal(0xABCDEF12u, state.ReadDword(ColorGlobal));
        Assert.Equal(0u, state.ReadDword(AmplitudeGlobal));
    }

    [Fact]
    public void Root_transfer_preserves_partial_current_bytes_and_prevents_duplicate_owners()
    {
        NativeBattleEffect316State state = NativeBattleEffect316State.FromUnknown();
        NativeBattleEffectArena root = state.ReadBank(0).Write(16, BitConverter.GetBytes(0x12345678u));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = state.AttachBank(0, root);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, root));
        state = JsonSerializer.Deserialize<NativeBattleEffect316State>(JsonSerializer.Serialize(state))!;
        state.Validate();
        Assert.Equal(BitConverter.GetBytes(0x12345678u), state.ReadBank(0).Read(16, 4));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(0, 4));
        Assert.Throws<InvalidDataException>(() => state.ReadBank(1));
    }
}
