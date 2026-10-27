using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDrawStock15NodeTests
{
    private const uint SourceAddress = 0x17FC68C;
    private const int First = 0x12000;
    private const int Second = First + 0x438;

    [Fact]
    public void Generic_profile_exposes_only_proved_node_contracts_and_preserves_existing_capacities()
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(15);
        Assert.Equal(SourceAddress, profile.ExecutableAddress);
        Assert.Equal(0x5944, profile.ExecutableLength);
        Assert.Equal(10, profile.EmitterCapacity);
        Assert.Equal(40, profile.GeometryCapacity);
        Assert.Equal(0x438, profile.EmitterStorageLength);
        Assert.Equal(0x6900, profile.GeometryStorageLength);
        Assert.Equal(0x9000, profile.DrawBankStep);
        Assert.Equal(0x8B0DA0u, profile.Callback(0x87E1A0));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaFamilyProfile.ForEffect(15));
        Assert.Throws<NotSupportedException>(() => profile.RootMode);
        Assert.Throws<NotSupportedException>(() => profile.Entry);
        Assert.Throws<NotSupportedException>(() => profile.Pools);
        Assert.Throws<NotSupportedException>(() => profile.Callback(0x87DC50));
        foreach (ushort effect in new ushort[] { 28, 52 })
        {
            NativeBattleCuragaFamilyProfile existing = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
            Assert.Equal(90, existing.EmitterCapacity);
            Assert.Equal(140, existing.GeometryCapacity);
            Assert.Equal(0x25F8, existing.EmitterStorageLength);
            Assert.Equal(0x16F80, existing.GeometryStorageLength);
            Assert.Equal(0x81B4, existing.ExecutableLength);
            Assert.Equal(0x6000, existing.DrawBankStep);
        }
    }

    [Fact]
    public void DrawStock_executable_fixups_use_the_distinct_extent_and_relocate_zero_channels_once()
    {
        byte[] bytes = new byte[0x5944];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x44), 0x1800);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x10C8), 24);
        NativeBattleCuragaExecutableSource source = new(bytes.ToImmutableArray()) { ResourceAddress = SourceAddress };
        NativeBattleCuragaExecutableState initialized = NativeBattleCuragaExecutableState.FromColdProcess(15).Initialize87EA20(source);
        Assert.Equal(0x5944, initialized.ByteLength);
        Assert.Equal(SourceAddress + 0x1000, Read(initialized, 4));
        Assert.Equal(SourceAddress + 0x1800, Read(initialized, 0x44));
        Assert.Equal(0u, Read(initialized, 8));
        Assert.Equal(SourceAddress + 24, Read(initialized, 0x10C8));
        Assert.Equal(SourceAddress, Read(initialized, 0x10CC));
        Assert.Same(initialized, initialized.Initialize87EA20(source));
        NativeBattleCuragaExecutableState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaExecutableState>(NativeBattleTestSnapshots.Json(initialized))!;
        Assert.Equal(initialized.Read(SourceAddress, 0x5944), restored.Initialize87EA20(source).Read(SourceAddress, 0x5944));
        Assert.Empty(initialized.Read(SourceAddress + 0x5944, 0));
        Assert.Throws<InvalidDataException>(() => initialized.Read(SourceAddress + 0x5944, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(source));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaExecutableState.FromColdProcess(15).Initialize87EA20(source with { Data = new byte[0x81B4].ToImmutableArray() }));
        Assert.Throws<NotSupportedException>(() => new NativeBattleCuragaExecutableState(1, null) { ResourceAddress = SourceAddress }.Initialize87EA20(source));
    }

    [Fact]
    public void Distinct_retained_owner_does_not_replace_Curaga_or_XPotion_and_unknown_owner_fails()
    {
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess() with { DrawStock15Executable = null };
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(15);
        Assert.Throws<NotSupportedException>(() => profile.RequireExecutable(original));
        NativeBattleCuragaExecutableState retained = NativeBattleCuragaExecutableState.FromColdProcess(15);
        NativeBattleEffectMemory updated = profile.WithExecutable(original, retained);
        Assert.Same(retained, profile.RequireExecutable(updated));
        Assert.Same(original.CuragaExecutable, updated.CuragaExecutable);
        Assert.Same(original.Potion52Executable, updated.Potion52Executable);
        Assert.Throws<InvalidDataException>(() => profile.WithExecutable(original, NativeBattleCuragaExecutableState.FromColdProcess()));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(updated))!;
        Assert.Equal(SourceAddress, profile.RequireExecutable(restored).ResourceAddress);
        Assert.Null(profile.RequireExecutable(restored).Data);
    }

    [Fact]
    public void Mixed_nodes_link_the_actual_smaller_storage_and_preserve_unknown_neighbor_bytes()
    {
        var fixture = Fixture();
        NativeBattleEffectArena arena = fixture.Frame.Memory.Arena!.MarkUnknown(First - 1, 1).MarkUnknown(Second + 0x6900, 1);
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = arena } };
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 5, 3);
        NativeBattleCuragaNodeAllocation geometry = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Address, First, 6);
        Assert.Equal(First, emitter.ArenaOffset);
        Assert.Equal(Second, geometry.ArenaOffset);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(geometry.Frame.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)1, task.ReadWord(0x48));
        Assert.Equal((ushort)1, task.ReadWord(0x4A));
        Assert.Equal(0x20DFAB8u + First, task.ReadDword(0x60));
        Assert.Equal(0x20DFAB8u + Second, task.ReadDword(0x64));
        Assert.Throws<NotSupportedException>(() => geometry.Frame.Memory.Arena!.Read(First - 1, 1));
        Assert.Throws<NotSupportedException>(() => geometry.Frame.Memory.Arena!.Read(Second + 0x6900, 1));
        NativeBattleCuragaFrame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(NativeBattleTestSnapshots.Json(geometry.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleCuragaNodes.Unlink882960(geometry.Frame, fixture.Address, First)), NativeBattleTestSnapshots.Json(NativeBattleCuragaNodes.Unlink882960(clone, fixture.Address, First)));
    }

    [Fact]
    public void Occupied_scan_wraps_before_last_physical_emitter_and_geometry_slots()
    {
        var fixture = Fixture();
        byte[] emitters = new byte[0x438];
        for (int index = 0; index < 9; index++)
        {
            emitters[index * 108 + 0x69] = 1;
        }
        NativeBattleCuragaFrame frame = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.Write(First, emitters) } };
        NativeBattleCuragaNodeAllocation blocked = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 5, 3);
        Assert.Null(blocked.ArenaOffset);
        Assert.Equal((ushort)2, blocked.Frame.State.Counter26D76C0);
        Assert.Equal(new byte[] { 0 }, blocked.Frame.Memory.Arena!.Read(First + 9 * 108 + 0x69, 1));
        frame = NativeBattleCuragaNodes.AllocateEmitter881700(fixture.Frame, fixture.Address, 0, 5, 3).Frame;
        byte[] geometry = new byte[0x6900];
        for (int index = 0; index < 39; index++)
        {
            geometry[index * 672 + 0x1D7] = 1;
        }
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(Second, geometry) } };
        blocked = NativeBattleCuragaNodes.AllocateGeometry882080(frame, fixture.Address, First, 6);
        Assert.Null(blocked.ArenaOffset);
        Assert.Equal((ushort)2, blocked.Frame.State.Counter26D82E8);
        Assert.Equal(new byte[] { 0 }, blocked.Frame.Memory.Arena!.Read(Second + 39 * 672 + 0x1D7, 1));
    }

    [Fact]
    public void Retained_cursor_can_address_last_physical_slots_then_wrap_and_reject_out_of_range()
    {
        var fixture = Fixture();
        NativeBattleCuragaFrame frame = fixture.Frame with { State = fixture.Frame.State with { Counter26D76C0 = 9, Counter26D82E8 = 39 } };
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 5, 3);
        Assert.Equal(First + 9 * 108, emitter.ArenaOffset);
        Assert.Equal((ushort)0, emitter.Frame.State.Counter26D76C0);
        NativeBattleCuragaNodeAllocation geometry = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Address, emitter.ArenaOffset!.Value, 6);
        Assert.Equal(Second + 39 * 672, geometry.ArenaOffset);
        Assert.Equal((ushort)0, geometry.Frame.State.Counter26D82E8);
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaNodes.AllocateEmitter881700(frame with { State = frame.State with { Counter26D76C0 = 10 } }, fixture.Address, 0, 5, 3));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame with { State = emitter.Frame.State with { Counter26D82E8 = 40 } }, fixture.Address, emitter.ArenaOffset.Value, 6));
    }

    private static uint Read(NativeBattleCuragaExecutableState memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(SourceAddress + (uint)offset, 4));

    private static (NativeBattleCuragaFrame Frame, uint Address) Fixture()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x273AA38, 0x273A248, 676, 3)]);
        NativeBattleEffectHierarchyAppend task = hierarchy.Append8DC540(0, 0x8B0DA0, 676);
        NativeBattleOriginalEffectState effect = new(15, 14, new(15, 0x8B0910, 0x8B08F0), new(0, 1, 8, 0, 0, 0), null) { Pools = task.Hierarchy.Pools };
        NativeBattleCuragaState state = new(0, null, null, First, null) { Auxiliary25F8 = First, Auxiliary16F80 = Second, Counter26D76C0 = 0, Counter26D82E8 = 0 };
        return (new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, state), task.Address);
    }
}
