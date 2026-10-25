using Memoria.FF8.Runtime;

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

    public static TelemetryCoverageManifest ManagedPayload(ushort expectedEffect)
    {
        ArgumentOutOfRangeException.ThrowIfZero(expectedEffect);
        List<TelemetryCoverageSite> sites = NativeBattleTelemetry.Sites.Select(site => new TelemetryCoverageSite(site.SiteId, site.Subsystem, site.Api, true, true, null)).ToList();
        sites.AddRange([
            new(2001, "renderer", "DrawUserPrimitives managed vertex payload", true, true, null),
            new(2002, "renderer", "Texture2D.SetData pixel payload", true, true, null),
            new(2003, "renderer", "Framebuffer GetData pixel payload", true, true, null),
            new(2004, "renderer", "Framebuffer ARGB conversion payload", true, true, null),
            new(2005, "diagnostics", "Diagnostic JSON output UTF-8 payload", true, true, null)
        ]);
        return new(NativeBattleTelemetry.InstrumentationVersion, sites);
    }

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
    private struct Counter
    {
        public long Bytes;
        public long Operations;
        public long LargestOperationBytes;
    }

    private const int KindCount = 8;
    private readonly object sync = new();
    private readonly Dictionary<int, int> enabledSites;
    private readonly int[] siteIds;
    private readonly Counter[] counters;
    private readonly bool available;

    public TelemetryTrafficCollector(TelemetryCoverageManifest coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        TelemetryCoverageSite[] sites = coverage.Sites.OrderBy(site => site.SiteId).ToArray();
        siteIds = new int[sites.Length];
        counters = new Counter[checked(sites.Length * KindCount)];
        enabledSites = new(sites.Length);
        for (int index = 0; index < sites.Length; index++)
        {
            TelemetryCoverageSite site = sites[index];
            if (index > 0 && site.SiteId == siteIds[index - 1])
            {
                throw new ArgumentException("Duplicate telemetry site identity.", nameof(coverage));
            }
            siteIds[index] = site.SiteId;
            if (site.Enabled)
            {
                enabledSites.Add(site.SiteId, index * KindCount);
            }
        }
        available = coverage.MandatoryCoverageAvailable;
    }

    public void Record(int siteId, TelemetryTrafficKind kind, long payloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
        if ((uint)kind >= KindCount || !enabledSites.TryGetValue(siteId, out int firstCounter))
        {
            throw new InvalidOperationException("Traffic must name a registered enabled site.");
        }
        lock (sync)
        {
            ref Counter counter = ref counters[firstCounter + (int)kind];
            long bytes = checked(counter.Bytes + payloadBytes);
            long operations = checked(counter.Operations + 1);
            counter.Bytes = bytes;
            counter.Operations = operations;
            counter.LargestOperationBytes = Math.Max(counter.LargestOperationBytes, payloadBytes);
        }
    }

    public TelemetryTrafficSnapshot Complete()
    {
        lock (sync)
        {
            TelemetryTrafficAggregate[] aggregates = new TelemetryTrafficAggregate[counters.Length];
            for (int site = 0; site < siteIds.Length; site++)
            {
                for (int kind = 0; kind < KindCount; kind++)
                {
                    int index = site * KindCount + kind;
                    Counter counter = counters[index];
                    aggregates[index] = new(siteIds[site], (TelemetryTrafficKind)kind, counter.Bytes, counter.Operations, counter.LargestOperationBytes);
                }
            }
            return new(aggregates, available, available ? null : "TELEMETRY_UNAVAILABLE: mandatory copy instrumentation has missing or disabled sites.");
        }
    }
}
