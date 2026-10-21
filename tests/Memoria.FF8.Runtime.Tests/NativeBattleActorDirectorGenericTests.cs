using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorDirectorGenericTests
{
    [Theory]
    [InlineData(16, 0x17F7F1C, 0x4770, 90, 0x8AB890)]
    [InlineData(61, 0x1795000, 0x2CC0, 100, 0x7E6010)]
    public void Profiles_keep_dedicated_roots_and_exact_generic_storage(ushort effect, uint address, int length, int geometry, uint callback)
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        Assert.Equal(address, profile.ExecutableAddress);
        Assert.Equal(length, profile.ExecutableLength);
        Assert.Equal(20, profile.EmitterCapacity);
        Assert.Equal(geometry, profile.GeometryCapacity);
        Assert.Equal(0x870, profile.EmitterStorageLength);
        Assert.Equal(geometry * 672, profile.GeometryStorageLength);
        Assert.Equal(callback, profile.Callback(0x87E1A0));
        Assert.True(profile.UsesCommonPacketCursor);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaFamilyProfile.ForEffect(effect));
        Assert.Throws<NotSupportedException>(() => profile.RootMode);
        Assert.Throws<NotSupportedException>(() => profile.Pools);
        Assert.Throws<NotSupportedException>(() => profile.Callback(0x87DC50));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(61)]
    public void Banks_relocate_once_and_retain_independent_original_extents(ushort effect)
    {
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        byte[] bytes = new byte[profile.ExecutableLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x10C8), 24);
        NativeBattleCuragaExecutableSource source = new(bytes.ToImmutableArray()) { ResourceAddress = profile.ExecutableAddress };
        NativeBattleCuragaExecutableState state = NativeBattleCuragaExecutableState.FromColdProcess(effect).Initialize87EA20(source);
        Assert.Equal(profile.ExecutableAddress + 0x1000, BinaryPrimitives.ReadUInt32LittleEndian(state.Read(profile.ExecutableAddress + 4, 4)));
        Assert.Equal(profile.ExecutableAddress, BinaryPrimitives.ReadUInt32LittleEndian(state.Read(profile.ExecutableAddress + 0x10CC, 4)));
        Assert.Same(state, state.Initialize87EA20(source));
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess() with { ActorDirector16Executable = null, ActorDirector61Executable = null };
        NativeBattleEffectMemory updated = profile.WithExecutable(original, state);
        Assert.Same(state, profile.RequireExecutable(updated));
        Assert.Same(original.CuragaExecutable, updated.CuragaExecutable);
        Assert.Same(original.DrawStock15Executable, updated.DrawStock15Executable);
        Assert.Same(original.Potion52Executable, updated.Potion52Executable);
        ushort other = effect == 16 ? (ushort)61 : (ushort)16;
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaFamilyProfile.ForGenericNodes(other).RequireExecutable(updated));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(updated))!;
        Assert.Equal(state.Read(profile.ExecutableAddress, profile.ExecutableLength), profile.RequireExecutable(restored).Read(profile.ExecutableAddress, profile.ExecutableLength));
        Assert.Throws<InvalidDataException>(() => state.Read(profile.ExecutableAddress + (uint)profile.ExecutableLength, 1));
        Assert.Throws<NotSupportedException>(() => new NativeBattleCuragaExecutableState(1, null) { ResourceAddress = profile.ExecutableAddress }.Initialize87EA20(source));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(61)]
    public void Mode_zero_uses_actual_source_target_anchors_and_rejects_mode_two_before_materialization(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleCuragaFrame initialized = NativeBattleCuragaContext.Initialize87E230(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(initialized.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)1, task.ReadWord(0x50));
        Assert.Equal(3u, task.ReadDword(0x38));
        Assert.Equal(unchecked((uint)(-3 << 16)), task.ReadDword(0x34 + 0x1C8));
        Assert.Equal(unchecked((uint)(100 << 16)), task.ReadDword(0x34 + 0x1E8));
        Assert.Equal(unchecked((uint)(-200 << 16)), task.ReadDword(0x34 + 0xD8));
        Assert.Equal(15, task.ReadWord(0x4E));
        Assert.Equal(1u, NativeBattleCuragaFamilyProfile.ForGenericNodes(effect).RequireExecutable(initialized.Memory).InitializationFlag);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x29C, 2));
        NativeBattleCuragaFrame unsupported = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaContext.Initialize87E230(unsupported, fixture.Address, fixture.Resources, fixture.Inputs));
        Assert.Equal(0u, NativeBattleCuragaFamilyProfile.ForGenericNodes(effect).RequireExecutable(unsupported.Memory).InitializationFlag);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(61)]
    public void Allocation_uses_last_physical_slots_then_wraps_and_checks_actual_capacities(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        NativeBattleCuragaFrame frame = fixture.Frame with { State = fixture.Frame.State with { Counter26D76C0 = 19, Counter26D82E8 = (ushort)(profile.GeometryCapacity - 1) } };
        NativeBattleCuragaNodeAllocation emitter = NativeBattleCuragaNodes.AllocateEmitter881700(frame, fixture.Address, 0, 0, 0);
        Assert.Equal(0x12000 + 19 * 108, emitter.ArenaOffset);
        Assert.Equal((ushort)0, emitter.Frame.State.Counter26D76C0);
        NativeBattleCuragaNodeAllocation geometry = NativeBattleCuragaNodes.AllocateGeometry882080(emitter.Frame, fixture.Address, emitter.ArenaOffset!.Value, 0);
        Assert.Equal(0x12870 + (profile.GeometryCapacity - 1) * 672, geometry.ArenaOffset);
        Assert.Equal((ushort)0, geometry.Frame.State.Counter26D82E8);
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaNodes.AllocateEmitter881700(frame with { State = frame.State with { Counter26D76C0 = 20 } }, fixture.Address, 0, 0, 0));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(61)]
    public void Common_packet_commit_preserves_private_cursors_and_rejected_partial_stores(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleCuragaFrame frame = fixture.Frame with { State = fixture.Frame.State with { PrimaryDrawCursor = 1234, SecondaryDrawCursor = 5678 }, Memory = fixture.Frame.Memory with { PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() } };
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000220], []);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, primitive.Words[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), primitive.Words[1]);
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
        NativeBattleCuragaNodeDrawStep committed = NativeBattleCuragaNodeKinematics.CommitBatch(frame, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(8, committed.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(1234, committed.Frame.State.PrimaryDrawCursor);
        Assert.Equal(5678, committed.Frame.State.SecondaryDrawCursor);
        Assert.Equal(bytes.AsSpan(4).ToArray(), committed.Frame.Memory.PrimitiveProcess.ReadCommonPacket(4, 4));
        NativeBattleGfGeometryBatch rejected = new([new(0, ImmutableArray.Create<byte>(9, 8, 7, 6), [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known], null)], 0);
        NativeBattleCuragaNodeDrawStep partial = NativeBattleCuragaNodeKinematics.CommitBatch(committed.Frame, committed.Ordering, rejected);
        Assert.Equal(8, partial.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(new byte[] { 8 }, partial.Frame.Memory.PrimitiveProcess.ReadCommonPacket(9, 1));
        Assert.Equal(new byte[] { 6 }, partial.Frame.Memory.PrimitiveProcess.ReadCommonPacket(11, 1));
        Assert.Throws<NotSupportedException>(() => partial.Frame.Memory.PrimitiveProcess.ReadCommonPacket(8, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaNodeKinematics.CommitBatch(frame with { Memory = frame.Memory with { PrimitiveProcess = null } }, NativeBattleLogicalOrdering.Clear(), batch));
    }

    private static (NativeBattleCuragaFrame Frame, uint Address, NativeBattleCuragaResources Resources, NativeBattleCuragaControllerInputs Inputs) Fixture(ushort effect)
    {
        NativeBattleActorDirectorProfile profile = NativeBattleActorDirectorProfile.ForEffect(effect);
        NativeBattleEffectHierarchy hierarchy = new(profile.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, profile.Callback(0x8AB290), 100);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        NativeBattleEffectHierarchyAppend director = hierarchy.Append8DC540(2, profile.Callback(0x8AB600), 72, root.Address, (_, _, _) => new(2, 3));
        NativeBattleEffectHierarchyAppend node = director.Hierarchy.Append8DC540(3, profile.Callback(0x8AB890), 676, director.Address, (_, _, _) => new(2, 3));
        hierarchy = node.Hierarchy.Write(node.Address, node.Hierarchy.Read(node.Address).WriteDword(0x30, profile.SourceAddress).WriteWord(0x29E, 15));
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 4, 0, 0, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(2, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        memory = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect).WithExecutable(memory, NativeBattleCuragaExecutableState.FromColdProcess(effect));
        NativeBattleCuragaState state = new(0, null, null, 0, null) { Auxiliary25F8 = 0x12000, Auxiliary16F80 = 0x12870, Counter26D76C0 = 0, Counter26D82E8 = 0 };
        byte[] camera = new byte[profile.CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), (ushort)camera.Length);
        NativeBattleActorDirectorResources resources = new(effect, new("camera", camera.ToImmutableArray()), profile.Sound, new(new byte[profile.SourceLength].ToImmutableArray()) { ResourceAddress = profile.SourceAddress });
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource pose = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleGraphicsModel Model(int index) => new(index, 0) { Transform = new(new(0, 0, 0), index == 2 ? new(0, 100, 0) : new(-10, -200, 30), 0, -3, 4096), GeometryBounds52 = new(new(-10, -20, -30), new(100, 200, 300)) };
        return (new(memory, selected, state), node.Address, resources.GenericNodes(), new(packets, NativeBattleCuragaMatrices.Identity8DD770(), Model, _ => pose));
    }
}
