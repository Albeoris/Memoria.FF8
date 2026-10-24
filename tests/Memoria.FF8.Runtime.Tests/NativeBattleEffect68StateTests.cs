using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect68StateTests
{
    [Fact]
    public void Cold_core_header_and_five_banks_exclude_physical_gaps_and_shared_owners()
    {
        NativeBattleEffect68State state = NativeBattleEffect68State.FromColdProcess();
        state.Validate();
        foreach ((uint address, int length) in OwnedRanges)
        {
            Assert.Equal(new byte[length], state.Read(address, length));
        }
        Assert.True(state.ColdSourceOrigin);
        Assert.False(state.MeshSourcesBound);
        Assert.Throws<InvalidDataException>(() => state.Read(0x25FCDDC, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x279CC54, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(RootDescriptor, 4));
        Assert.Throws<InvalidDataException>(() => state.Read(0x1D2A248, 48));
        Assert.Throws<InvalidDataException>(() => state.Read(0x1591C44, 8));
    }

    [Fact]
    public void Unknown_core_and_header_partial_writes_keep_independent_knownness_after_JSON()
    {
        NativeBattleEffect68State state = NativeBattleEffect68State.FromUnknown().WriteWord(CameraAddress + 0x30, 123).WriteDword(ResultCountAddress, 2);
        NativeBattleEffect68State restored = JsonSerializer.Deserialize<NativeBattleEffect68State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal((ushort)123, restored.ReadWord(CameraAddress + 0x30));
        Assert.Equal(2u, restored.ReadDword(ResultCountAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(CameraAddress + 0x32));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ResultPagesAddress));
        Assert.False(restored.ColdSourceOrigin);
        Assert.False(restored.MeshSourcesBound);
    }

    [Fact]
    public void All_five_fixed_banks_transfer_exclusively_and_preserve_unwritten_payload()
    {
        NativeBattleEffect68State state = NativeBattleEffect68State.FromUnknown();
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleEffectArena bank = state.ReadBank(index).Write(12, BitConverter.GetBytes((ushort)(index + 17)));
            state = state.DetachBank(index);
            Assert.Throws<InvalidDataException>(() => state.ReadBank(index));
            state = state.AttachBank(Pools[index].Bank, bank);
            Assert.Equal(BitConverter.GetBytes((ushort)(index + 17)), state.ReadBank(index).Read(12, 2));
            Assert.Throws<NotSupportedException>(() => state.ReadBank(index).Read(14, 2));
            Assert.Throws<InvalidDataException>(() => state.AttachBank(index, bank));
        }
        state.Validate();
    }

    [Fact]
    public void Cold_binding_adds_only_two_canonical_meshes_and_preserves_shared_Scan_state()
    {
        NativeBattleScanByteOwner existing = new([1, 2, 3, 4], [true, true, true, true]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Scan = memory.Scan! with { Complete269D93A = 7, MutableMeshes = memory.Scan!.MutableMeshes.Add(0x15CDDEC, existing) } };
        NativeBattleEffect68State state = NativeBattleEffect68State.FromColdProcess();
        (state, NativeBattleEffectMemory bound) = state.BindMeshSources(memory, Resources());
        Assert.True(state.MeshSourcesBound);
        Assert.Equal(3, bound.Scan!.MutableMeshes.Count);
        Assert.Same(existing, bound.Scan!.MutableMeshes[0x15CDDEC]);
        Assert.Equal((ushort)7, bound.Scan!.Complete269D93A);
        Assert.Same(memory.Scan!.Input, bound.Scan!.Input);
        Assert.Same(memory.Scan!.Text, bound.Scan!.Text);
        Assert.Same(memory.Scan!.Results, bound.Scan!.Results);
        Assert.Equal(1000, bound.Scan!.MutableMeshes[MutableMeshAddresses[0]].Bytes.Length);
        Assert.Equal(1896, bound.Scan!.MutableMeshes[MutableMeshAddresses[1]].Bytes.Length);
    }

    [Fact]
    public void Warm_binding_preserves_current_XYZ_W_and_never_refills_missing_bound_bytes()
    {
        NativeBattleEffect68Resources resources = Resources();
        (NativeBattleEffect68State state, NativeBattleEffectMemory memory) = NativeBattleEffect68State.FromColdProcess().BindMeshSources(NativeBattleEffectMemory.FromColdProcess(), resources);
        NativeBattleScanByteOwner warm = memory.Scan!.MutableMeshes[MutableMeshAddresses[0]].WriteWord(8, 0x8123).WriteWord(14, 0xCAFE);
        memory = memory with { Scan = memory.Scan! with { MutableMeshes = memory.Scan!.MutableMeshes.SetItem(MutableMeshAddresses[0], warm) } };
        memory = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        (state, memory) = state.BindMeshSources(memory, resources);
        Assert.Equal((ushort)0x8123, memory.Scan!.MutableMeshes[MutableMeshAddresses[0]].ReadWord(8));
        Assert.Equal((ushort)0xCAFE, memory.Scan!.MutableMeshes[MutableMeshAddresses[0]].ReadWord(14));
        NativeBattleEffectMemory missing = memory with { Scan = memory.Scan! with { MutableMeshes = memory.Scan!.MutableMeshes.Remove(MutableMeshAddresses[0]) } };
        Assert.Throws<NotSupportedException>(() => state.BindMeshSources(missing, resources));
        Assert.Throws<NotSupportedException>(() => resources.ReadCurrentMesh(missing, MutableMeshAddresses[0]));
    }

    [Fact]
    public void Unknown_history_cannot_seed_original_templates_and_partial_cold_publication_is_rejected()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect68State.FromUnknown().BindMeshSources(memory, Resources()));
        memory = memory with { Scan = memory.Scan! with { MutableMeshes = memory.Scan!.MutableMeshes.Add(MutableMeshAddresses[0], new(new byte[1000].ToImmutableArray(), Enumerable.Repeat(true, 1000).ToImmutableArray())) } };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect68State.FromColdProcess().BindMeshSources(memory, Resources()));
    }

    private static NativeBattleEffect68Resources Resources() => new(new("texture", []), 0, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(MutableMeshAddresses[0], new(0, MutableMeshAddresses[0], new byte[1000].ToImmutableArray())).Add(MutableMeshAddresses[1], new(0, MutableMeshAddresses[1], new byte[1896].ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
}
