using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect63StateTests
{
    [Fact]
    public void Cold_factory_owns_exact_three_scalar_spans_and_one_root_bank()
    {
        NativeBattleEffect63State state = Assert.IsType<NativeBattleEffect63State>(NativeBattleEffectMemory.FromColdProcess().Effect63);
        state.Validate();
        Assert.Equal(3, OwnedRanges.Length);
        Assert.Equal(24, OwnedRanges.Sum(range => range.Length));
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[16], state.ReadBank(RootBank).Read(0, 16));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(CursorGlobal + 4, 1));
        Assert.Throws<InvalidDataException>(() => state.Read(MainDescriptor, 4));
    }

    [Fact]
    public void Unknown_source_bytes_stay_unknown_until_the_actual_field_writer()
    {
        NativeBattleEffect63State state = NativeBattleEffect63State.FromUnknown().WriteDword(CasterGlobal, 0x1D97530).WriteWord(ParametersGlobal, 7);
        Assert.Equal(0x1D97530u, state.ReadDword(CasterGlobal));
        Assert.Equal((ushort)7, state.ReadWord(ParametersGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ParametersGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(TargetGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(0, 4));
        state.Validate();
    }

    [Fact]
    public void Root_bank_transfer_is_exclusive_and_JSON_preserves_partial_knownness()
    {
        NativeBattleEffect63State state = NativeBattleEffect63State.FromUnknown();
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, [5]);
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect63State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!).AttachBank(0, bank);
        Assert.Equal(new byte[] { 5 }, state.ReadBank(0).Read(14, 1));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(15, 1));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        state.Validate();
    }

    [Fact]
    public void Retained_scalar_snapshot_transport_does_not_claim_unwritten_neighbors()
    {
        NativeBattleEffect63State state = NativeBattleEffect63State.FromColdProcess();
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(1, [0xA5]);
        state = state.WriteView(CursorGlobal, source, 4);
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect63State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        var actual = state.ReadView(CursorGlobal, 4).ReadSnapshot(0, 4);
        Assert.Equal(new bool[] { false, true, false, false }, actual.Known);
        Assert.Equal((byte)0xA5, actual.Data[1]);
        Assert.Throws<InvalidDataException>(() => state.WriteDword(WorkGlobal + 4, 0));
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0x25450F4, NativeBattleEffectArena.Clear()));
        state.Validate();
    }
}
