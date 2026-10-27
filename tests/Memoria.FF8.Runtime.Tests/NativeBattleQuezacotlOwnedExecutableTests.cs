using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlOwnedExecutableTests
{
    [Fact]
    public void Scalar_writes_retain_owner_and_capacity_with_only_actual_payload_traffic()
    {
        NativeBattleQuezacotlExecutableMemory memory = new(new byte[NativeBattleQuezacotlExecutableMemory.Length].ToImmutableArray());
        Span<byte> value = stackalloc byte[4];
        memory.Write(NativeBattleQuezacotlExecutableMemory.Address + 16, value);
        object identity = memory.Arena.Working.BackingIdentity;
        int capacity = memory.Arena.Working.Capacity;
        PayloadTraffic traffic = new();
        long allocated;
        bool sameOwner = true;
        using (NativeBattleTelemetry.BeginScope(traffic))
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(value, index);
                sameOwner &= ReferenceEquals(memory, memory.Write(NativeBattleQuezacotlExecutableMemory.Address + 16, value));
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.True(sameOwner);
        Assert.Equal(0, allocated);
        Assert.Same(identity, memory.Arena.Working.BackingIdentity);
        Assert.Equal(capacity, memory.Arena.Working.Capacity);
        Assert.Equal(4000, traffic.Copy);
        Assert.Equal(4000, traffic.Fill);
        Assert.Equal(0, traffic.Growth);
        Assert.Equal(999u, memory.ReadUInt32(NativeBattleQuezacotlExecutableMemory.Address + 16));
    }

    [Fact]
    public void Explicit_snapshots_preserve_legacy_shape_unknown_backing_provenance_and_independent_restores()
    {
        NativeBattleQuezacotlExecutableMemory memory = new(Enumerable.Repeat((byte)0xA5, NativeBattleQuezacotlExecutableMemory.Length).ToImmutableArray());
        NativeBattleQuezacotlExecutableMemory legacy = NativeBattleStorageSnapshots.Capture(memory);
        string legacyJson = JsonSerializer.Serialize(legacy);
        using (JsonDocument json = JsonDocument.Parse(legacyJson))
        {
            Assert.Equal(new[] { "Data" }, json.RootElement.EnumerateObject().Select(property => property.Name));
        }
        memory.Arena.MarkUnknown(12, 2).WriteResourcePointer(24, new(null, null) { CpuRow = 2 });
        NativeBattleQuezacotlExecutableMemory frozen = NativeBattleStorageSnapshots.Capture(memory);
        Assert.Throws<InvalidOperationException>(() => memory.Data);
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(memory));
        Assert.Throws<InvalidOperationException>(() => frozen.Write(NativeBattleQuezacotlExecutableMemory.Address, [0]));
        string jsonText = JsonSerializer.Serialize(frozen);
        Assert.Contains("UnknownRegions", jsonText);
        Assert.Contains("ResourcePointers", jsonText);
        NativeBattleQuezacotlExecutableMemory serialized = JsonSerializer.Deserialize<NativeBattleQuezacotlExecutableMemory>(jsonText)!;
        NativeBattleQuezacotlExecutableMemory first = NativeBattleStorageSnapshots.Restore(serialized);
        NativeBattleQuezacotlExecutableMemory second = NativeBattleStorageSnapshots.Restore(serialized);
        Assert.Throws<NotSupportedException>(() => first.ReadUInt16(NativeBattleQuezacotlExecutableMemory.Address + 12));
        Span<byte> bytes = stackalloc byte[2];
        Span<bool> known = stackalloc bool[2];
        first.ReadWithKnownness(NativeBattleQuezacotlExecutableMemory.Address + 12, bytes, known);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, bytes.ToArray());
        Assert.False(known[0]);
        Assert.False(known[1]);
        Assert.Equal(2, first.Arena.ReadResourcePointer(24)!.CpuRow);
        first.Write(NativeBattleQuezacotlExecutableMemory.Address, [9]);
        Assert.Equal((byte)0xA5, second.ReadByte(NativeBattleQuezacotlExecutableMemory.Address));
        Assert.Equal((byte)0xA5, frozen.Data[0]);
        Assert.Equal((byte)0xA5, legacy.Data[12]);
    }

    [Fact]
    public void Fragment_pool_and_executable_share_physical_bytes_across_explicit_graph_restore()
    {
        NativeBattleQuezacotlExecutableMemory memory = new(new byte[NativeBattleQuezacotlExecutableMemory.Length].ToImmutableArray());
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x12A53D0, 0x12A53E0, 44, 32).BindStorage(memory.Arena, NativeBattleQuezacotlExecutableMemory.Address, NativeBattleTaskStorageBinding.AdoptPhysicalState);
        pool.Slots[0].WriteDword(16, 0x12345678);
        Assert.Equal(0x12345678u, memory.ReadUInt32(0x12A53F0));
        var frozen = NativeBattleStorageSnapshots.Capture(new ExecutableGraph(memory, pool));
        ExecutableGraph first = NativeBattleStorageSnapshots.Restore(frozen);
        ExecutableGraph second = NativeBattleStorageSnapshots.Restore(frozen);
        first.Memory.Write(0x12A53F0, [9, 8, 7, 6]);
        Assert.Equal(0x06070809u, first.Pool.Slots[0].ReadDword(16));
        Assert.Equal(0x12345678u, second.Pool.Slots[0].ReadDword(16));
        Assert.Equal(0x12345678u, frozen.Pool.Slots[0].ReadDword(16));
    }

    private sealed record ExecutableGraph(NativeBattleQuezacotlExecutableMemory Memory, NativeBattleEffectTaskPool Pool);

    private sealed class PayloadTraffic : INativeBattleTelemetrySink
    {
        public long Copy;
        public long Fill;
        public long Growth;

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload)
            {
                Copy += payloadBytes;
            }
            if (siteId == 1005 && kind == NativeBattleTrafficKind.Fill)
            {
                Fill += payloadBytes;
            }
            if (siteId is 1012 or 1013)
            {
                Growth += payloadBytes;
            }
        }
    }
}
