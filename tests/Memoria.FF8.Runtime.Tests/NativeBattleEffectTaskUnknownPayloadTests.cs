using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskUnknownPayloadTests
{
    [Fact]
    public void UnresolvedStackPaddingRejectsOnlyOverlappingReadsAndExactWritesResolveOnlyTheirBytes()
    {
        NativeBattleModulePoolTask task = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 48, 1).Slots[0].WriteWord(20, 0x1234).MarkPayloadUnknown(22, 2);
        Assert.Equal((ushort)0x1234, task.ReadWord(20));
        Assert.Throws<NotSupportedException>(() => task.ReadDword(20));
        Assert.Throws<NotSupportedException>(() => task.ReadBytes(20, 4));
        task = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))!;
        Assert.Throws<NotSupportedException>(() => task.ReadByte(22));
        task = task.WriteByte(22, 0x56);
        Assert.Equal((byte)0x56, task.ReadByte(22));
        Assert.Throws<NotSupportedException>(() => task.ReadByte(23));
        task = task.MarkPayloadUnknown(24, 4);
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(23, 5) }, task.UnknownPayloadRanges!.Value);
        task = task.WriteDword(24, 0x12345678);
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(23, 1) }, task.UnknownPayloadRanges!.Value);
        task = task.WriteWord(22, 0xABCD);
        Assert.Null(task.UnknownPayloadRanges);
        Assert.Equal(0xABCD1234u, task.ReadDword(20));
        Assert.DoesNotContain("UnknownPayloadRanges", JsonSerializer.Serialize(task));
        Assert.Throws<InvalidDataException>(() => task.MarkPayloadUnknown(11, 1));
        Assert.Throws<InvalidDataException>(() => task.MarkPayloadUnknown(47, 2));
    }

    [Fact]
    public void PoolAppendPreservesUnknownSuffixWhileHierarchyClearsOnlyItsActualDwordPrefix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2000, 0x1000, 100, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].MarkPayloadUnknown(20, 2).MarkPayloadUnknown(43, 3);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        (NativeBattleEffectTaskPool appended, _) = pool.Append(0x8D6B30);
        Assert.Equal(task.UnknownPayloadRanges, appended.Slots[0].UnknownPayloadRanges);
        NativeBattleEffectHierarchyAppend hierarchy = new NativeBattleEffectHierarchy([pool]).Append8DC540(0, 0x8D6B30, 45);
        task = hierarchy.Hierarchy.Read(hierarchy.Address);
        Assert.Equal(0u, task.ReadDword(20));
        Assert.Equal((byte)0, task.ReadByte(43));
        Assert.Throws<NotSupportedException>(() => task.ReadByte(44));
        Assert.Equal(new NativeBattleEffectUnknownRegion[] { new(44, 2) }, task.UnknownPayloadRanges!.Value);
    }

    [Fact]
    public void ClosedTaskTransferMovesUnknownPaddingAndKnownPointerTagsToTheSingleArenaOwner()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521920, 0x20DFAB8, 48, 1);
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null)).WriteWord(20, 0x1234).MarkPayloadUnknown(22, 2);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        Assert.True(NativeBattleEffectTaskMemory.TryRead([pool], 0x20DFACCu, 2, out byte[] xyz));
        Assert.Equal(new byte[] { 0x34, 0x12 }, xyz);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectTaskMemory.TryRead([pool], 0x20DFACCu, 4, out _));
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), null, 0) { Pools = [pool] };
        NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect);
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
        Assert.Null(task.ResourcePointers);
        Assert.Throws<NotSupportedException>(() => task.ReadResourcePointer(16));
        foreach (ImmutableArray<NativeBattleEffectUnknownRegion> ranges in new ImmutableArray<NativeBattleEffectUnknownRegion>[] { [], [new(11, 1)], [new(47, 2)], [new(20, 2), new(22, 2)], [new(24, 2), new(20, 2)] })
        {
            NativeBattleModulePoolTask malformed = task with { UnknownPayloadRanges = ranges };
            Assert.Throws<InvalidDataException>(() => (pool with { Slots = pool.Slots.SetItem(0, malformed) }).Validate());
        }
    }
}
