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
    public void Pool_observations_include_preexisting_rentals_growth_and_disposal_without_a_traffic_scope()
    {
        NativeBattlePoolObservation before = NativeBattleTelemetry.PoolSnapshot();
        NativeBattleEffectArenaWriter writer = new(NativeBattleEffectArena.Clear());
        writer.BeginReadThrough();
        try
        {
            NativeBattlePoolObservation initial = NativeBattleTelemetry.PoolSnapshot();
            Assert.Equal(before.OutstandingCount + 1, initial.OutstandingCount);
            Assert.True(initial.OutstandingBytes > before.OutstandingBytes);
            _ = writer.ReadThrough(0, 8192);
            NativeBattlePoolObservation grown = NativeBattleTelemetry.PoolSnapshot();
            Assert.Equal(initial.OutstandingCount, grown.OutstandingCount);
            Assert.True(grown.OutstandingBytes >= before.OutstandingBytes + 8192);
            Assert.Equal(before.RentOperations + 2, grown.RentOperations);
            Assert.Equal(before.ReturnOperations + 1, grown.ReturnOperations);
            Assert.True(grown.PeakBytes >= grown.OutstandingBytes);
        }
        finally
        {
            writer.EndReadThrough();
        }
        NativeBattlePoolObservation after = NativeBattleTelemetry.PoolSnapshot();
        Assert.Equal(before.OutstandingCount, after.OutstandingCount);
        Assert.Equal(before.OutstandingBytes, after.OutstandingBytes);
        Assert.Equal(before.ReturnOperations + 2, after.ReturnOperations);
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
        // Copy the old four-byte backing range, apply the one-byte store, read four bytes.
        Assert.Equal(9, sink.Total(NativeBattleTrafficKind.CpuCopyPayload));
        Assert.Equal(4, sink.Total(NativeBattleTrafficKind.Fill));
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
        Assert.Equal(4, sink.Total(NativeBattleTrafficKind.CpuCopyPayload));
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
