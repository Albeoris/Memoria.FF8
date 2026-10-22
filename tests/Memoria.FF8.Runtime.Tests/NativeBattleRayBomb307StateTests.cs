using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb307StateTests
{
    [Fact]
    public void Cold_loader_scalar_and_particle_owners_are_zero_while_imported_unknown_state_is_not_seeded()
    {
        NativeBattleRayBomb307State cold = NativeBattleRayBomb307State.FromColdProcess();
        cold.Validate();
        Assert.Equal(0u, cold.ReadDword(CursorGlobal));
        Assert.Equal(new byte[24], cold.Particles.Read(0, 24));
        NativeBattleRayBomb307State unknown = NativeBattleRayBomb307State.FromUnknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.Particles.Read(0, 4));
    }

    [Theory]
    [InlineData(RootBank)]
    [InlineData(RootDescriptor)]
    [InlineData(ChildBank)]
    [InlineData(ChildDescriptor)]
    [InlineData(ParticlesAddress)]
    [InlineData(0x22416B4u)]
    public void Globals_exclude_canonical_task_pools_and_separate_particle_rows(uint address)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleRayBomb307State.FromColdProcess().WriteDword(address, 0));
    }

    [Fact]
    public void Saved_actor_partial_bytes_and_single_cursor_survive_JSON()
    {
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(0, BitConverter.GetBytes((ushort)0x1020)).Write(12, BitConverter.GetBytes(0x12345678u));
        NativeBattleRayBomb307State state = NativeBattleRayBomb307State.FromUnknown().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CursorGlobal, ArenaAddress + PacketBankLength).WriteView(SavedActorsAddress + 60, row, 20);
        state.Validate();
        string json = JsonSerializer.Serialize(state);
        NativeBattleRayBomb307State restored = JsonSerializer.Deserialize<NativeBattleRayBomb307State>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(ArenaAddress + PacketBankLength, restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadView(SavedActorsAddress + 60, 20).Read(4, 8));
        Assert.Equal((ushort)0x1020, restored.ReadWord(SavedActorsAddress + 60));
    }
}
