using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleResonance49Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResonance49StateTests
{
    [Fact]
    public void Cold_loader_and_unknown_import_distinguish_six_banks_and_exact_lower_fronts()
    {
        NativeBattleResonance49State cold = NativeBattleResonance49State.FromColdProcess();
        NativeBattleResonance49State unknown = NativeBattleResonance49State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(new byte[EffectObjectLength], cold.Read(EffectObjectAddress, EffectObjectLength));
        Assert.Equal(new byte[ScreenObjectLength], cold.Read(ScreenObjectAddress, ScreenObjectLength));
        Assert.Equal(new byte[GridLength + ScratchLength], cold.Read(GridAddress, GridLength + ScratchLength));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(PrimaryCursorGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(ScratchAddress + ScratchLength - 2));
        for (int index = 0; index < Pools.Length; index++)
        {
            int length = Pools[index].Stride * Pools[index].Capacity;
            Assert.Equal(new byte[length], cold.InactiveBanks[index]!.Read(0, length));
            Assert.Throws<NotSupportedException>(() => unknown.InactiveBanks[index]!.Read(0, 2));
        }
    }

    [Fact]
    public void Sparse_globals_exclude_all_hierarchy_banks_descriptors_gaps_and_shared_owners()
    {
        NativeBattleResonance49State state = NativeBattleResonance49State.FromColdProcess();
        foreach (uint address in Pools.SelectMany(pool => new[] { pool.Bank, pool.Descriptor }).Concat(new uint[] { 0x26467E4, 0x2647358, 0x26478F4, 0x26480E0, SharedViewAddress, 0x1D8E054 }))
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(0x26467E2, 4));
        Assert.Throws<InvalidDataException>(() => state.WriteWord(0x26480E2, 0));
    }

    [Fact]
    public void Partial_grid_environment_packet_and_scratch_bytes_keep_knownness_through_JSON()
    {
        NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, new byte[] { 1, 2, 3, 4, 5, 6 });
        NativeBattleResonance49State state = NativeBattleResonance49State.FromUnknown().WriteWord(ScratchAddress + 6, 0xBEEF).WriteView(ScratchAddress, xyz, 6).WriteWord(GridAddress + 4, 0x8877).WriteDword(0x26472D8, 0x12345678).WriteDword(0x2647820 + 124, 0xCAFEBABE).WriteDword(PrimaryCursorGlobal, 0x2777000);
        NativeBattleResonance49State restored = JsonSerializer.Deserialize<NativeBattleResonance49State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, restored.Read(ScratchAddress, 6));
        Assert.Equal((ushort)0xBEEF, restored.ReadWord(ScratchAddress + 6));
        Assert.Equal((ushort)0x8877, restored.ReadWord(GridAddress + 4));
        Assert.Equal(0x12345678u, restored.ReadDword(0x26472D8));
        Assert.Equal(0xCAFEBABEu, restored.ReadDword(0x2647820 + 124));
        Assert.Equal(0x2777000u, restored.ReadDword(PrimaryCursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(GridAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(ScratchAddress + 8));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Inactive_bank_transfer_is_nullable_bounded_and_requires_exact_six_owners()
    {
        NativeBattleResonance49State state = NativeBattleResonance49State.FromUnknown();
        (state with { InactiveBanks = Enumerable.Repeat<NativeBattleEffectArena?>(null, 6).ToImmutableArray() }).Validate();
        Assert.Throws<InvalidDataException>(() => (state with { InactiveBanks = [] }).Validate());
        for (int index = 0; index < Pools.Length; index++)
        {
            int length = Pools[index].Stride * Pools[index].Capacity;
            NativeBattleEffectArena overflow = state.InactiveBanks[index]!.MarkUnknown(length, 1);
            Assert.Throws<InvalidDataException>(() => (state with { InactiveBanks = state.InactiveBanks.SetItem(index, overflow) }).Validate());
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.Write(checked((int)(RootBank - BaseAddress)), new byte[1]) }).Validate());
    }
}
