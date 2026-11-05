using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    internal NativeBattleFileStaging BindResource(uint address, ImmutableArray<byte> bytes) => BindResource(address, bytes, 0, bytes.IsDefault ? 0 : bytes.Length);

    internal NativeBattleFileStaging BindResource(uint address, ImmutableArray<byte> bytes, int sourceOffset, int length)
    {
        if (bytes.IsDefault || sourceOffset < 0 || length < 0 || sourceOffset > bytes.Length - length)
        {
            throw new InvalidDataException("Native staging resource binding requires immutable source bytes.");
        }
        RequireWritableRange(address, length);
        if (address < BaseAddress)
        {
            Rebase(address);
        }
        if (length != 0)
        {
            if (Transaction is { } transaction)
            {
                transaction.BindResource(address, bytes, sourceOffset, length);
            }
            else
            {
                RequireOwner().BindResource(address, bytes, sourceOffset, length);
            }
        }
        return this;
    }

    internal bool TryBorrowResource(uint address, int length, out ImmutableArray<byte> bytes, out int sourceOffset)
    {
        RequirePhysicalRange(address, length);
        if (snapshot)
        {
            bytes = default;
            sourceOffset = 0;
            return false;
        }
        return Transaction is { } transaction ? transaction.TryBorrowResource(address, length, out bytes, out sourceOffset) : RequireOwner().TryBorrowResource(address, length, out bytes, out sourceOffset);
    }

    private bool TryResource(uint address, int length, out ImmutableArray<byte> bytes)
    {
        if (snapshot)
        {
            bytes = default;
            return false;
        }
        return Transaction is { } transaction ? transaction.TryResource(address, length, out bytes) : RequireOwner().TryResource(address, length, out bytes);
    }
}

internal sealed partial class NativeBattleFileStagingOverlay
{
    private readonly record struct ResourceBinding(uint Address, ImmutableArray<byte> Data, int SourceOffset, int Length, long Sequence);
    private readonly record struct ResourceEvent(long Sequence, uint Address, byte Kind, int Index);
    private delegate void SpanWriter(uint address, ReadOnlySpan<byte> bytes);
    private readonly List<ResourceBinding> resources = [];
    private readonly List<ResourceEvent> resourceEvents = [];

    internal void BindResource(uint address, ImmutableArray<byte> data, int sourceOffset, int length)
    {
        source.RequireReadWindowMutation();
        InvalidatePointers(address, length);
        resources.Add(new(address, data, sourceOffset, length, ++sequence));
    }

    private bool ResourceByte(uint address, out byte value, out long written)
    {
        for (int index = resources.Count - 1; index >= 0; index--)
        {
            ResourceBinding binding = resources[index];
            if (address >= binding.Address && (ulong)address < (ulong)binding.Address + (uint)binding.Length)
            {
                value = binding.Data[binding.SourceOffset + (int)(address - binding.Address)];
                written = binding.Sequence;
                return true;
            }
        }
        value = 0;
        written = 0;
        return false;
    }

    internal bool TryResource(uint address, int length, out ImmutableArray<byte> data)
    {
        if (TryBorrowResource(address, length, out data, out int sourceOffset) && sourceOffset == 0 && data.Length == length)
        {
            return true;
        }
        data = default;
        return false;
    }

    internal bool TryBorrowResource(uint address, int length, out ImmutableArray<byte> data, out int sourceOffset)
    {
        RequireLayerActive();
        long bindingSequence = 0;
        data = default;
        sourceOffset = 0;
        for (int index = resources.Count - 1; index >= 0; index--)
        {
            ResourceBinding binding = resources[index];
            if (!Intersects(binding.Address, binding.Length, address, length))
            {
                continue;
            }
            if (address < binding.Address || (ulong)address + (uint)length > (ulong)binding.Address + (uint)binding.Length)
            {
                return TryBorrowResourceFragments(address, length, out data, out sourceOffset);
            }
            data = binding.Data;
            sourceOffset = checked(binding.SourceOffset + (int)(address - binding.Address));
            bindingSequence = binding.Sequence;
            break;
        }
        foreach (var entry in bytes)
        {
            if (entry.Key >= address && (ulong)entry.Key < (ulong)address + (uint)length && entry.Value.Sequence > bindingSequence)
            {
                data = default;
                return false;
            }
        }
        if (unknown.Any(range => range.Sequence > bindingSequence && Intersects(range.Address, range.Length, address, length)))
        {
            data = default;
            return false;
        }
        return !data.IsDefault || (parent is null ? source.TryBorrowResource(address, length, out data, out sourceOffset) : parent.TryBorrowResource(address, length, out data, out sourceOffset));
    }

