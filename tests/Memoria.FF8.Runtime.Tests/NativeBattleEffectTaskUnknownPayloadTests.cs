using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskUnknownPayloadTests
{
    [Fact]
    public void KnownScalarReadsBesideUnknownSuffixDoNotAllocatePerAccess()
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1).Slots[0].WriteDword(20, 7).MarkPayloadUnknown(80, 4);
        for (int index = 0; index < 10000; index++)
        {
            _ = task.ReadDword(20);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        uint total = 0;
        for (int index = 0; index < 10000; index++)
        {
            total += task.ReadDword(20);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(70000u, total);
        Assert.True(allocated < 1024, $"Known task scalar reads allocated {allocated} bytes.");
    }

    [Fact]
    public void ScalarWritesShareTheirBankAndRetainDisjointUnknownMetadata()
    {
        NativeBattleModulePoolTask source = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1).Slots[0].MarkPayloadUnknown(80, 4);
        NativeBattleModulePoolTask bytes = source.WriteBytes(20, new byte[] { 1, 2, 3, 4 });
        NativeBattleModulePoolTask dword = bytes.WriteDword(24, 0x12345678);
        NativeBattleModulePoolTask word = dword.WriteWord(28, 0xABCD);
        NativeBattleModulePoolTask value = word.WriteByte(30, 0xEF);
        Assert.Equal(source.ReadUnknownPayloadRanges(), value.ReadUnknownPayloadRanges());
        Assert.Same(source, value);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.ReadBytes(20, 4));
        Assert.Equal(0x12345678u, bytes.ReadDword(24));
        Assert.Equal(0xABCD, dword.ReadWord(28));
        Assert.Equal(0xEF, word.ReadByte(30));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, value.ReadBytes(20, 4));
        Assert.Equal(0x12345678u, value.ReadDword(24));
        Assert.Equal(0xABCD, value.ReadWord(28));
        Assert.Equal(0xEF, value.ReadByte(30));
        Assert.Throws<NotSupportedException>(() => value.ReadByte(80));
    }

    [Fact]
    public void RepeatedDwordWritesUpdateSharedPayloadWithoutCopyingTheBank()
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 4096, 1).Slots[0].MarkPayloadUnknown(4000, 4);
        task = task.WriteDword(20, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint value = 0; value < 1000; value++)
        {
            task = task.WriteDword(20, value);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 5_000_000, $"Scalar task writes allocated {allocated} bytes.");
        Assert.Equal(999u, task.ReadDword(20));
    }

    [Fact]
    public void UnresolvedStackPaddingRejectsOnlyOverlappingReadsAndExactWritesResolveOnlyTheirBytes()
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 48, 1).Slots[0].WriteWord(20, 0x1234).MarkPayloadUnknown(22, 2);
        Assert.Equal((ushort)0x1234, task.ReadWord(20));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(20));
        Assert.Throws<NotSupportedException>(() => task.ReadBytes(20, 4));
        task = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)))!);
        Assert.Throws<NotSupportedException>(() => task.ReadByte(22));
        task = task.WriteByte(22, 0x56);
        Assert.Equal((byte)0x56, task.ReadByte(22));
        Assert.Throws<NotSupportedException>(() => task.ReadByte(23));
        task = task.MarkPayloadUnknown(24, 4);
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(23, 5) }, task.ReadUnknownPayloadRanges()!.Value);
        task = task.WriteDword(24, 0x12345678);
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(23, 1) }, task.ReadUnknownPayloadRanges()!.Value);
        task = task.WriteWord(22, 0xABCD);
        Assert.Null(task.ReadUnknownPayloadRanges());
        Assert.Equal(0xABCD1234u, task.ReadDword(20));
        Assert.DoesNotContain("UnknownPayloadRanges", JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(task)));
        Assert.Throws<InvalidDataException>(() => task.MarkPayloadUnknown(11, 1));
        Assert.Throws<InvalidDataException>(() => task.MarkPayloadUnknown(47, 2));
    }

    [Fact]
    public void PoolAppendPreservesUnknownSuffixWhileHierarchyClearsOnlyItsActualDwordPrefix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].MarkPayloadUnknown(20, 2).MarkPayloadUnknown(43, 3);
        pool = pool.WriteSlot(0, task);
        (NativeBattleEffectTaskPool appended, _) = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(pool)).Append(0x8D6B30);
        Assert.Equal(task.ReadUnknownPayloadRanges(), appended.Slots[0].ReadUnknownPayloadRanges());
        NativeBattleEffectHierarchyAppend hierarchy = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x8D6B30, 45);
        task = hierarchy.Hierarchy.Read(hierarchy.Address);
        Assert.Equal(0u, task.ReadDword(20));
        Assert.Equal((byte)0, task.ReadByte(43));
        Assert.Throws<NotSupportedException>(() => task.ReadByte(44));
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(44, 2) }, task.ReadUnknownPayloadRanges()!.Value);
    }

    [Fact]
    public void ClosedTaskTransferMovesUnknownPaddingAndKnownPointerTagsToTheSingleArenaOwner()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521920, 0x20DFAB8, 48, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null)).WriteWord(20, 0x1234).MarkPayloadUnknown(22, 2);
        pool = pool.WriteSlot(0, task);
        Assert.True(NativeBattleEffectTaskMemory.TryRead([pool], 0x20DFACCu, 2, out byte[] xyz));
        Assert.Equal(new byte[] { 0x34, 0x12 }, xyz);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTaskMemory.TryRead([pool], 0x20DFACCu, 4, out _));
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), null, 0) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect.Pools);
        NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(memory, effect);
        Assert.Empty(transferred.Effect.Pools);
        Assert.Equal(new byte[] { 0x34, 0x12 }, transferred.Memory.Arena!.Read(20, 2));
        Assert.Throws<NotSupportedException>(() => transferred.Memory.Arena.Read(20, 4));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), transferred.Memory.Arena.ReadResourcePointer(16)!.Cpu);
        Assert.Equal(0xAABB1234u, BitConverter.ToUInt32(transferred.Memory.Arena.Write(22, new byte[] { 0xBB, 0xAA }).Read(20, 4)));
    }

    [Fact]
    public void UnknownPointerStoresInvalidateTheTagAndMalformedRangeListsFailValidation()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 48, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null)).MarkPayloadUnknown(17, 1);
        Assert.Null(NativeBattleStorageSnapshots.Capture(task).ResourcePointers);
        Assert.Throws<NotSupportedException>(() => task.ReadResourcePointer(16));
        foreach (ImmutableArray<NativeBattleEffectUnknownRegion> ranges in new ImmutableArray<NativeBattleEffectUnknownRegion>[] { [], [new(11, 1)], [new(47, 2)], [new(20, 2), new(22, 2)], [new(24, 2), new(20, 2)] })
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleTaskFixture.EditTask(task, data => data with { Unknown = ranges }));
        }
    }
}
