using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal interface INativeBattleReadWindowSource
{
    long ReadWindowGeneration { get; }
    void ValidateReadWindow(long generation);
    void ReadWindowByte(int absoluteOffset, out byte value, out bool known);
    void ReadWindowWithKnownness(int absoluteOffset, Span<byte> destination, Span<bool> known)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            ReadWindowByte(absoluteOffset + index, out byte value, out bool isKnown);
            destination[index] = value;
            known[index] = isKnown;
        }
    }
    void SeedReadWindow(NativeBattleReadWindowLease lease);
    bool ObservesLayer(INativeBattleReadWindowSource layer) => ReferenceEquals(this, layer);
}

internal sealed class NativeBattleReadWindowBudget
{
    private int maximum;
    internal int Retained { get; private set; }

    internal void Reset(int limit)
    {
        if (limit < 0 || Retained != 0)
        {
            throw new InvalidOperationException("Native temporal budget requires an inactive nonnegative limit.");
        }
        maximum = limit;
    }

    internal void Require(int additional)
    {
        if (additional < 0 || additional > maximum - Retained)
        {
            throw new InvalidOperationException("Native temporal composition exceeds its shared retained-byte journal.");
        }
    }

    internal void Accept(int count) => Retained += count;
    internal void Release(int count) => Retained -= count;
}

internal readonly record struct NativeBattleReadWindowByte(byte Value, bool Known);

internal sealed class NativeBattleReadWindowLease
{
    internal readonly NativeBattleStagingDeltaMap<NativeBattleReadWindowByte> Original = new(1005);
    internal NativeBattleReadWindowStore Store = null!;
    internal INativeBattleReadWindowSource? Source;
    internal NativeBattleReadWindowBudget? SharedBudget;
    internal long SourceGeneration;
    internal long Generation;
    internal int Offset;
    internal int Length;
    internal int Budget;
    internal int Index;
    internal int Additional;
    internal bool Active;

    internal void Seed(int absoluteOffset)
    {
        if (absoluteOffset < Offset || absoluteOffset >= Offset + Length || Original.TryGetValue((uint)absoluteOffset, out _))
        {
            return;
        }
        Store.ValidateBudget(this, 1);
        Original.EnsureCapacity(checked(Original.Count + 1));
        Source!.ReadWindowByte(absoluteOffset, out byte value, out bool known);
        Original[(uint)absoluteOffset] = new(value, known);
    }

    internal NativeBattleReadWindowByte ByteAt(int relativeOffset)
    {
        int absolute = Offset + relativeOffset;
        if (Original.TryGetValue((uint)absolute, out NativeBattleReadWindowByte original))
        {
            return original;
        }
        Source!.ReadWindowByte(absolute, out byte value, out bool known);
        return new(value, known);
    }
}

/// <summary>A scoped temporal read borrow. Closing it never rolls back the mutable source.</summary>
internal readonly struct NativeBattleReadWindow : IDisposable
{
    private readonly NativeBattleReadWindowLease? lease;
    private readonly long generation;

    internal NativeBattleReadWindow(NativeBattleReadWindowLease lease)
    {
        this.lease = lease;
        generation = lease.Generation;
    }

    private NativeBattleReadWindowLease Require(int offset, int length)
    {
        if (lease is null || !lease.Active || lease.Generation != generation)
        {
            throw new ObjectDisposedException(nameof(NativeBattleReadWindow));
        }
        lease.Source!.ValidateReadWindow(lease.SourceGeneration);
        if (offset < 0 || length < 0 || offset > lease.Length - length)
        {
            throw new InvalidDataException("Native temporal read leaves its observed source window.");
        }
        return lease;
    }

    public int Length => Require(0, 0).Length;
    internal int RetainedByteCount => Require(0, 0).Original.Count;

    public void ReadInto(int offset, Span<byte> destination)
    {
        NativeBattleReadWindowLease current = Require(offset, destination.Length);
        for (int index = 0; index < destination.Length; index++)
        {
            if (!current.ByteAt(offset + index).Known)
            {
                throw new NotSupportedException("Native temporal read intersects original unknown source bytes.");
            }
        }
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = current.ByteAt(offset + index).Value;
        }
        NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.CpuCopyPayload, destination.Length);
    }

    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        NativeBattleReadWindowLease current = Require(offset, destination.Length);
        if (destination.Length != known.Length)
        {
            throw new ArgumentException("Native temporal data and knownness destinations must have equal lengths.", nameof(known));
        }
        if (!destination.IsEmpty && current.Original.Count == 0)
        {
            current.Source!.ReadWindowWithKnownness(current.Offset + offset, destination, known);
        }
        else
        {
            int absolute = current.Offset + offset;
            for (int index = 0; index < destination.Length;)
            {
                if (current.Original.TryGetValue((uint)(absolute + index), out NativeBattleReadWindowByte original))
                {
                    destination[index] = original.Value;
                    known[index] = original.Known;
                    index++;
                    continue;
                }
                int first = index++;
                while (index < destination.Length && !current.Original.TryGetValue((uint)(absolute + index), out _))
                {
                    index++;
                }
                current.Source!.ReadWindowWithKnownness(absolute + first, destination[first..index], known[first..index]);
            }
        }
        NativeBattleTelemetry.Record(1002, NativeBattleTrafficKind.CpuCopyPayload, destination.Length * 2L);
    }

    public byte ReadByte(int offset)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadInto(offset, bytes);
        return bytes[0];
    }

    public ushort ReadUInt16(int offset)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));

    public uint ReadUInt32(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public int ReadInt32(int offset) => unchecked((int)ReadUInt32(offset));
    public void Dispose() => lease?.Store.Close(lease, generation);
}

