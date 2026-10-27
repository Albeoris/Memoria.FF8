using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>One node traversal's ctor observation and bounded unpublished stores.</summary>
internal sealed class NativeBattleNodeBankOperation : IDisposable, INativeBattleReadWindowSource
{
    private readonly record struct Cell(byte Data, bool Known, bool Dirty);
    private readonly NativeBattleNodeSource source;
    private readonly NativeBattleReadWindow baseline;
    private readonly NativeBattleEffectArenaTransaction publication;
    private readonly NativeBattleStagingDeltaMap<Cell> cells = new(1005);
    private readonly List<int> dirty = [];
    private bool disposed;

    internal NativeBattleNodeBankOperation(NativeBattleNodeSource source)
    {
        this.source = source;
        baseline = source.Arena.OpenReadWindow(0, source.Length);
        try
        {
            publication = NativeBattleEffectArenaTransaction.CreatePrivateChild(source.Arena);
        }
        catch
        {
            baseline.Dispose();
            throw;
        }
    }

    internal int Length => source.Length;
    internal NativeBattleNodeSource Source => source;

    internal byte ReadByte(int offset)
    {
        RequireRange(offset, 1);
        if (cells.TryGetValue((uint)offset, out Cell cell))
        {
            if (!cell.Known)
            {
                throw new NotSupportedException("Native node read intersects unresolved physical bytes.");
            }
            return cell.Data;
        }
        return baseline.ReadByte(offset);
    }

    internal ushort ReadUInt16(int offset)
    {
        RequireRange(offset, 2);
        return (ushort)(ReadByte(offset) | ReadByte(offset + 1) << 8);
    }

    internal uint ReadUInt32(int offset)
    {
        RequireRange(offset, 4);
        return ReadUInt16(offset) | (uint)ReadUInt16(offset + 2) << 16;
    }

    internal void ReadWithKnownness(int offset, Span<byte> bytes, Span<bool> known)
    {
        RequireRange(offset, bytes.Length);
        if (bytes.Length != known.Length)
        {
            throw new ArgumentException("Native node bytes and knownness destinations must match.", nameof(known));
        }
        baseline.ReadWithKnownness(offset, bytes, known);
        for (int index = 0; index < bytes.Length; index++)
        {
            if (cells.TryGetValue((uint)(offset + index), out Cell cell))
            {
                bytes[index] = cell.Data;
                known[index] = cell.Known;
            }
        }
    }

    internal void ReadInto(int offset, Span<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        Span<byte> scratch = stackalloc byte[256];
        Span<bool> known = stackalloc bool[256];
        for (int cursor = 0; cursor < bytes.Length;)
        {
            int count = Math.Min(scratch.Length, bytes.Length - cursor);
            ReadWithKnownness(offset + cursor, scratch[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                throw new NotSupportedException("Native node read intersects unresolved physical bytes.");
            }
            cursor += count;
        }
        for (int cursor = 0; cursor < bytes.Length;)
        {
            int count = Math.Min(scratch.Length, bytes.Length - cursor);
            ReadWithKnownness(offset + cursor, bytes.Slice(cursor, count), known[..count]);
            cursor += count;
        }
    }

    internal void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        cells.EnsureCapacity(checked(cells.Count + bytes.Length));
        dirty.EnsureCapacity(checked(dirty.Count + bytes.Length));
        source.Arena.Working.ObserveReadWindowWrite(this, checked(source.Arena.WorkingOffset + offset), bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            int target = offset + index;
            if (!cells.TryGetValue((uint)target, out Cell previous) || !previous.Dirty)
            {
                dirty.Add(target);
            }
            cells[(uint)target] = new(bytes[index], true, true);
        }
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, bytes.Length);
    }

    internal void WriteUInt16(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Write(offset, bytes);
    }

    internal void WriteUInt32(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Write(offset, bytes);
    }

    internal NativeBattleGfSourceReader OpenReader(int offset, int length)
    {
        RequireRange(offset, length);
        return new(source.Arena.Working.OpenReadWindow(this, checked(source.Arena.WorkingOffset + offset), length, length));
    }

    internal void Publish()
    {
        RequireActive();
        dirty.Sort();
        bool checkpoint = false;
        try
        {
            publication.BeginCheckpoint();
            checkpoint = true;
            Span<byte> bytes = stackalloc byte[256];
            for (int cursor = 0; cursor < dirty.Count;)
            {
                int first = dirty[cursor++];
                int count = 1;
                bytes[0] = cells[(uint)first].Data;
                while (cursor < dirty.Count && count < bytes.Length && dirty[cursor] == first + count)
                {
                    bytes[count++] = cells[(uint)dirty[cursor++]].Data;
                }
                publication.View.Write(first, bytes[..count]);
            }
            publication.CommitCheckpoint();
            checkpoint = false;
            publication.Freeze();
        }
        catch
        {
            if (checkpoint)
            {
                publication.RollbackCheckpoint();
            }
            throw;
        }
        foreach (int offset in dirty)
        {
            Cell accepted = cells[(uint)offset];
            cells[(uint)offset] = accepted with { Dirty = false };
        }
        dirty.Clear();
        NativeBattleTaskOperation.AcceptPublishedEffectStorage(source);
    }

    private void RequireRange(int offset, int length)
    {
        RequireActive();
        if (offset < 0 || length < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native node operation leaves its actual bounded source.");
        }
    }

    private void RequireActive() => ObjectDisposedException.ThrowIf(disposed, this);
    long INativeBattleReadWindowSource.ReadWindowGeneration => 0;
    void INativeBattleReadWindowSource.ValidateReadWindow(long generation)
    {
        RequireActive();
        _ = baseline.Length;
    }
    void INativeBattleReadWindowSource.SeedReadWindow(NativeBattleReadWindowLease lease)
    {
    }
    void INativeBattleReadWindowSource.ReadWindowByte(int absoluteOffset, out byte value, out bool known)
    {
        int offset = checked(absoluteOffset - source.Arena.WorkingOffset);
        RequireRange(offset, 1);
        if (cells.TryGetValue((uint)offset, out Cell cell))
        {
            value = cell.Data;
            known = cell.Known;
            return;
        }
        Span<byte> bytes = stackalloc byte[1];
        Span<bool> flags = stackalloc bool[1];
        baseline.ReadWithKnownness(offset, bytes, flags);
        value = bytes[0];
        known = flags[0];
    }

    public void Dispose()
    {
        if (!disposed)
        {
            source.Arena.Working.InvalidateReadWindows(this);
            publication.Dispose();
            baseline.Dispose();
            disposed = true;
        }
    }
}
