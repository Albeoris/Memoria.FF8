using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnemyGeometryCacheTests
{
    [Fact]
    public void EqualBytesFromIndependentArraysReuseOnlyTheLastSuccessfulImmutableGeometry()
    {
        NativeBattleEnemyGeometryCache cache = new();
        byte[] bytes = Geometry(16);
        ImmutableArray<NativeBattleRawMesh> first = cache.Read(bytes, 1);
        ImmutableArray<NativeBattleRawMesh> same = cache.Read(bytes.ToArray(), 1);
        Assert.True(first.Equals(same));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleRawGeometryReader.Read(bytes, 1))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)));
    }

    [Fact]
    public void CallerVertexAndUvMutationsInvalidateTheOwnedKeyAndPreservePriorOutputs()
    {
        NativeBattleEnemyGeometryCache cache = new();
        byte[] bytes = Geometry(16);
        ImmutableArray<NativeBattleRawMesh> first = cache.Read(bytes, 1);
        string original = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), 42);
        ImmutableArray<NativeBattleRawMesh> moved = cache.Read(bytes, 1);
        Assert.False(first.Equals(moved));
        Assert.Equal((short)42, moved[0].Vertices[0].Position.X);
        int uv = Counts(16) + 12 + 8;
        bytes[uv] = 77;
        ImmutableArray<NativeBattleRawMesh> textured = cache.Read(bytes, 1);
        Assert.False(moved.Equals(textured));
        Assert.Equal(77, textured[0].Polygons[0].Bytes[8]);
        Assert.Equal(0, moved[0].Polygons[0].Bytes[8]);
        Array.Fill(bytes, (byte)0xFF);
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)));
        Assert.Equal((short)42, moved[0].Vertices[0].Position.X);
        Assert.Equal(77, textured[0].Polygons[0].Bytes[8]);
    }

    [Fact]
    public void ChangedBoneCountAndMalformedBytesValidateWithoutPoisoningTheLastSuccessfulEntry()
    {
        NativeBattleEnemyGeometryCache cache = new();
        byte[] bytes = Geometry(1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 1);
        ImmutableArray<NativeBattleRawMesh> valid = cache.Read(bytes, 2);
        Assert.Throws<InvalidDataException>(() => cache.Read(bytes, 1));
        Assert.True(valid.Equals(cache.Read(bytes.ToArray(), 2)));
        byte[] malformed = bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(malformed, -1);
        Assert.Throws<InvalidDataException>(() => cache.Read(malformed, 2));
        Assert.True(valid.Equals(cache.Read(bytes, 2)));
    }

    [Fact]
    public void RepeatedEqualReadsDoNotReparseOrCopyTheirOwnedKey()
    {
        NativeBattleEnemyGeometryCache cache = new();
        byte[] bytes = Geometry(256);
        byte[] identical = bytes.ToArray();
        ImmutableArray<NativeBattleRawMesh> first = cache.Read(bytes, 1);
        _ = cache.Read(identical, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool same = true;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            same &= first.Equals(cache.Read(iteration % 2 == 0 ? bytes : identical, 1));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(same);
        Assert.True(allocated < 64 * 1024, $"Repeated geometry reads allocated {allocated} bytes.");
    }

    [Fact]
    public void ResolveReadsCurrentTransactionGeometryAndRejectsUnknownBytesBeforeConsultingCache()
    {
        NativeBattleEnemyGeometryCache cache = new();
        byte[] geometry = Geometry(16);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess();
        NativeBattleModelCpuResourceRow row = cpu.Rows[3].Set(12, new(3, 0)).Set(20, new(3, 64)).Set(24, new(3, 80)).Set(16, new(3, 128)).Set(44, null).Set(48, null);
        row = row with { Lengths = row.Lengths.SetItem(20, 12).SetItem(24, 2).SetItem(16, geometry.Length) };
        cpu = cpu with { Rows = cpu.Rows.SetItem(3, row) };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        byte[] animations = [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0];
        uint address = NativeBattleModelCpuResources.EnemyAddress;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(address, skeleton).Write(address + 64, animations).Write(address + 80, new byte[2]).Write(address + 128, geometry);
        NativeBattleGraphicsResource original = new([0, 0]);
        NativeBattleGraphicsResource first = NativeBattleEnemyResourceView.Resolve(cpu, source, 3, original, cache);
        string frozen = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first));
        NativeBattleGraphicsResource unrelated = NativeBattleEnemyResourceView.Resolve(cpu, source.Write(address + 1000, [1]), 3, original, cache);
        Assert.True(first.Geometry!.Value.Equals(unrelated.Geometry!.Value));
        using NativeBattleFileStagingTransaction transaction = new(source);
        NativeBattleFileStaging current = transaction.View.Write(address + 128 + 14, BitConverter.GetBytes((short)42));
        NativeBattleGraphicsResource changed = NativeBattleEnemyResourceView.Resolve(cpu, current, 3, original, cache);
        Assert.Equal((short)42, changed.Geometry!.Value[0].Vertices[0].Position.X);
        Assert.False(first.Geometry!.Value.Equals(changed.Geometry!.Value));
        current = current.MarkUnknown(address + 128 + 14, 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleEnemyResourceView.Resolve(cpu, current, 3, original, cache));
        current = current.Write(address + 128 + 14, BitConverter.GetBytes((short)42));
        NativeBattleGraphicsResource repaired = NativeBattleEnemyResourceView.Resolve(cpu, current, 3, original, cache);
        Assert.True(changed.Geometry!.Value.Equals(repaired.Geometry!.Value));
        NativeBattleFileStaging published = transaction.Freeze(current);
        Assert.Equal((short)42, BinaryPrimitives.ReadInt16LittleEndian(published.Read(address + 128 + 14, 2)));
        Assert.Equal(frozen, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)));
        Assert.Equal((short)42, BinaryPrimitives.ReadInt16LittleEndian(source.Read(address + 128 + 14, 2)));
    }

    private static int Counts(int vertices) => (14 + vertices * 6 + 3) & ~3;

    private static byte[] Geometry(int vertices)
    {
        int counts = Counts(vertices);
        byte[] bytes = new byte[counts + 28];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), (ushort)vertices);
        for (int vertex = 0; vertex < vertices; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + vertex * 6), (short)(vertex - 1));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + vertex * 6), 2);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18 + vertex * 6), 3);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(counts), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(counts + 12), 0x8000);
        return bytes;
    }
}