internal sealed class NativeBattleReadWindowStore(int maximumRetainedBytes)
{
    private readonly List<NativeBattleReadWindowLease> active = [];
    private readonly Stack<NativeBattleReadWindowLease> available = [];
    private int retained;
    private bool preparing;
    internal long CapturedBytes { get; private set; }
    internal long ResetBytes { get; private set; }

    internal NativeBattleReadWindow Open(INativeBattleReadWindowSource source, int offset, int length, int budget, NativeBattleReadWindowBudget? sharedBudget = null)
    {
        if (preparing)
        {
            throw new InvalidOperationException("Native temporal observation preparation cannot be reentered.");
        }
        if (budget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }
        active.EnsureCapacity(active.Count + 1);
        available.EnsureCapacity(available.Count + active.Count + (available.Count == 0 ? 1 : 0));
        NativeBattleReadWindowLease lease = available.Count == 0 ? new() : available.Pop();
        lease.Store = this;
        lease.Source = source;
        lease.SharedBudget = sharedBudget;
        lease.SourceGeneration = source.ReadWindowGeneration;
        lease.Generation = checked(lease.Generation + 1);
        lease.Offset = offset;
        lease.Length = length;
        lease.Budget = Math.Min(length, budget);
        bool counted = false;
        preparing = true;
        try
        {
            source.ValidateReadWindow(lease.SourceGeneration);
            source.SeedReadWindow(lease);
            if (lease.Original.Count != 0)
            {
                CapturedBytes += lease.Original.Count;
                counted = true;
                NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, lease.Original.Count * 2L);
            }
            lease.SharedBudget?.Require(lease.Original.Count);
            lease.Index = active.Count;
            active.Add(lease);
            lease.Active = true;
            retained += lease.Original.Count;
            lease.SharedBudget?.Accept(lease.Original.Count);
            return new(lease);
        }
        catch
        {
            try
            {
                if (!counted && lease.Original.Count != 0)
                {
                    CapturedBytes += lease.Original.Count;
                    NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, lease.Original.Count * 2L);
                }
            }
            finally
            {
                ResetBytes += lease.Original.Clear();
                lease.Source = null;
                lease.SharedBudget = null;
                available.Push(lease);
            }
            throw;
        }
        finally
        {
            preparing = false;
        }
    }

    internal void ValidateBudget(NativeBattleReadWindowLease lease, int additional)
    {
        lease.SharedBudget?.Require(checked(lease.Original.Count + additional));
        if (additional > lease.Budget - lease.Original.Count || (long)retained + lease.Original.Count + additional > maximumRetainedBytes)
        {
            throw new InvalidOperationException("Native temporal reads exceed their bounded retained-byte journal.");
        }
    }

    internal void BeforeWrite(INativeBattleReadWindowSource? layer, int offset, int length)
    {
        if (preparing)
        {
            throw new InvalidOperationException("Native temporal observation preparation cannot be reentered by a mutation.");
        }
        if (active.Count == 0)
        {
            return;
        }
        preparing = true;
        try
        {
            int additional = 0;
            foreach (NativeBattleReadWindowLease lease in active)
            {
                lease.Additional = 0;
                if (layer is not null && !lease.Source!.ObservesLayer(layer))
                {
                    continue;
                }
                int first = Math.Max(offset, lease.Offset);
                int end = Math.Min(offset + length, lease.Offset + lease.Length);
                for (int current = first; current < end; current++)
                {
                    if (!lease.Original.TryGetValue((uint)current, out _))
                    {
                        lease.Additional++;
                    }
                }
                if (lease.Additional > lease.Budget - lease.Original.Count)
                {
                    throw new InvalidOperationException("Native temporal read exceeds its bounded retained-byte journal.");
                }
                additional = checked(additional + lease.Additional);
            }
            if (additional > maximumRetainedBytes - retained)
            {
                throw new InvalidOperationException("Native temporal reads exceed their aggregate retained-byte journal.");
            }
            for (int index = 0; index < active.Count; index++)
            {
                NativeBattleReadWindowBudget? group = active[index].SharedBudget;
                if (group is null)
                {
                    continue;
                }
                int groupAdditional = 0;
                for (int other = 0; other < active.Count; other++)
                {
                    if (ReferenceEquals(group, active[other].SharedBudget))
                    {
                        groupAdditional = checked(groupAdditional + active[other].Additional);
                    }
                }
                group.Require(groupAdditional);
            }
            foreach (NativeBattleReadWindowLease lease in active)
            {
                lease.Original.EnsureCapacity(checked(lease.Original.Count + lease.Additional));
            }
            foreach (NativeBattleReadWindowLease lease in active)
            {
                if (lease.Additional == 0)
                {
                    continue;
                }
                int first = Math.Max(offset, lease.Offset);
                int end = Math.Min(offset + length, lease.Offset + lease.Length);
                for (int current = first; current < end; current++)
                {
                    if (!lease.Original.TryGetValue((uint)current, out _))
                    {
                        lease.Source!.ReadWindowByte(current, out byte value, out bool known);
                        lease.Original[(uint)current] = new(value, known);
                    }
                }
            }
            foreach (NativeBattleReadWindowLease lease in active)
            {
                lease.SharedBudget?.Accept(lease.Additional);
            }
            retained += additional;
            CapturedBytes += additional;
            if (additional != 0)
            {
                NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, additional * 2L);
            }
        }
        finally
        {
            preparing = false;
        }
    }

    internal void CloseSource(INativeBattleReadWindowSource source)
    {
        for (int index = active.Count - 1; index >= 0; index--)
        {
            NativeBattleReadWindowLease lease = active[index];
            if (lease.Source!.ObservesLayer(source))
            {
                Close(lease, lease.Generation);
            }
        }
    }

    internal void CloseAll()
    {
        while (active.Count != 0)
        {
            NativeBattleReadWindowLease lease = active[^1];
            Close(lease, lease.Generation);
        }
    }

    internal void Close(NativeBattleReadWindowLease lease, long generation)
    {
        if (!lease.Active || lease.Generation != generation)
        {
            return;
        }
        if (preparing)
        {
            throw new InvalidOperationException("Native temporal observation cannot close during mutation preparation.");
        }
        retained -= lease.Original.Count;
        lease.SharedBudget?.Release(lease.Original.Count);
        lease.SharedBudget = null;
        int last = active.Count - 1;
        if (lease.Index != last)
        {
            NativeBattleReadWindowLease moved = active[last];
            active[lease.Index] = moved;
            moved.Index = lease.Index;
        }
        active.RemoveAt(last);
        lease.Active = false;
        lease.Source = null;
        ResetBytes += lease.Original.Clear();
        available.Push(lease);
    }
}

