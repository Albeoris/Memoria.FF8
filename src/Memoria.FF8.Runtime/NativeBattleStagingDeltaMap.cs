using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

/// <summary>Reusable compact journal index. Reset touches active entries, never its retained bucket capacity.</summary>
internal sealed class NativeBattleStagingDeltaMap<T>(int telemetrySite = 1035)
{
    private struct Entry
    {
        public uint Address;
        public T Value;
    }

    private Entry[] entries = [];
    private int[] buckets = [];
    private uint[] epochs = [];
    private uint epoch = 1;
    private int count;
    public int Count => count;
    public KeyView Keys => new(this);

    public T this[uint address]
    {
        get => TryGetValue(address, out T value) ? value : throw new KeyNotFoundException();
        set
        {
            EnsureCapacity(checked(count + 1));
            int slot = Find(address, out bool present);
            if (present)
            {
                entries[buckets[slot]].Value = value;
                return;
            }
            entries[count] = new() { Address = address, Value = value };
            epochs[slot] = epoch;
            buckets[slot] = count++;
        }
    }

    public bool TryGetValue(uint address, out T value)
    {
        if (buckets.Length != 0)
        {
            int slot = Find(address, out bool present);
            if (present)
            {
                value = entries[buckets[slot]].Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    public void Remove(uint address)
    {
        if (buckets.Length == 0)
        {
            return;
        }
        int slot = Find(address, out bool present);
        if (!present)
        {
            return;
        }
        int removed = buckets[slot];
        int last = --count;
        if (removed != last)
        {
            Entry moved = entries[last];
            int movedSlot = Find(moved.Address, out _);
            entries[removed] = moved;
            buckets[movedSlot] = removed;
        }
        entries[last] = default;
        buckets[slot] = -1;
    }

    public long Clear()
    {
        long cleared = 0;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<Entry>())
        {
            Array.Clear(entries, 0, count);
            cleared += (long)count * Unsafe.SizeOf<Entry>();
        }
        count = 0;
        epoch++;
        if (epoch == 0)
        {
            // Only the generation counter rollover requires clearing retained index stamps.
            Array.Clear(epochs);
            cleared += epochs.Length * 4L;
            epoch = 1;
        }
        return cleared;
    }

    public void EnsureCapacity(int required)
    {
        if (required < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(required));
        }
        if (required <= entries.Length)
        {
            return;
        }
        int capacity = Math.Max(32, buckets.Length);
        while (capacity / 2 < required)
        {
            capacity = checked(capacity * 2);
        }
        Entry[] grown = new Entry[capacity / 2];
        int[] grownBuckets = new int[capacity];
        uint[] grownEpochs = new uint[capacity];
        Array.Copy(entries, grown, count);
        NativeBattleTelemetry.Record(telemetrySite, NativeBattleTrafficKind.CpuCopyPayload, (long)count * Unsafe.SizeOf<Entry>());
        NativeBattleTelemetry.Record(telemetrySite, NativeBattleTrafficKind.ZeroFill, (long)grown.Length * Unsafe.SizeOf<Entry>() + capacity * 8L);
        entries = grown;
        buckets = grownBuckets;
        epochs = grownEpochs;
        epoch = 1;
        for (int index = 0; index < count; index++)
        {
            int slot = Find(entries[index].Address, out _);
            epochs[slot] = epoch;
            buckets[slot] = index;
        }
    }

    public Enumerator GetEnumerator() => new(this);

    private int Find(uint address, out bool present)
    {
        int mask = buckets.Length - 1;
        int slot = unchecked((int)(address * 2654435761u)) & mask;
        int deleted = -1;
        for (int inspected = 0; inspected < buckets.Length; inspected++)
        {
            if (epochs[slot] != epoch)
            {
                present = false;
                return deleted >= 0 ? deleted : slot;
            }
            int index = buckets[slot];
            if (index < 0)
            {
                if (deleted < 0)
                {
                    deleted = slot;
                }
            }
            else if (entries[index].Address == address)
            {
                present = true;
                return slot;
            }
            slot = (slot + 1) & mask;
        }
        present = false;
        return deleted >= 0 ? deleted : throw new InvalidOperationException("Native staging journal index exhausted its reserved capacity.");
    }

    internal readonly struct KeyView(NativeBattleStagingDeltaMap<T> owner)
    {
        public KeyEnumerator GetEnumerator() => new(owner);
    }

    internal struct KeyEnumerator(NativeBattleStagingDeltaMap<T> owner)
    {
        private int index = -1;
        public uint Current => owner.entries[index].Address;
        public bool MoveNext() => ++index < owner.count;
    }

    internal struct Enumerator(NativeBattleStagingDeltaMap<T> owner)
    {
        private int index = -1;
        public KeyValuePair<uint, T> Current => new(owner.entries[index].Address, owner.entries[index].Value);
        public bool MoveNext() => ++index < owner.count;
    }
}
