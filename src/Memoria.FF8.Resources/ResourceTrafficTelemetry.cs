namespace Memoria.FF8.Resources;

/// <summary>Scoped managed resource staging payload counters. Callbacks may run on concurrent resource workers.</summary>
public static class ResourceTrafficTelemetry
{
    private sealed class Scope(Action<int, long> sink, Scope? previous) : IDisposable
    {
        private int active = 1;
        internal void Record(int site, long bytes)
        {
            if (Volatile.Read(ref active) != 0)
            {
                sink(site, bytes);
            }
        }

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

    public static IDisposable BeginScope(Action<int, long> recordCopy)
    {
        ArgumentNullException.ThrowIfNull(recordCopy);
        Scope scope = new(recordCopy, Current.Value);
        Current.Value = scope;
        return scope;
    }

    public static MemoryStream CreateStagingStream(int capacity = 0)
    {
        return new StagingStream(capacity);
    }

    internal static void Record(int site, long bytes)
    {
        Current.Value?.Record(site, bytes);
    }

    internal sealed class StagingStream(int capacity = 0) : MemoryStream(capacity)
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            int capacity = Capacity;
            long length = Length;
            base.Write(buffer, offset, count);
            if (Capacity > capacity && length > 0)
            {
                Record(1501, length);
            }
            Record(1502, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // MemoryStream delegates this override to Write(byte[],...) for derived streams.
            // Calling the byte[] overload directly avoids that framework staging allocation.
            int capacity = Capacity;
            long length = Length;
            long position = Position;
            long end = checked(position + buffer.Length);
            if (end > int.MaxValue)
            {
                throw new IOException("Resource staging exceeds the managed stream extent.");
            }
            if (end > Length)
            {
                SetLength(end);
            }
            buffer.CopyTo(GetBuffer().AsSpan(checked((int)position)));
            Position = end;
            if (Capacity > capacity && length > 0)
            {
                Record(1501, length);
            }
            Record(1502, buffer.Length);
        }

        public override byte[] ToArray()
        {
            byte[] result = base.ToArray();
            Record(1503, result.LongLength);
            return result;
        }
    }
}
