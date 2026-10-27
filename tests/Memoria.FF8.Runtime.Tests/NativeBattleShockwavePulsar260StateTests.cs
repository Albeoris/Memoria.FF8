using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260StateTests
{
    [Fact]
    public void Cold_scalar_loader_is_zero_but_imported_unknown_values_are_not_seeded()
    {
        NativeBattleShockwavePulsar260State cold = NativeBattleShockwavePulsar260State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Equal(new byte[32], cold.ReadView(CopiedViewAddress, 32).Read(0, 32));
        NativeBattleShockwavePulsar260State unknown = NativeBattleShockwavePulsar260State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(SavedFlagsAddress));
    }

    [Theory]
    [InlineData(ChildDescriptor)]
    [InlineData(MainDescriptor)]
    [InlineData(RootDescriptor)]
    [InlineData(RootBank)]
    [InlineData(RootBank + 12)]
    public void Scalar_owner_excludes_actual_descriptors_and_root_pool(uint address)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(address, 0));
    }

    [Fact]
    public void Current_pointer_and_partial_copied_matrix_survive_JSON_without_a_second_arena_owner()
    {
        NativeBattleEffectArena matrix = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(20, BitConverter.GetBytes(1234));
        NativeBattleShockwavePulsar260State state = NativeBattleShockwavePulsar260State.FromUnknown().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(PrimaryCursorGlobal, ArenaAddress + FirstPacketOffset).WriteView(CopiedViewAddress, matrix, 32);
        state.Validate();
        string json = NativeBattleTestSnapshots.Json(state);
        NativeBattleShockwavePulsar260State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShockwavePulsar260State>(json)!;
        restored.Validate();
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(ArenaAddress + FirstPacketOffset, restored.ReadDword(PrimaryCursorGlobal));
        Assert.Equal(1234u, restored.ReadDword(CopiedViewAddress + 20));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CopiedViewAddress + 18));
    }
}
