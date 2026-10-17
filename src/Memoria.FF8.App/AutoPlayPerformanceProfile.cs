using System.Diagnostics;
using System.Text.Json;

namespace Memoria.FF8;

internal sealed class AutoPlayPerformanceProfile
{
    private readonly string? destination = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUTOPLAY_PROFILE");
    private readonly Dictionary<string, Measurement> measurements = new(StringComparer.Ordinal);
    private readonly long started = Stopwatch.GetTimestamp();

    internal Scope Measure(string name)
    {
        return string.IsNullOrWhiteSpace(destination) ? default : new(this, name);
    }

    internal void Count(string name)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }
        if (!measurements.TryGetValue(name, out Measurement? measurement))
        {
            measurement = new();
            measurements.Add(name, measurement);
        }
        measurement.Calls++;
    }

    internal void Write()
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }
        string path = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var report = new
        {
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            MetricsAreInclusiveAndMayOverlap = true,
            AllocationsCoverTheCallingThreadOnly = true,
            Metrics = measurements.OrderByDescending(pair => pair.Value.Ticks).ToDictionary(pair => pair.Key, pair => new
            {
                pair.Value.Calls,
                Milliseconds = pair.Value.Ticks * 1000d / Stopwatch.Frequency,
                pair.Value.AllocatedBytes
            })
        };
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
    }

    private sealed class Measurement
    {
        internal long Calls;
        internal long Ticks;
        internal long AllocatedBytes;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly AutoPlayPerformanceProfile? owner;
        private readonly string? name;
        private readonly long timestamp;
        private readonly long allocated;

        internal Scope(AutoPlayPerformanceProfile owner, string name)
        {
            this.owner = owner;
            this.name = name;
            allocated = GC.GetAllocatedBytesForCurrentThread();
            timestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (owner is null)
            {
                return;
            }
            long elapsed = Stopwatch.GetTimestamp() - timestamp;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (!owner.measurements.TryGetValue(name!, out Measurement? measurement))
            {
                measurement = new();
                owner.measurements.Add(name!, measurement);
            }
            measurement.Calls++;
            measurement.Ticks += elapsed;
            measurement.AllocatedBytes += bytes;
        }
    }
}