internal sealed partial class NativeBattleWorkingMemory : INativeBattleReadWindowSource
{
    private NativeBattleReadWindowStore? readWindows;
    internal long ReadWindowCapturedByteCount => readWindows?.CapturedBytes ?? 0;
    internal long ReadWindowResetByteCount => readWindows?.ResetBytes ?? 0;

    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, int maximumRetainedBytes = 1_048_576) => OpenReadWindow(this, offset, length, maximumRetainedBytes);

    internal NativeBattleReadWindow OpenReadWindow(INativeBattleReadWindowSource source, int offset, int length, int maximumRetainedBytes, NativeBattleReadWindowBudget? sharedBudget = null)
    {
        RequireRange(offset, length);
        readWindows ??= new(maxJournalEntries);
        return readWindows.Open(source, offset, length, maximumRetainedBytes, sharedBudget);
    }

    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, NativeBattleReadWindowBudget sharedBudget, int maximumRetainedBytes)
    {
        return OpenReadWindow(this, offset, length, maximumRetainedBytes, sharedBudget);
    }

    internal void PrepareReadWindowWrite(int offset, int length)
    {
        RequireRange(offset, length);
        ObserveReadWindowWrite(null, offset, length);
    }

    internal void ObserveReadWindowWrite(INativeBattleReadWindowSource? layer, int offset, int length) => readWindows?.BeforeWrite(layer, offset, length);
    internal void InvalidateReadWindows(INativeBattleReadWindowSource source) => readWindows?.CloseSource(source);
    internal void SeedReadWindowUndo(NativeBattleReadWindowLease lease)
    {
        if (byteUndo is null)
        {
            return;
        }
        foreach (ByteUndo undo in byteUndo)
        {
            lease.Seed(undo.Offset);
        }
    }

    long INativeBattleReadWindowSource.ReadWindowGeneration => 0;
    void INativeBattleReadWindowSource.ValidateReadWindow(long generation) => RequireActive();
    void INativeBattleReadWindowSource.SeedReadWindow(NativeBattleReadWindowLease lease) => SeedReadWindowUndo(lease);
    void INativeBattleReadWindowSource.ReadWindowByte(int absoluteOffset, out byte value, out bool isKnown)
    {
        value = ByteAt(absoluteOffset);
        isKnown = IsKnown(absoluteOffset);
    }

    void INativeBattleReadWindowSource.ReadWindowWithKnownness(int absoluteOffset, Span<byte> destination, Span<bool> known) => ReadWithKnownness(absoluteOffset, destination, known);

    internal void ReadWindowCurrentByte(int absoluteOffset, out byte value, out bool isKnown)
    {
        value = ByteAt(absoluteOffset);
        isKnown = IsKnown(absoluteOffset);
    }
}

