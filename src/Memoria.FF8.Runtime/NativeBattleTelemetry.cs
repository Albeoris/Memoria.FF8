using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

public enum NativeBattleTrafficKind
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

public interface INativeBattleTelemetrySink
{
    void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes);
}

public sealed record NativeBattleTelemetrySite(int SiteId, string Subsystem, string Api);
public readonly record struct NativeBattlePoolObservation(long OutstandingCount, long OutstandingBytes, long PeakCount, long PeakBytes, long RentOperations, long ReturnOperations);

/// <summary>Opt-in payload instrumentation. A scope follows execution context into asynchronous loads, never another host's context.</summary>
public static partial class NativeBattleTelemetry
{
    private sealed class Scope(INativeBattleTelemetrySink sink, Scope? previous) : IDisposable
    {
        private int active = 1;
        internal INativeBattleTelemetrySink Sink { get; } = sink;
        internal bool Active => Volatile.Read(ref active) != 0;

        public void Dispose()
        {
            Interlocked.Exchange(ref active, 0);
            if (ReferenceEquals(Current.Value, this))
            {
                Current.Value = previous;
            }
        }
    }

    private static readonly AsyncLocal<Scope?> Current = new();
    private static readonly object PoolSync = new();
    private static NativeBattlePoolObservation pool;
    public const string InstrumentationVersion = "shared-payload-v3";

    /// <summary>Process-wide arena-writer rentals only; includes rentals predating a scored scope. Peaks are lifetime observations.</summary>
    public static NativeBattlePoolObservation PoolSnapshot()
    {
        lock (PoolSync)
        {
            return pool;
        }
    }

    internal static void ObservePoolRent(int actualCapacity)
    {
        lock (PoolSync)
        {
            long count = checked(pool.OutstandingCount + 1);
            long bytes = checked(pool.OutstandingBytes + actualCapacity);
            pool = pool with { OutstandingCount = count, OutstandingBytes = bytes, PeakCount = Math.Max(pool.PeakCount, count), PeakBytes = Math.Max(pool.PeakBytes, bytes), RentOperations = checked(pool.RentOperations + 1) };
        }
    }

    internal static void ObservePoolReturn(int actualCapacity)
    {
        lock (PoolSync)
        {
            long count = checked(pool.OutstandingCount - 1);
            long bytes = checked(pool.OutstandingBytes - actualCapacity);
            if (count < 0 || bytes < 0)
            {
                throw new InvalidOperationException("Arena telemetry observed an unowned pooled return.");
            }
            pool = pool with { OutstandingCount = count, OutstandingBytes = bytes, ReturnOperations = checked(pool.ReturnOperations + 1) };
        }
    }

    public static IDisposable BeginScope(INativeBattleTelemetrySink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        Scope scope = new(sink, Current.Value);
        Current.Value = scope;
        return scope;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
    {
        if (Current.Value is { Active: true } scope)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(payloadBytes);
            scope.Sink.Record(siteId, kind, payloadBytes);
        }
    }

    public static void Copy<T>(int siteId, ReadOnlySpan<T> source, Span<T> destination) where T : unmanaged
    {
        source.CopyTo(destination);
        Record(siteId, NativeBattleTrafficKind.CpuCopyPayload, checked((long)source.Length * Unsafe.SizeOf<T>()));
    }

    public static void Copy<T>(int siteId, Span<T> source, Span<T> destination) where T : unmanaged
    {
        Copy(siteId, (ReadOnlySpan<T>)source, destination);
    }

    public static T[] Materialize<T>(int siteId, ReadOnlySpan<T> source) where T : unmanaged
    {
        T[] result = source.ToArray();
        Record(siteId, NativeBattleTrafficKind.CpuCopyPayload, checked((long)source.Length * Unsafe.SizeOf<T>()));
        return result;
    }

    public static T[] Materialize<T>(int siteId, Span<T> source) where T : unmanaged
    {
        return Materialize(siteId, (ReadOnlySpan<T>)source);
    }

    public static T[] Materialize<T>(int siteId, ImmutableArray<T> source) where T : unmanaged
    {
        return Materialize(siteId, source.AsSpan());
    }

    public static T[] Materialize<T>(int siteId, T[] source) where T : unmanaged
    {
        return Materialize(siteId, (ReadOnlySpan<T>)source);
    }

    public static ImmutableArray<byte> Slice(int siteId, ImmutableArray<byte> source, int start, int length)
    {
        ImmutableArray<byte> result = source.Slice(start, length);
        // A complete slice retains the same array; a partial immutable slice materializes its payload.
        if (start != 0 || length != source.Length)
        {
            Record(siteId, NativeBattleTrafficKind.CpuCopyPayload, length);
        }
        return result;
    }

    public static ImmutableArray<T> Immutable<T>(int siteId, ReadOnlySpan<T> source) where T : unmanaged
    {
        ImmutableArray<T> result = ImmutableArray.Create(source);
        Record(siteId, NativeBattleTrafficKind.CpuCopyPayload, checked((long)source.Length * Unsafe.SizeOf<T>()));
        return result;
    }

    public static ImmutableArray<T> Immutable<T>(int siteId, Span<T> source) where T : unmanaged
    {
        return Immutable(siteId, (ReadOnlySpan<T>)source);
    }

    public static ImmutableArray<T> Immutable<T>(int siteId, T[] source) where T : unmanaged
    {
        return Immutable(siteId, (ReadOnlySpan<T>)source);
    }

    public static ImmutableArray<T> Immutable<T>(int siteId, ImmutableArray<T> source) where T : unmanaged
    {
        return source;
    }

    public static ImmutableArray<T> Add<T>(int siteId, ImmutableArray<T> source, T value) where T : unmanaged
    {
        ImmutableArray<T> result = source.Add(value);
        Record(siteId, NativeBattleTrafficKind.CpuCopyPayload, checked((long)source.Length * Unsafe.SizeOf<T>()));
        return result;
    }

    public static void Clear<T>(int siteId, T[] array, int start, int length) where T : unmanaged
    {
        Array.Clear(array, start, length);
        Record(siteId, NativeBattleTrafficKind.ZeroFill, checked((long)length * Unsafe.SizeOf<T>()));
    }

    public static void Fill<T>(int siteId, T[] array, T value, int start, int length) where T : unmanaged
    {
        Array.Fill(array, value, start, length);
        Record(siteId, NativeBattleTrafficKind.Fill, checked((long)length * Unsafe.SizeOf<T>()));
    }
}
