using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyResourceOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Full_load_private_clone_shared_mutation_and_release_preserve_resource_backing_aliases_and_history(bool restoredLegacy)
    {
        NativeBattleModelDatSource source = Source();
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess() with { EnemyCursor = 0, EnemyLinkedSlots = 0, ScratchMaximum = 0 };
        cpu = cpu with { Rows = cpu.Rows.SetItem(0, cpu.Rows[0] with { LinkedPhysicalIndex36 = 255 }) };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, 0, null, false);
        uint originalSkeleton = cpu.Rows[0].Require(12).SharedAddress;
        int originalLength = source.Section(9) - source.Section(0);
        Assert.True(staging.TryBorrowResource(originalSkeleton, originalLength, out ImmutableArray<byte> retained, out int sourceOffset));
        Assert.Equal(source.Bytes, retained);
        Assert.Equal(source.Section(0), sourceOffset);
        Assert.Equal(0, staging.MaterializedPageCount);
        if (restoredLegacy)
        {
            Saved restored = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(cpu, staging)));
            cpu = restored.Cpu;
            staging = restored.Staging;
        }
        (cpu, staging) = NativeBattleModelEnemyHeap.Load(cpu, staging, source, 1, 0, false);
        uint privateSkeleton = cpu.Rows[1].Require(12).SharedAddress;
        int privateLength = cpu.Rows[0].PrivateLength4!.Value;
        int sharedLength = cpu.Rows[0].SharedLength8!.Value;
        Assert.NotEqual(originalSkeleton, privateSkeleton);
        Assert.Equal(cpu.Rows[0].Require(16), cpu.Rows[1].Require(16));
        Assert.Equal(cpu.Rows[0].Require(20), cpu.Rows[1].Require(20));
        using NativeBattleStagingReadWindow privateHistory = staging.OpenReadWindow(privateSkeleton, privateLength, maximumRetainedBytes: 0);
        staging.WriteByte(privateSkeleton + 6, 0xCC);
        Assert.NotEqual(staging.ReadByte(originalSkeleton + 6), staging.ReadByte(privateSkeleton + 6));
        Assert.Equal(source.Bytes[source.Section(0) + 6], privateHistory.ReadByte(6));
        uint sharedGeometry = cpu.Rows[0].Require(16).SharedAddress;
        using NativeBattleStagingReadWindow sharedHistory = staging.OpenReadWindow(sharedGeometry, 32, maximumRetainedBytes: 0);
        staging.WriteByte(sharedGeometry + 1, 0xDD);
        Assert.Equal(0xDD, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(16), 2)[1]);
        Assert.NotEqual(0xDD, sharedHistory.ReadByte(1));
        NativeBattleModelTextureBinding binding = new(Enumerable.Repeat<byte?>(null, 16).ToImmutableArray());
        NativeBattleModelTextureCache cache = NativeBattleModelTextureCache.FromColdProcess();
        cache = cache with { Rows = cache.Rows.SetItem(0, new(3, source.ModelId, 0, binding)).SetItem(1, new(3, source.ModelId, 0, binding)) };
        int cursorBefore = cpu.EnemyCursor!.Value;
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(cpu, staging, cache, 0);
        Assert.Equal(cursorBefore - privateLength, cpu.EnemyCursor);
        Assert.Equal(0, cpu.Rows[1].Require(16).Offset);
        Assert.Equal(sharedLength, cpu.Rows[1].Require(12).Offset);
        Assert.Equal(0xCC, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(12), 7)[6]);
        Assert.Equal(0xDD, NativeBattleModelEnemyHeap.Read(cpu, staging, cpu.Rows[1].Require(16), 2)[1]);
        Assert.Equal(source.Bytes[source.Section(0) + 6], privateHistory.ReadByte(6));
        Saved after = NativeBattleTestSnapshots.RestoreJson<Saved>(NativeBattleTestSnapshots.Json(new Saved(cpu, staging)));
        cache = cache.Release5080D0(0);
        (cpu, staging) = NativeBattleModelEnemyHeap.Release(after.Cpu, after.Staging, cache, 1);
        Assert.Equal(cursorBefore - privateLength * 2 - sharedLength, cpu.EnemyCursor);
        Assert.Equal((byte?)0, cpu.EnemyLinkedSlots);
    }

    private static NativeBattleModelDatSource Source()
    {
        int[] lengths = [112, 5001, 64, 16, 16, 0, 16, 16, 16, 0];
        int[] sections = new int[lengths.Length + 1];
        sections[0] = 64;
        for (int index = 0; index < lengths.Length; index++)
        {
            sections[index + 1] = sections[index] + lengths[index];
        }
        byte[] bytes = new byte[sections[^1]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, lengths.Length);
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        for (int index = sections[0]; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 251);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sections[2] - 4), 12);
        return new(16, false, 0, 166, "model.dat", bytes.ToImmutableArray());
    }

    private sealed record Saved(NativeBattleModelCpuResources Cpu, NativeBattleFileStaging Staging);
}
