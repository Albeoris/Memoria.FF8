using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class TelemetryTrafficCalibrationTests(ITestOutputHelper output)
{
    private sealed class Sink(TelemetryTrafficCollector collector) : INativeBattleTelemetrySink
    {
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            collector.Record(siteId, (TelemetryTrafficKind)kind, payloadBytes);
        }
    }

    [Fact]
    public void Repeated_four_byte_copy_calibration_reports_disabled_and_enabled_observer_cost()
    {
        const int operations = 250000;
        TelemetryCoverageManifest coverage = new("calibration-v1", Enumerable.Range(1, 200).Select(site => new TelemetryCoverageSite(site, "synthetic", "four-byte copy", true, true, null)).ToArray());
        byte[] source = [1, 2, 3, 4];
        byte[] destination = new byte[4];
        // Exercise both paths before measuring; alternate order to limit warmup and drift bias.
        Measure(false, 10000);
        Measure(true, 10000);
        List<object> samples = [];
        for (int sample = 0; sample < 6; sample++)
        {
            bool enabled = sample % 2 != 0;
            samples.Add(Measure(enabled, operations));
        }
        output.WriteLine(JsonSerializer.Serialize(new { metric = "synthetic_four_byte_copy_observer_calibration", clock_frequency = Stopwatch.Frequency, participating_threads = 1, samples }));

        object Measure(bool enabled, int count)
        {
            TelemetryTrafficCollector collector = new(coverage);
            using IDisposable? scope = enabled ? NativeBattleTelemetry.BeginScope(new Sink(collector)) : null;
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            for (int operation = 0; operation < count; operation++)
            {
                NativeBattleTelemetry.Copy(200, source.AsSpan(), destination);
            }
            long ticks = Stopwatch.GetTimestamp() - start;
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            Assert.Equal(source, destination);
            if (enabled)
            {
                TelemetryTrafficAggregate counter = collector.Complete().Sites.Single(site => site.SiteId == 200 && site.Kind == TelemetryTrafficKind.CpuCopyPayload);
                Assert.Equal(count, counter.Operations);
                Assert.Equal(count * 4L, counter.Bytes);
            }
            // Timing is reported, not asserted: machine load and runtime tiering are external variables.
            return new { enabled, operations = count, ticks, nanoseconds_per_copy = ticks * 1_000_000_000d / Stopwatch.Frequency / count, allocated_bytes = allocatedBytes };
        }
    }
}
