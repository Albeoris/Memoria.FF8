namespace Memoria.FF8.BattleEffects;

public enum TelemetryTrafficKind
{
    CpuCopyPayload,
    LogicalMemoryCopy,
    ZeroFill,
    Fill,
    GpuUpload,
    GpuReadback,
    GpuCopy,
    DiagnosticOutput
}

public sealed record TelemetryCoverageSite(int SiteId, string Subsystem, string Api, bool Mandatory, bool Enabled, string? BlindSpot);
public sealed record TelemetryCoverageManifest(string InstrumentationVersion, IReadOnlyList<TelemetryCoverageSite> Sites)
{
    public bool MandatoryCoverageAvailable => Sites.Count > 0 && Sites.Any(site => site.Mandatory) && Sites.All(site => !site.Mandatory || (site.Enabled && site.BlindSpot is null)) && Sites.Select(site => site.SiteId).Distinct().Count() == Sites.Count;

    public static TelemetryCoverageManifest PreparationOnly { get; } = new("preparation-v1", new TelemetryCoverageSite[]
    {
        new(1, "arena", "NativeBattleEffectArena reads/writes/snapshots/overlap/unknown bytes", true, false, "Runtime hooks unavailable."),
        new(2, "arena_writer", "NativeBattleEffectArenaWriter rental/buffer/undo/materialization/publication", true, false, "Runtime hooks unavailable."),
        new(3, "retained_memory", "NativeKnownByteRanges and NativeBattleFileStaging.CopyMemory", true, false, "Runtime hooks unavailable."),
        new(4, "resources", "Resource loads and immutable materialization bypasses", true, false, "Runtime hooks unavailable."),
        new(5, "renderer", "Vertex/index/texture uploads and framebuffer conversions/copies", true, false, "Renderer hooks unavailable."),
        new(6, "diagnostics", "Snapshot/capture/serialization output", true, false, "Host hooks unavailable.")
    });
}

public readonly record struct TelemetryTrafficAggregate(int SiteId, TelemetryTrafficKind Kind, long Bytes, long Operations, long LargestOperationBytes);
public sealed record TelemetryTrafficSnapshot(IReadOnlyList<TelemetryTrafficAggregate> Sites, bool Available, string? Error)
{
    public long? CpuCopyPayloadBytes => Available ? Sites.Where(site => site.Kind == TelemetryTrafficKind.CpuCopyPayload).Sum(site => site.Bytes) : null;
    public bool RequiresLargeCopyReview => CpuCopyPayloadBytes >= 1024L * 1024 * 1024;
}

/// <summary>Window-wide payload counters; callers choose disjoint windows. Execution includes cleanup; its phase children must never be added again. No battle-state access or stack collection.</summary>
public sealed class TelemetryTrafficCollector
{
    private readonly object sync = new();
    private readonly TelemetryCoverageManifest coverage;
    private readonly Dictionary<(int Site, TelemetryTrafficKind Kind), TelemetryTrafficAggregate> aggregates = [];

    public TelemetryTrafficCollector(TelemetryCoverageManifest coverage)
    {
        this.coverage = coverage;
        foreach (TelemetryCoverageSite site in coverage.Sites)
        {
            foreach (TelemetryTrafficKind kind in Enum.GetValues<TelemetryTrafficKind>())
            {
                if (!aggregates.TryAdd((site.SiteId, kind), new(site.SiteId, kind, 0, 0, 0)))
                {
                    throw new ArgumentException("Duplicate telemetry site identity.", nameof(coverage));
                }
            }
        }
    }

    public void Record(int siteId, TelemetryTrafficKind kind, long payloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        lock (sync)
        {
            if (!aggregates.TryGetValue((siteId, kind), out TelemetryTrafficAggregate previous) || !coverage.Sites.Any(site => site.SiteId == siteId && site.Enabled))
            {
                throw new InvalidOperationException("Traffic must name a registered enabled site.");
            }
            aggregates[(siteId, kind)] = previous with { Bytes = checked(previous.Bytes + payloadBytes), Operations = checked(previous.Operations + 1), LargestOperationBytes = Math.Max(previous.LargestOperationBytes, payloadBytes) };
        }
    }

    public TelemetryTrafficSnapshot Complete()
    {
        lock (sync)
        {
            bool available = coverage.MandatoryCoverageAvailable;
            return new(aggregates.Values.OrderBy(value => value.SiteId).ThenBy(value => value.Kind).ToArray(), available, available ? null : "TELEMETRY_UNAVAILABLE: mandatory copy instrumentation has missing or disabled sites.");
        }
    }
}