internal sealed partial class NativeBattleArenaOverlay
{
    private long readWindowGeneration;

    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, int maximumRetainedBytes)
    {
        NativeBattleEffectArena owner = Source;
        owner.RequireRange(offset, length);
        return owner.Working.OpenReadWindow(this, owner.WorkingOffset + offset, length, maximumRetainedBytes);
    }

    private void ObserveReadWindowWrite(int offset, int length)
    {
        NativeBattleEffectArena owner = Source;
        owner.Working.ObserveReadWindowWrite(this, owner.WorkingOffset + offset, length);
    }

    bool INativeBattleReadWindowSource.ObservesLayer(INativeBattleReadWindowSource layer) => ReferenceEquals(this, layer) || privateParent is not null && ((INativeBattleReadWindowSource)privateParent).ObservesLayer(layer);

    long INativeBattleReadWindowSource.ReadWindowGeneration => readWindowGeneration;

    void INativeBattleReadWindowSource.ValidateReadWindow(long generation)
    {
        if (source is null || generation != readWindowGeneration)
        {
            throw new ObjectDisposedException(nameof(NativeBattleReadWindow));
        }
        _ = Source;
    }

    void INativeBattleReadWindowSource.SeedReadWindow(NativeBattleReadWindowLease lease)
    {
        NativeBattleEffectArena owner = Source;
        owner.Working.SeedReadWindowUndo(lease);
        if (privateParent is not null)
        {
            ((INativeBattleReadWindowSource)privateParent).SeedReadWindow(lease);
        }
        foreach (ByteUndo undo in byteUndo)
        {
            lease.Seed(owner.WorkingOffset + undo.Offset);
        }
        if (mergingChild)
        {
            foreach (ByteUndo undo in mergeBytes)
            {
                lease.Seed(owner.WorkingOffset + undo.Offset);
            }
        }
    }

    void INativeBattleReadWindowSource.ReadWindowByte(int absoluteOffset, out byte value, out bool isKnown)
    {
        NativeBattleEffectArena owner = Source;
        int offset = absoluteOffset - owner.WorkingOffset;
        if (offset < present.Length && present[offset])
        {
            value = data[offset];
            isKnown = known[offset];
        }
        else
        {
            if (privateParent is not null)
            {
                ((INativeBattleReadWindowSource)privateParent).ReadWindowByte(absoluteOffset, out value, out isKnown);
            }
            else
            {
                owner.Working.ReadWindowCurrentByte(absoluteOffset, out value, out isKnown);
            }
        }
    }
}

public partial record NativeBattleEffectArena
{
    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, int maximumRetainedBytes = 1_048_576)
    {
        RequireRange(offset, length);
        if (snapshot)
        {
            throw new InvalidOperationException("Immutable native storage does not require a temporal observation window.");
        }
        if (TryEffectiveRoute(offset, length, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            return transaction!.OpenReadWindow(routedOffset, length, maximumRetainedBytes);
        }
        return Working.OpenReadWindow(workingOffset + offset, length, maximumRetainedBytes);
    }
}

internal sealed partial class NativeBattleEffectArenaTransaction
{
    internal NativeBattleReadWindow OpenReadWindow(int offset, int length, int maximumRetainedBytes = 1_048_576)
    {
        RequireActive();
        return writer.OwnedOverlay.OpenReadWindow(offset, length, maximumRetainedBytes);
    }
}
