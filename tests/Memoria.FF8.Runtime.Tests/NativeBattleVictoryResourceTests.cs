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
        NativeBattleFileStaging restored = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
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
        NativeBattleFileStaging restored = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(bound.Staging))!;
        Assert.Equal(JsonSerializer.Serialize(bound.Staging), JsonSerializer.Serialize(restored));
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
        state = JsonSerializer.Deserialize<NativeBattleSequenceState>(JsonSerializer.Serialize(state))!;
        NativeBattlePoseSource differentResource = new([], []);
        Assert.Equal(JsonSerializer.Serialize(decoded), JsonSerializer.Serialize(NativeBattlePoseMatrices.ReadDecodedPose(state, differentResource)));
        Assert.Equal(JsonSerializer.Serialize(decoded), JsonSerializer.Serialize(NativeBattlePoseMatrices.ReadLinkedDecodedPose(state, differentResource)));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadDecodedPose(state with { DecodedPose = null }, differentResource));
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
