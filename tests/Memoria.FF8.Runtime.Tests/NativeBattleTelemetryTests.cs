namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTelemetryTests
{
    private sealed class Sink : INativeBattleTelemetrySink
    {
        private readonly object sync = new();
        public Dictionary<(int Site, NativeBattleTrafficKind Kind), long> Bytes { get; } = [];

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            lock (sync)
            {
                Bytes[(siteId, kind)] = checked(Bytes.GetValueOrDefault((siteId, kind)) + payloadBytes);
            }
        }

        public long Total(NativeBattleTrafficKind kind) => Bytes.Where(pair => pair.Key.Kind == kind).Sum(pair => pair.Value);
    }

    [Fact]
    public void Private_writer_journal_does_not_claim_ArrayPool_rentals_and_publishes_to_the_same_owner()
    {
        NativeBattlePoolObservation before = NativeBattleTelemetry.PoolSnapshot();
        NativeBattleEffectArena owner = NativeBattleEffectArena.Clear();
        using (NativeBattleEffectArenaWriter writer = new(owner))
        {
            writer.BeginReadThrough();
            writer.Write(8191, [7]);
            Assert.Equal((byte)0, owner.ReadByte(8191));
            Assert.Equal((byte)7, writer.ReadThrough(0, 8192)[8191]);
            Assert.Equal(before, NativeBattleTelemetry.PoolSnapshot());
            Assert.Same(owner, writer.Freeze());
            Assert.Equal((byte)7, owner.ReadByte(8191));
        }
        Assert.Equal(before, NativeBattleTelemetry.PoolSnapshot());
        using (NativeBattleEffectArenaWriter reused = new(owner))
        {
            reused.Write(8191, [9]);
            Assert.Equal((byte)9, reused.ReadThrough(8191, 1)[0]);
            Assert.Equal((byte)7, owner.ReadByte(8191));
        }
        Assert.Equal((byte)7, owner.ReadByte(8191));
        Assert.Equal(before, NativeBattleTelemetry.PoolSnapshot());
    }

    [Fact]
    public void Arena_counts_actual_backing_copies_and_known_map_fills()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(10, [1, 2, 3, 4]);
        Sink sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            source = source.Write(11, [9]);
            var snapshot = source.ReadSnapshot(10, 4);
            Assert.Equal(new byte[] { 1, 9, 3, 4 }, snapshot.Data);
            Assert.All(snapshot.Known, value => Assert.True(value));
        }
        // One byte is stored; the snapshot reads four data bytes and four knownness bytes.
        Assert.Equal(9, sink.Total(NativeBattleTrafficKind.CpuCopyPayload));
        Assert.Equal(1, sink.Total(NativeBattleTrafficKind.Fill));
        Assert.Equal(0, sink.Total(NativeBattleTrafficKind.LogicalMemoryCopy));
    }

    [Fact]
    public void Physical_overlap_copy_and_fills_have_separate_payload_units()
    {
        Sink sink = new();
        byte[] bytes = [1, 2, 3, 4];
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            NativeBattleTelemetry.Copy(9001, bytes.AsSpan(0, 3), bytes.AsSpan(1));
            NativeBattleTelemetry.Clear(9002, bytes, 0, 1);
            NativeBattleTelemetry.Fill(9003, bytes, (byte)7, 2, 2);
            _ = NativeBattleTelemetry.Materialize(9004, bytes.AsSpan());
        }
        Assert.Equal(new byte[] { 0, 1, 7, 7 }, bytes);
        Assert.Equal(7, sink.Total(NativeBattleTrafficKind.CpuCopyPayload));
        Assert.Equal(1, sink.Total(NativeBattleTrafficKind.ZeroFill));
        Assert.Equal(2, sink.Total(NativeBattleTrafficKind.Fill));
    }

    [Fact]
    public void Staging_native_copy_volume_is_independent_of_implementation_copies()
    {
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, [1, 2, 3, 4]);
        Sink sink = new();
        using (NativeBattleTelemetry.BeginScope(sink))
        {
            source = source.CopyMemory(NativeBattleFileStaging.Address, NativeBattleFileStaging.Address + 10, 4);
        }
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Read(NativeBattleFileStaging.Address + 10, 4));
        Assert.Equal(4, sink.Total(NativeBattleTrafficKind.LogicalMemoryCopy));
        // The four-byte native copy reads data+knownness, stages four bytes, journals
        // and stores four byte/knownness pairs, and grows the existing 32-lane page.
        Assert.Equal(8, sink.Bytes[(1002, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal(4, sink.Bytes[(1035, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal(16, sink.Bytes[(1005, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal(64, sink.Bytes[(1012, NativeBattleTrafficKind.CpuCopyPayload)]);
        Assert.Equal(92, sink.Total(NativeBattleTrafficKind.CpuCopyPayload));
    }

    [Fact]
    public async Task Async_workers_inherit_scopes_without_cross_host_or_late_worker_contamination()
    {
        Sink first = new();
        Sink second = new();
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task late;
        using (NativeBattleTelemetry.BeginScope(first))
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => NativeBattleTelemetry.Record(9001, NativeBattleTrafficKind.CpuCopyPayload, 3), TestContext.Current.CancellationToken)));
            using (NativeBattleTelemetry.BeginScope(second))
            {
                await Task.Run(() => NativeBattleTelemetry.Record(9001, NativeBattleTrafficKind.CpuCopyPayload, 5), TestContext.Current.CancellationToken);
            }
            NativeBattleTelemetry.Record(9001, NativeBattleTrafficKind.CpuCopyPayload, 1);
            late = Task.Run(async () =>
            {
                await release.Task;
                NativeBattleTelemetry.Record(9001, NativeBattleTrafficKind.CpuCopyPayload, 99);
            }, TestContext.Current.CancellationToken);
        }
        release.SetResult();
        await late;
        NativeBattleTelemetry.Record(9001, NativeBattleTrafficKind.CpuCopyPayload, 99);
        Assert.Equal(25, first.Total(NativeBattleTrafficKind.CpuCopyPayload));
        Assert.Equal(5, second.Total(NativeBattleTrafficKind.CpuCopyPayload));
    }
}
