using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVictoryResourceTests
{
    [Fact]
    public void SymbolicCpuPointerMakesNumericBytesUnknownAndPartialOverwriteCannotRecoverThem()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleResourcePointer pointer = new(new(2, 40), null);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(address, [1, 2, 3, 4, 5, 6]).WriteResourcePointer(address, pointer);
        Assert.Equal(pointer, staging.ReadResourcePointer(address));
        Assert.Throws<NotSupportedException>(() => staging.Read(address, 4));
        staging = staging.Write(address, [7, 8]);
        Assert.False(staging.ResourcePointers.ContainsKey(address));
        Assert.Equal(new byte[] { 7, 8 }, staging.Read(address, 2));
        Assert.Equal(new byte[] { 5, 6 }, staging.Read(address + 4, 2));
        Assert.Throws<NotSupportedException>(() => staging.Read(address, 4));
    }

    [Fact]
    public void AbsolutePointerAliasesSurviveJsonAndLowerFieldArenaRebase()
    {
        uint address = NativeBattleFileStaging.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(address + 4, new(null, address + 100, 8)).Rebase(0x18DD018);
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!;
        Assert.Equal(staging.ResourcePointers[address + 4], restored.ReadResourcePointer(address + 4));
        Assert.Equal(address + 100, BinaryPrimitives.ReadUInt32LittleEndian(restored.Read(address + 4, 4)));
        Assert.Equal(staging.BaseAddress, restored.BaseAddress);
    }

    [Theory]
    [InlineData(false, 16)]
    [InlineData(true, 32)]
    public void BindingPreservesActualCpuSkeletonGeometryAndUnknownLinkedFieldsAndBacksUpDefault(bool linked, int advance)
    {
        uint directory = NativeBattleFileStaging.Address;
        uint row = directory + 128;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(directory, Nested());
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, NativeBattleModelCpuResourceRow.Unknown().Set(12, new(0, 100)).Set(16, new(0, 200))).SetItem(1, NativeBattleModelCpuResourceRow.Unknown().Set(12, new(1, 300)).Set(16, new(1, 400))) };
        NativeBattleGraphicsModel model = new(0, 2) { ModelId4 = 0, Flags2 = 4, ActiveResourceRow100 = 0, OriginalResourceRow104 = 0, LinkedTextureResourceIndex = linked ? 1 : null, LinkedTransform = linked ? new(new(0, 0, 0), new(0, 0, 0)) : null, SequenceState = new() { Default2 = 6 } };
        NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(staging, cpu, model, row, directory);
        Assert.Equal(row + (uint)advance, bound.NextRow);
        Assert.Equal(row, bound.Model.ActiveStagedResourceRow100);
        Assert.Null(bound.Model.ActiveResourceRow100);
        Assert.Equal((ushort?)12, bound.Model.Flags2);
        Assert.Equal((short?)6, bound.Model.SequenceState!.Locals[7]);
        Assert.Equal((byte)1, bound.Model.SequenceState.Default2);
        Assert.Equal(new NativeBattleModelCpuReference(0, 100), bound.Staging.ReadResourcePointer(row)!.Cpu);
        Assert.Equal(new NativeBattleModelCpuReference(0, 200), bound.Staging.ReadResourcePointer(row + 4)!.Cpu);
        Assert.Equal(directory + 20, bound.Staging.ReadResourcePointer(row + 8)!.StagingAddress);
        Assert.Equal(directory + 24, bound.Staging.ReadResourcePointer(row + 12)!.StagingAddress);
        if (linked)
        {
            Assert.Equal(row + 16, bound.Model.LinkedStagedResourceRow);
            Assert.Equal(new NativeBattleModelCpuReference(1, 300), bound.Staging.ReadResourcePointer(row + 16)!.Cpu);
            Assert.Null(bound.Staging.ReadResourcePointer(row + 28));
            Assert.Throws<NotSupportedException>(() => bound.Staging.Read(row + 28, 4));
        }
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(bound.Staging))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(bound.Staging), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void StagedSourceCopyObservesEarlierOverlappingWritesInNativeFieldOrder()
    {
        uint start = NativeBattleFileStaging.Address;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().WriteResourcePointer(start, new(new(0, 10), null)).WriteResourcePointer(start + 4, new(new(0, 20), null)).WriteResourcePointer(start + 8, new(new(0, 30), null)).WriteResourcePointer(start + 12, new(new(0, 40), null));
        NativeBattleResourceRowBinding.CopyFields502270<uint, NativeBattleResourcePointer?>(start, start + 4, 0, (row, field) => staging.ReadResourcePointer(row + (uint)field * 4), (row, field, pointer) => staging = staging.WriteResourcePointer(row + (uint)field * 4, pointer));
        Assert.All(new uint[] { 4, 8, 12, 16 }, offset => Assert.Equal(new NativeBattleModelCpuReference(0, 10), staging.ReadResourcePointer(start + offset)!.Cpu));
    }

    [Theory]
    [InlineData(NativeBattleDescriptorTaskKind.Callback502F30)]
    [InlineData(NativeBattleDescriptorTaskKind.Victory503040)]
    public void ReusedCallbackClearsItsPhysicalPhaseAndRetainsUnwrittenCompletionByte(NativeBattleDescriptorTaskKind kind)
    {
        NativeBattleDescriptorState state = NativeBattleDescriptorState.Clear();
        NativeBattleDescriptorTask old = state.Tasks[1] with { Payload = new byte[] { 0x11, 0x77, 0xCC, 0x33, 0, 0, 0, 0 }.ToImmutableArray() };
        state = state with { Tasks = state.Tasks.SetItem(1, old) };
        (state, int slot) = state.AppendTask(kind, 0);
        Assert.Equal(1, slot);
        Assert.Equal((byte)0, state.Tasks[slot].Phase);
        Assert.Equal((byte)0xCC, state.Tasks[slot].Payload[2]);
        Assert.Equal((byte)0x33, state.Tasks[slot].Payload[3]);
        Assert.Equal(0, state.Tasks[slot].PacketSlot);
    }

    [Fact]
    public void DecodedPhysicalPoseSurvivesResourceRebindingWithoutResamplingTheNewAnimationDirectory()
    {
        NativeBattleDecodedPose decoded = new(new(10, 20, 30), [new(new(1, 2, 3), new(4096, 4096, 4096))], false);
        NativeBattleSequenceState state = new() { DecodedPose = decoded, LinkedDecodedPose = decoded, LinkedPose = new(0, 1, 1, 0) };
        state = NativeBattleTestSnapshots.RestoreJson<NativeBattleSequenceState>(NativeBattleTestSnapshots.Json(state))!;
        NativeBattlePoseSource differentResource = new([], []);
        Assert.Equal(NativeBattleTestSnapshots.Json(decoded), NativeBattleTestSnapshots.Json(NativeBattlePoseMatrices.ReadDecodedPose(state, differentResource)));
        Assert.Equal(NativeBattleTestSnapshots.Json(decoded), NativeBattleTestSnapshots.Json(NativeBattlePoseMatrices.ReadLinkedDecodedPose(state, differentResource)));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadDecodedPose(state with { DecodedPose = null }, differentResource));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected327_retained_party_and_staged_geometry_owners_survive_fresh_views_without_extra_resource_reads(bool staged)
    {
        ImmutableArray<NativeBattleRawMesh> geometry = [new([], [])];
        NativeBattleGraphicsResource original = new([0, 0]) { AnimationFrames = [], Geometry = geometry, LinkedGeometry = [] };
        int pointerReads = 0;
        int byteReads = 0;
        uint row = NativeBattleFileStaging.Address + 0x200;
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animation = [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0];
        NativeBattleResourcePointer skeletonPointer = new(null, row + 0x100, 64);
        NativeBattleResourcePointer animationPointer = new(null, row + 0x200, 12);
        NativeBattleResourcePointer sequencePointer = new(null, row + 0x300, 2);
        NativeBattleGraphicsResource Resolve()
        {
            return NativeBattleVictoryResources.Resolve(new(0, 2) { ActiveStagedResourceRow100 = row }, original, address =>
            {
                pointerReads++;
                return address == row ? skeletonPointer : address == row + 8 ? animationPointer : address == row + 12 ? sequencePointer : throw new InvalidOperationException("The native retained geometry path does not look up a geometry pointer.");
            }, (pointer, length) =>
            {
                byteReads++;
                byte[] bytes = ReferenceEquals(pointer, skeletonPointer) ? skeleton : ReferenceEquals(pointer, animationPointer) ? animation : ReferenceEquals(pointer, sequencePointer) ? new byte[2] : throw new InvalidOperationException();
                return bytes.AsSpan(0, length).ToArray();
            });
        }
        NativeBattleGraphicsResource first = staged ? Resolve() : original;
        NativeBattleGraphicsResource second = staged ? Resolve() : original with { };
        Assert.Null(first.PilotGeometryResource);
        Assert.NotSame(first, second);
        Assert.True(first.Geometry!.Value.Equals(second.Geometry!.Value));
        string frozen = NativeBattleTestSnapshots.Json(first);
        int pointersBefore = pointerReads;
        int bytesBefore = byteReads;
        NativeBattlePilotVisualIdentity parent = new(327, 0x21FFD68, 0, 19, 0);
        NativeBattlePilotVisualIdentity a = NativeBattleGilgameshVisuals.ActorResource(parent, first, 0)!.Value;
        NativeBattlePilotVisualIdentity b = NativeBattleGilgameshVisuals.ActorResource(parent, second, 0)!.Value;
        Assert.Equal(a, b);
        Assert.Equal(pointersBefore, pointerReads);
        Assert.Equal(bytesBefore, byteReads);
        Assert.Equal(staged ? 6 : 0, pointerReads);
        Assert.Equal(staged ? 8 : 0, byteReads);
        NativeBattleGraphicsResource replaced = second with { Geometry = geometry.ToArray().ToImmutableArray() };
        NativeBattlePilotVisualIdentity c = NativeBattleGilgameshVisuals.ActorResource(parent, replaced, 0)!.Value;
        Assert.NotEqual(a.ResourceGeneration, c.ResourceGeneration);
        Assert.Equal(NativeBattleTestSnapshots.Json(second), NativeBattleTestSnapshots.Json(replaced));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(first));
        Assert.Null(NativeBattleGilgameshVisuals.ActorResource(null, new([0, 0]), 0));
    }

    [Fact]
    public void Selected327_geometry_identity_uses_actual_CPU_owner_and_only_native_consumed_linked_storage()
    {
        NativeBattleRawMesh mesh = new([], []);
        NativeBattleModelCpuReference cpu = new(3, 128);
        NativeBattleGraphicsResource original = new([0, 0]) { AnimationFrames = [], Geometry = [mesh, mesh], LinkedGeometry = [mesh], LinkedGeometryUsesBodySkeleton = true, PilotGeometryResource = cpu };
        NativeBattlePilotVisualIdentity parent = new(327, 0x21FFD68, 0, 19, 0);
        NativeBattlePilotVisualIdentity a = NativeBattleGilgameshVisuals.ActorResource(parent, original, 0)!.Value;
        NativeBattleGraphicsResource fresh = original with { Geometry = original.Geometry!.Value.ToArray().ToImmutableArray() };
        Assert.Equal(a, NativeBattleGilgameshVisuals.ActorResource(parent, fresh, 0));
        NativeBattleGraphicsResource replaced = fresh with { LinkedGeometry = original.LinkedGeometry!.Value.ToArray().ToImmutableArray() };
        Assert.NotEqual(a.ResourceGeneration, NativeBattleGilgameshVisuals.ActorResource(parent, replaced, 0)!.Value.ResourceGeneration);
        NativeBattleGraphicsResource unused = original with { LinkedGeometryUsesBodySkeleton = false };
        Assert.Equal(NativeBattleGilgameshVisuals.ActorResource(parent, unused, 0), NativeBattleGilgameshVisuals.ActorResource(parent, unused with { LinkedGeometry = replaced.LinkedGeometry }, 0));
        Assert.Equal(NativeBattleTestSnapshots.Json(original), NativeBattleTestSnapshots.Json(replaced));
    }

    private static byte[] Nested()
    {
        byte[] bytes = new byte[32];
        int[] directory = [3, 20, 24, 28, 32];
        for (int index = 0; index < directory.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 4), directory[index]);
        }
        return bytes;
    }
}
