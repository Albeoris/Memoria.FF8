using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaTests
{
    [Fact]
    public void Empty_pointer_check_distinguishes_absence_explicit_null_and_deferred_public_enumeration()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        Assert.False(arena.MayHaveResourcePointers());
        Assert.False(NativeBattleStorageSnapshots.Capture(arena).MayHaveResourcePointers());
        IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> deferred = arena.EnumerateResourcePointers();

        arena.WriteResourcePointer(4, null);

        Assert.True(arena.MayHaveResourcePointers());
        Assert.Null(Assert.Single(deferred).Value);
        Assert.True(NativeBattleStorageSnapshots.Capture(arena).MayHaveResourcePointers());
        arena.Write(5, [7]);
        Assert.False(arena.MayHaveResourcePointers());
    }

    [Fact]
    public void Pointer_check_is_conservative_for_transaction_routes_and_tracks_last_pointer_rollback()
    {
        NativeBattleEffectArena owner = NativeBattleEffectArena.Clear().WriteResourcePointer(36, new(null, 0x12345678));
        NativeBattleEffectArena window = NativeBattleEffectArena.BorrowWorking(owner.Working, 32, 16);
        using (NativeBattleEffectArenaTransaction transaction = new(window))
        {
            Assert.True(window.MayHaveResourcePointers());
            transaction.BeginCheckpoint();
            window.Write(4, [9, 8, 7, 6]);
            Assert.Empty(window.EnumerateResourcePointers());
            Assert.True(window.MayHaveResourcePointers());
            Assert.True(transaction.View.MayHaveResourcePointers());
            window.WriteResourcePointer(8, null);
            Assert.Single(window.EnumerateResourcePointers());
            transaction.RollbackCheckpoint();
            Assert.Equal(0x12345678u, window.ReadUInt32(4));
            Assert.Single(window.EnumerateResourcePointers());
        }
        owner.Working.BeginCheckpoint();
        owner.Write(36, [9, 8, 7, 6]);
        Assert.False(owner.MayHaveResourcePointers());
        owner.Working.RollbackCheckpoint();
        Assert.True(owner.MayHaveResourcePointers());
        Assert.True(window.MayHaveResourcePointers());
    }

    [Fact]
    public void Empty_import_hydrates_one_fresh_known_zero_owner_and_keeps_snapshot_and_rollback_contracts()
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena second = new([]) { ResourcePointers = ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty };
        Assert.NotSame(first.Working, second.Working);
        Assert.Same(first.Working, first.Working);
        Assert.Equal(0, first.Working.StoredLength);
        Assert.True(first.Working.InitialKnownZero);
        Assert.Equal(new byte[8], first.Read(NativeBattleEffectArena.Length - 8, 8));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second)));
        NativeBattleEffectArena frozen = NativeBattleStorageSnapshots.Capture(first);
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = frozen.Working;
        });
        first.AdmitRetained(new());
        using (NativeBattleTaskOperation operation = new([]))
        {
            first.Write(4, [1, 2, 3, 4]);
            Assert.Throws<InvalidOperationException>(() => first.Capture());
        }
        Assert.Equal(new byte[4], first.Read(4, 4));
        Assert.Equal(new byte[4], second.Read(4, 4));
        Assert.False(first.Working.HasCheckpoint);
    }

    [Fact]
    public void Empty_import_fast_path_retains_strict_metadata_validation_and_nonempty_restore()
    {
        NativeBattleEffectArena invalid = new(default(ImmutableArray<NativeBattleEffectMemoryRegion>));
        Assert.Throws<InvalidDataException>(() => invalid.Read(0, 1));
        NativeBattleEffectArena invalidFiles = new([]) { TextureFiles = default(ImmutableArray<NativeBattleTextureFileSource>) };
        Assert.Throws<InvalidDataException>(() => invalidFiles.Read(0, 1));
        NativeBattleEffectArena explicitFiles = new([]) { TextureFiles = ImmutableArray<NativeBattleTextureFileSource>.Empty };
        Assert.Equal((byte)0, explicitFiles.Read(0, 1)[0]);
        Assert.True(explicitFiles.TextureFiles.HasValue);
        Assert.Empty(explicitFiles.TextureFiles.Value);
        NativeBattleEffectArena nonempty = new([new(4, [1, 2, 3, 4])]) { UnknownRegions = [new(5, 1)] };
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, nonempty.ReadSnapshot(4, 4).Data);
        Assert.Equal(new[] { true, false, true, true }, nonempty.ReadSnapshot(4, 4).Known);
    }

    [Fact]
    public void Empty_hydration_preserves_zero_byte_fill_telemetry_and_a_throwing_sink_can_retry()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        HydrationSink sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            Assert.Throws<InvalidOperationException>(() => arena.Read(0, 1));
        }
        Assert.Equal(1, sink.Fills);
        Assert.Equal(new byte[1], arena.Read(0, 1));
        Assert.Equal(0, arena.Working.StoredLength);
    }

    private sealed class HydrationSink : INativeBattleTelemetrySink
    {
        internal int Fills { get; private set; }
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1013 && kind == NativeBattleTrafficKind.Fill)
            {
                Assert.Equal(0, payloadBytes);
                Fills++;
                throw new InvalidOperationException("Hydration telemetry failure.");
            }
        }
    }

    [Fact]
    public void Successful_validation_keeps_record_equality_and_rejects_invalid_clones_independently()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(10, [1, 2, 3]);
        NativeBattleEffectArena equal = source with { };
        int hash = source.GetHashCode();
        string serialized = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source));
        source.Validate();
        source.Validate();
        Assert.Equal(equal, source);
        Assert.Equal(hash, source.GetHashCode());
        Assert.Equal(serialized, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(source)));
        NativeBattleEffectArena invalid = new([new(-1, [1])]);
        Assert.Throws<InvalidDataException>(() => invalid.Validate());
        Assert.Throws<InvalidDataException>(() => invalid.Validate());
        NativeBattleEffectArena unknown = new([]) { UnknownRegions = [new(10, 1), new(11, 1)] };
        Assert.Throws<InvalidDataException>(() => unknown.Read(0, 1));
    }

    [Fact]
    public void OriginalClearProvesAllUnwrittenBytesZeroAndOverlappingWritesKeepOneOwner()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        Assert.All(arena.Read(0xFFFF0, 16), value => Assert.Equal((byte)0, value));
        arena = arena.Write(10, [1, 2, 3]).Write(12, [8, 9]).Write(8, [4]);
        Assert.Equal(new byte[] { 0, 4, 0, 1, 2, 8, 9, 0 }, arena.Read(7, 8));
        Assert.Equal(2, NativeBattleStorageSnapshots.Capture(arena).Regions.Length);
        Assert.Equal(10, NativeBattleStorageSnapshots.Capture(arena).Regions[1].Offset);
        Assert.Equal(new byte[] { 1, 2, 8, 9 }, NativeBattleStorageSnapshots.Capture(arena).Regions[1].Data);
        arena.Validate();
    }

    [Fact]
    public void ArenaSnapshotRestoresWrittenRegionsWithoutStoringInventedUnknownBytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x30000, [5, 6, 7]);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(arena));
        NativeBattleEffectArena restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(json)!);
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Equal(arena.Read(0x2FFFF, 5), restored.Read(0x2FFFF, 5));
        Assert.Throws<JsonException>(() => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>("{}")));
    }

    [Fact]
    public void ArenaDoesNotSpillAcrossTheNativeAllocationBoundary()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        Assert.Throws<InvalidDataException>(() => arena.Read(-1, 1));
        Assert.Throws<InvalidDataException>(() => arena.Write(0xFFFFF, [1, 2]));
        Assert.Throws<InvalidDataException>(() => arena.Read(0, int.MaxValue));
    }

    [Fact]
    public void Writes_own_their_private_buffer_and_keep_prior_snapshots_immutable()
    {
        byte[] bytes = [1, 2, 3, 4];
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear().Write(0x100, bytes);
        bytes[0] = 99;
        NativeBattleEffectArena preserved = NativeBattleStorageSnapshots.Capture(first);
        NativeBattleEffectArena second = first.Write(0x101, [8, 9]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, preserved.Read(0x100, 4));
        Assert.Same(first, second);
        Assert.Equal(new byte[] { 1, 8, 9, 4 }, second.Read(0x100, 4));
        byte[] exposedRead = second.Read(0x100, 4);
        exposedRead[0] = 77;
        Assert.Equal(new byte[] { 1, 8, 9, 4 }, second.Read(0x100, 4));
    }
}
