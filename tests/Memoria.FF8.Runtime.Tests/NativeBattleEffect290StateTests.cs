using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect290Resources;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect290StateTests
{
    [Fact]
    public void Process_factory_initializes_exact_scalar_ranges_and_exclusive_root()
    {
        NativeBattleEffect290State state = Assert.IsType<NativeBattleEffect290State>(NativeBattleEffectMemory.FromColdProcess().Effect290);
        state.Validate();
        Assert.True(state.ScriptSourcePending);
        Assert.Equal(2, OwnedRanges.Length);
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.Equal(new byte[20], state.ReadBank(0).Read(0, 20));
        Assert.Throws<InvalidDataException>(() => state.Read(RootBank, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(BaseAddress + 12, 4));
    }

    [Fact]
    public void Script_cold_bind_imports_only_exact_cells_and_warm_JSON_never_refills()
    {
        NativeBattleEffect290Resources resources = Sources();
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2000000, [9, 8, 7]);
        (NativeBattleEffect290State state, NativeBattleFileStaging bound) = NativeBattleEffect290State.FromColdProcess().BindSources(staging, resources);
        Assert.False(state.ScriptSourcePending);
        Assert.Equal(23, ScriptCountAddresses.Length);
        foreach (uint address in ScriptCountAddresses)
        {
            Assert.Equal(resources.OriginalScriptCount(address), bound.Read(address, 2));
            Assert.All(bound.ReadSnapshot(address - 1, 1).Known, known => Assert.False(known));
        }
        uint first = ScriptCountAddresses[0];
        bound = bound.Write(first, BitConverter.GetBytes((ushort)0));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect290State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!);
        bound = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(bound)))!);
        (state, bound) = state.BindSources(bound, resources);
        Assert.Equal(new byte[2], bound.Read(first, 2));
        Assert.Equal(new byte[] { 9, 8, 7 }, bound.Read(0x2000000, 3));
        Assert.Equal((short)0, resources.ReadScriptWord(bound, first));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(first, 2));
    }

    [Fact]
    public void Unknown_origin_partial_cold_conflict_and_missing_bound_cell_fail()
    {
        NativeBattleEffect290Resources resources = Sources();
        NativeBattleFileStaging unknown = NativeBattleFileStaging.Unknown();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect290State.FromUnknown().BindSources(unknown, resources));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect290State.FromColdProcess().BindSources(unknown.Write(ScriptCountAddresses[0], [1]), resources));
        NativeBattleEffect290State boundState = NativeBattleEffect290State.FromColdProcess() with { ScriptSourcePending = false };
        Assert.Throws<InvalidDataException>(() => boundState.BindSources(unknown, resources));
        Assert.Throws<NotSupportedException>(() => resources.ReadScriptWord(unknown, ScriptCountAddresses[0]));
        Assert.Throws<InvalidDataException>(() => resources.OriginalScriptCount(ScriptCountAddresses[0] + 1));
    }

    [Fact]
    public void Current_internal_random_and_bank_masks_survive_exclusive_JSON_transfer()
    {
        NativeBattleEffect290State state = NativeBattleEffect290State.FromUnknown().WriteDword(InternalRandomGlobal, 1234).WriteDword(CursorGlobal, 5678);
        NativeBattleEffectArena bank = state.ReadBank(0).Write(14, BitConverter.GetBytes((ushort)1));
        state = state.DetachBank(0);
        Assert.Throws<InvalidDataException>(() => state.ReadBank(0));
        state = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect290State>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(state)))!).AttachBank(0, bank);
        Assert.Throws<InvalidDataException>(() => state.AttachBank(0, bank));
        Assert.Equal(1234u, state.ReadDword(InternalRandomGlobal));
        Assert.Equal(5678u, state.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
        Assert.Equal((ushort)1, BitConverter.ToUInt16(state.ReadBank(0).Read(14, 2)));
        Assert.Throws<NotSupportedException>(() => state.ReadBank(0).Read(0, 4));
        state.Validate();
    }

    private static NativeBattleEffect290Resources Sources()
    {
        ImmutableDictionary<uint, ImmutableArray<byte>> fronts = ScriptCountAddresses.Select((address, index) => new KeyValuePair<uint, ImmutableArray<byte>>(address, BitConverter.GetBytes((ushort)(index + 10)).ToImmutableArray())).ToImmutableDictionary();
        return new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, fronts);
    }
}