    private bool TryBorrowResourceFragments(uint address, int length, out ImmutableArray<byte> data, out int sourceOffset)
    {
        List<int> boundaries = [0, length];
        foreach (ResourceBinding binding in resources)
        {
            long first = Math.Max(0L, (long)binding.Address - address);
            long end = Math.Min(length, (long)binding.Address + binding.Length - address);
            if (end > first)
            {
                boundaries.Add(checked((int)first));
                boundaries.Add(checked((int)end));
            }
        }
        boundaries.Sort();
        data = default;
        sourceOffset = 0;
        int previous = 0;
        foreach (int end in boundaries)
        {
            if (end <= previous)
            {
                continue;
            }
            if (!TryBorrowResource(checked(address + (uint)previous), end - previous, out ImmutableArray<byte> part, out int offset) || !data.IsDefault && (!data.Equals(part) || offset != sourceOffset + previous))
            {
                data = default;
                return false;
            }
            if (data.IsDefault)
            {
                data = part;
                sourceOffset = offset;
            }
            previous = end;
        }
        return !data.IsDefault;
    }

    private void OrderResourceEvents()
    {
        resourceEvents.Clear();
        resourceEvents.EnsureCapacity(resources.Count + unknown.Count + bytes.Count);
        for (int index = 0; index < resources.Count; index++)
        {
            resourceEvents.Add(new(resources[index].Sequence, resources[index].Address, 0, index));
        }
        for (int index = 0; index < unknown.Count; index++)
        {
            resourceEvents.Add(new(unknown[index].Sequence, unknown[index].Address, 1, index));
        }
        foreach (var entry in bytes)
        {
            resourceEvents.Add(new(entry.Value.Sequence, entry.Key, 2, 0));
        }
        resourceEvents.Sort((left, right) => left.Sequence != right.Sequence ? left.Sequence.CompareTo(right.Sequence) : left.Address.CompareTo(right.Address));
    }

    private void PublishResourceData(Action<uint, ImmutableArray<byte>, int, int> bind, Action<uint, int> markUnknown, SpanWriter write)
    {
        OrderResourceEvents();
        Span<byte> data = stackalloc byte[NativeBattleFileStagingWorkingOwner.PageLength];
        for (int index = 0; index < resourceEvents.Count;)
        {
            ResourceEvent current = resourceEvents[index++];
            if (current.Kind == 0)
            {
                ResourceBinding binding = resources[current.Index];
                bind(binding.Address, binding.Data, binding.SourceOffset, binding.Length);
            }
            else if (current.Kind == 1)
            {
                Unknown range = unknown[current.Index];
                markUnknown(range.Address, range.Length);
            }
            else
            {
                int count = 1;
                data[0] = bytes[current.Address].Data;
                while (index < resourceEvents.Count && count < data.Length && resourceEvents[index] is var next && next.Kind == 2 && next.Sequence == current.Sequence && (ulong)next.Address == (ulong)current.Address + (uint)count)
                {
                    data[count++] = bytes[next.Address].Data;
                    index++;
                }
                write(current.Address, data[..count]);
            }
        }
    }

    private void CaptureResourcePrefix(NativeBattleStagingReadWindowLease lease, NativeBattleFileStagingOverlay reader)
    {
        OrderResourceEvents();
        foreach (ResourceEvent current in resourceEvents)
        {
            if (current.Kind == 0)
            {
                ResourceBinding binding = resources[current.Index];
                lease.AddResource(binding.Address, binding.Data, binding.SourceOffset, binding.Length);
            }
            else if (current.Kind == 1)
            {
                Unknown range = unknown[current.Index];
                NativeBattleFileStagingWorkingOwner.AddReadWindowRange(lease.Unknown, range.Address, range.Length, lease.Address, lease.Length);
                foreach (uint address in lease.InitialCells.Keys)
                {
                    if (address >= range.Address && (ulong)address < (ulong)range.Address + (uint)range.Length)
                    {
                        NativeBattleReadWindowByte cell = lease.InitialCells[address];
                        lease.AddCell(address, cell.Value, false);
                    }
                }
            }
            else if (current.Address >= lease.Address && (ulong)current.Address < (ulong)lease.Address + (uint)lease.Length)
            {
                ResourceByte(current.Address, out _, out long bound);
                if (bound < current.Sequence && !reader.BoundBelow(this, current.Address))
                {
                    lease.AddCell(current.Address, bytes[current.Address].Data, true);
                }
            }
        }
    }

    private bool BoundBelow(NativeBattleFileStagingOverlay ancestor, uint address)
    {
        for (NativeBattleFileStagingOverlay? current = this; current is not null && !ReferenceEquals(current, ancestor); current = current.parent)
        {
            if (current.ResourceByte(address, out _, out _))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Intersects(uint first, int length, uint second, int secondLength) => first < (ulong)second + (uint)secondLength && second < (ulong)first + (uint)length;
}
