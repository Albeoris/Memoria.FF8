using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco99StateTests
{
    [Fact]
    public void Genuine_cold_state_has_one_unknown_external_span_and_shared_morph_without_warming_other_effects()
    {
        NativeBattleChoco99State state = NativeBattleChoco99State.FromColdProcess();
        state.Validate();
        Assert.Equal(0x1F560, state.PendingIoBanks!.Length);
        Assert.Equal(0x19D3018u, state.PendingIoBanks.BaseAddress);
        Assert.Throws<NotSupportedException>(() => state.PendingIoBanks.Read(0x19D3018, 1));
        Assert.Equal(new byte[0x760], state.MorphScratch);
        Assert.Null(state.ProjectedScratch);
        Assert.Null(state.Camera);
        Assert.Empty(state.Models);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleChoco97State? prior97 = memory.Choco97;
        NativeBattleChoco98State? prior98 = memory.Choco98;
        memory = memory with { Choco99 = state };
        Assert.Same(prior97, memory.Choco97);
        Assert.Same(prior98, memory.Choco98);
    }

    [Fact]
    public void Two_physical_model_decoder_owners_roundtrip_independently_while_shared_scratch_remains_single()
    {
        NativeBattleEffectOwnedModelState first = new(0x256D750, 0x1665B08, null);
        NativeBattleEffectOwnedModelState second = new(0x256D890, 0x1665B08, null);
        NativeBattleChoco99State state = NativeBattleChoco99State.FromColdProcess() with
        {
            Models = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty.Add(first.TaskAddress, first).Add(second.TaskAddress, second),
            ProjectedScratch = new byte[0xCD0].ToImmutableArray(),
            FreezePrimary = 2,
            FreezeSecondary = 7
        };
        state.Validate();
        NativeBattleChoco99State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco99State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(2, restored.Models.Count);
        Assert.Equal(first, restored.Models[first.TaskAddress]);
        Assert.Equal(second, restored.Models[second.TaskAddress]);
        Assert.Equal(state.ProjectedScratch!.Value.ToArray(), restored.ProjectedScratch!.Value.ToArray());
        Assert.Equal((uint?)2, restored.FreezePrimary);
        Assert.Equal((uint?)7, restored.FreezeSecondary);
        NativeBattleChoco99State malformed = state with { Models = state.Models.SetItem(first.TaskAddress, second) };
        Assert.Throws<InvalidDataException>(() => malformed.Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Models = state.Models.Add(0x256D9D0, new(0x256D9D0, 0x1665B08, null)) }).Validate());
    }

    [Fact]
    public void Warm_missing_sources_and_wrong_physical_extents_fail_without_reconstruction()
    {
        NativeBattleChoco99State state = NativeBattleChoco99State.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => (state with { ProgramSourceKnownCold = false }).Validate());
        Assert.Throws<NotSupportedException>(() => (state with { IoScriptKnownCold = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { MorphScratch = new byte[0x168].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ProjectedScratch = new byte[0xCD0 - 1].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { FreezePrimary = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { PendingIoBanks = new(0x19DB018, 0x13184, NativeBattleEffectArena.Clear()) }).Validate());
    }

    [Fact]
    public void Async_completion_writes_only_actual99_root_BYTE62_and_preserves_sibling_physical_slot()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(NativeBattleChoco99Controller.MainDescriptor, NativeBattleChoco99Controller.MainBank, 0x64, 2)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x717ED0, 0x64);
        NativeBattleOriginalEffectState effect = new(99, 98, new(99, 0x717D30, 0x717D10), new(0, 1, 8, 0, 0, 0), null) { Pools = root.Hierarchy.Pools };
        NativeBattleOriginalEffectState before = NativeBattleStorageSnapshots.Capture(effect);
        NativeBattleOriginalEffectState completed = NativeBattleChoco99Controller.WriteCompletionByte(effect, root.Address + 0x62, 1);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(before.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(effect.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x63));
        Assert.Equal(NativeBattleTestSnapshots.Json(before.Pools[0].Slots[1]), NativeBattleTestSnapshots.Json(completed.Pools[0].Slots[1]));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco99Controller.WriteCompletionByte(effect, root.Address + 0x63, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Controller.WriteCompletionByte(effect, root.Address + 0x62, 1));
    }
}
