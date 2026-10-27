using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100StateTests
{
    [Fact]
    public void Genuine_cold_state_has_one_unknown_external_span_and_shared_morph_without_warming_other_effects()
    {
        NativeBattleChoco100State state = NativeBattleChoco100State.FromColdProcess();
        state.Validate();
        Assert.Equal(0x22300, state.PendingIoBanks!.Length);
        Assert.Equal(0x19D5018u, state.PendingIoBanks.BaseAddress);
        Assert.Throws<NotSupportedException>(() => state.PendingIoBanks.Read(0x19D5018, 1));
        Assert.Equal(new byte[0x120], state.MorphScratch);
        Assert.Equal(new byte[0xFE0], state.ProjectedScratch);
        Assert.Null(state.Camera);
        Assert.NotNull(state.SecondarySettings);
        NativeBattleChoco100State imported = state with { SecondarySettings = null };
        Assert.DoesNotContain("SecondarySettings", NativeBattleTestSnapshots.Json(imported));
        Assert.Null(NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco100State>(NativeBattleTestSnapshots.Json(imported))!.SecondarySettings);
        Assert.Empty(state.Models);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleChoco97State? prior97 = memory.Choco97;
        NativeBattleChoco98State? prior98 = memory.Choco98;
        memory = memory with { Choco100 = state };
        Assert.Same(prior97, memory.Choco97);
        Assert.Same(prior98, memory.Choco98);
    }

    [Fact]
    public void Two_physical_model_decoder_owners_roundtrip_independently_with_static_and_loaded_sources_while_shared_scratch_remains_single()
    {
        NativeBattleEffectOwnedModelState first = new(0x2565438, 0x1656DE4, null);
        NativeBattleEffectOwnedModelState second = new(0x2565578, 0x19E8678, null);
        NativeBattleChoco100State state = NativeBattleChoco100State.FromColdProcess() with
        {
            Models = ImmutableDictionary<uint, NativeBattleEffectOwnedModelState>.Empty.Add(first.TaskAddress, first).Add(second.TaskAddress, second),
            ProjectedScratch = new byte[0xFE0].ToImmutableArray(),
            FreezePrimary = 2,
            FreezeSecondary = 7
        };
        state.Validate();
        NativeBattleChoco100State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco100State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal(2, restored.Models.Count);
        Assert.Equal(first, restored.Models[first.TaskAddress]);
        Assert.Equal(second, restored.Models[second.TaskAddress]);
        Assert.Equal(state.ProjectedScratch.ToArray(), restored.ProjectedScratch.ToArray());
        Assert.Equal((uint?)2, restored.FreezePrimary);
        Assert.Equal((uint?)7, restored.FreezeSecondary);
        NativeBattleChoco100State malformed = state with { Models = state.Models.SetItem(first.TaskAddress, second) };
        Assert.Throws<InvalidDataException>(() => malformed.Validate());
        Assert.Throws<InvalidDataException>(() => (state with { Models = state.Models.Add(0x25656B8, new(0x25656B8, 0x1656DE4, null)) }).Validate());
    }

    [Fact]
    public void Warm_missing_sources_and_wrong_physical_extents_fail_without_reconstruction()
    {
        NativeBattleChoco100State state = NativeBattleChoco100State.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => (state with { ProgramSourceKnownCold = false }).Validate());
        Assert.Throws<NotSupportedException>(() => (state with { IoScriptKnownCold = false }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { MorphScratch = new byte[0x168].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ProjectedScratch = new byte[0xFE0 - 1].ToImmutableArray() }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { FreezePrimary = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { ProjectedScratch = default }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { PendingIoBanks = new(0x19DB018, 0x13184, NativeBattleEffectArena.Clear()) }).Validate());
    }

    [Fact]
    public void Entry_uses_parameter_header_base_for_actors_count_and_range()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
        NativeBattleActionHeaderSlot first = packets.Headers[0] with { Payload = packets.Headers[0].Payload.SetItem(16, 3).SetItem(17, 5) };
        NativeBattleActionHeaderSlot selected = packets.Headers[2] with { Payload = packets.Headers[2].Payload.SetItem(0, 4).SetItem(8, 0x44).SetItem(9, 0x83).SetItem(10, 0xD2).SetItem(11, 1).SetItem(16, 1).SetItem(17, 2), FirstContactIndex = 0 };
        packets = packets with { Headers = packets.Headers.SetItem(0, first).SetItem(2, selected) };
        NativeBattleOriginalEffectState effect = new(100, 99, new(100, 0x70D390, 0x70D370), new(0, 1, 8, 2, 0, 0), null)
        {
            ResourcePointers = [new(0x255EAC8, 0)]
        };
        NativeBattleChoco100Resources resources = new(null!, [], new(0x1663154, new byte[0x2884].ToImmutableArray()), null!, null!, null!, null!, null!, null!, 0, 0, 0, 0);
        NativeBattleChoco100Frame initialized = NativeBattleChoco100Controller.Initialize70D390(NativeBattleEffectMemory.FromColdProcess() with { Choco100 = NativeBattleChoco100State.FromColdProcess() }, effect, packets, resources, new(1));
        NativeBattleModulePoolTask root = new NativeBattleEffectHierarchy(initialized.Effect.Pools).Read(NativeBattleChoco100Controller.MainBank);
        Assert.Equal((byte)4, root.ReadByte(0x2C));
        Assert.Equal((byte)0, root.ReadByte(0x2D));
        Assert.Equal((ushort)1, root.ReadWord(0x5A));
        Assert.Equal((ushort)2, root.ReadWord(0x58));
        Assert.Equal((byte)2, root.ReadByte(0x2F));
    }

    [Fact]
    public void Loaded498_requires_actual_completed_IO_bytes_before_model_initialization()
    {
        ImmutableArray<byte> bytes = Enumerable.Range(0, 0x5090).Select(index => unchecked((byte)(index * 13))).ToImmutableArray();
        NativeBattleEffectOwnedModelResource model = new(0x19E8678, bytes, null!, []);
        NativeBattleChoco97IoBanks unknown = NativeBattleChoco100State.FromColdProcess().PendingIoBanks!;
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Controller.RequireLoadedModel498(null, model));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Controller.RequireLoadedModel498(unknown, model));
        NativeBattleChoco97IoBanks partial = unknown.Write(model.Address, bytes.AsSpan().Slice(0, bytes.Length - 1).ToArray().ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Controller.RequireLoadedModel498(partial, model));
        NativeBattleChoco97IoBanks loaded = unknown.Write(model.Address, bytes);
        NativeBattleChoco100Controller.RequireLoadedModel498(loaded, model);
        NativeBattleChoco97IoBanks mismatched = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(loaded)).Write(model.Address + 0x5000, [0xFF]);
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco100Controller.RequireLoadedModel498(mismatched, model));
        NativeBattleChoco97IoBanks cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleChoco97IoBanks>(NativeBattleTestSnapshots.Json(loaded))!;
        NativeBattleChoco100Controller.RequireLoadedModel498(cold, model);
        Assert.Equal(bytes.ToArray(), cold.Read(model.Address, bytes.Length));
    }

    [Fact]
    public void Async_completion_writes_only_actual100_root_BYTE62_and_preserves_sibling_physical_slot()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(NativeBattleChoco100Controller.MainDescriptor, NativeBattleChoco100Controller.MainBank, 0x64, 2)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x70EB00, 0x64);
        NativeBattleOriginalEffectState effect = new(100, 99, new(100, 0x70D390, 0x70D370), new(0, 1, 8, 0, 0, 0), null) { Pools = root.Hierarchy.Pools };
        NativeBattleOriginalEffectState before = NativeBattleStorageSnapshots.Capture(effect);
        NativeBattleOriginalEffectState completed = NativeBattleChoco100Controller.WriteCompletionByte(effect, root.Address + 0x62, 1);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(before.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(effect.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x62));
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(completed.Pools).Read(root.Address).ReadByte(0x63));
        Assert.Equal(NativeBattleTestSnapshots.Json(before.Pools[0].Slots[1]), NativeBattleTestSnapshots.Json(completed.Pools[0].Slots[1]));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100Controller.WriteCompletionByte(effect, root.Address + 0x63, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco98Controller.WriteCompletionByte(effect, root.Address + 0x62, 1));
    }
}
