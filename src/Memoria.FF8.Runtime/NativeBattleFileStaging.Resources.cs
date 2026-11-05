using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    internal NativeBattleFileStaging BindResource(uint address, ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault)
        {
            throw new InvalidDataException("Native staging resource binding requires immutable source bytes.");
        }
        RequireWritableRange(address, bytes.Length);
        if (address < BaseAddress)
        {
            Rebase(address);
        }
        if (!bytes.IsEmpty)
        {
            if (Transaction is { } transaction)
            {
                transaction.BindResource(address, bytes);
            }
            else
            {
                RequireOwner().BindResource(address, bytes);
            }
        }
        return this;
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
    private readonly record struct ResourceBinding(uint Address, ImmutableArray<byte> Data, long Sequence);
    private readonly record struct ResourceEvent(long Sequence, uint Address, byte Kind, int Index);
    private delegate void SpanWriter(uint address, ReadOnlySpan<byte> bytes);
    private readonly List<ResourceBinding> resources = [];
    private readonly List<ResourceEvent> resourceEvents = [];

    internal void BindResource(uint address, ImmutableArray<byte> data)
    {
        source.RequireReadWindowMutation();
        InvalidatePointers(address, data.Length);
        resources.Add(new(address, data, ++sequence));
    }

    private bool ResourceByte(uint address, out byte value, out long written)
    {
        for (int index = resources.Count - 1; index >= 0; index--)
        {
            ResourceBinding binding = resources[index];
            if (address >= binding.Address && (ulong)address < (ulong)binding.Address + (uint)binding.Data.Length)
            {
                value = binding.Data[(int)(address - binding.Address)];
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
        RequireLayerActive();
        long bindingSequence = 0;
        data = default;
        for (int index = resources.Count - 1; index >= 0; index--)
        {
            ResourceBinding binding = resources[index];
            if (!Intersects(binding.Address, binding.Data.Length, address, length))
            {
                continue;
            }
            if (binding.Address != address || binding.Data.Length != length)
            {
                return false;
            }
            data = binding.Data;
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
        return !data.IsDefault || (parent is null ? source.TryResource(address, length, out data) : parent.TryResource(address, length, out data));
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

    private void PublishResourceData(Action<uint, ImmutableArray<byte>> bind, Action<uint, int> markUnknown, SpanWriter write)
    {
        OrderResourceEvents();
        Span<byte> data = stackalloc byte[NativeBattleFileStagingWorkingOwner.PageLength];
        for (int index = 0; index < resourceEvents.Count;)
        {
            ResourceEvent current = resourceEvents[index++];
            if (current.Kind == 0)
            {
                ResourceBinding binding = resources[current.Index];
                bind(binding.Address, binding.Data);
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
                lease.AddResource(binding.Address, binding.Data);
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
