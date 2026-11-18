namespace Memoria.FF8.Runtime;

/// <summary>One effect callback's coherent geometry snapshots over immutable CPU publications.</summary>
internal sealed class NativeBattleEffect314GeometryReadScope
{
    private sealed class Entry(NativeBattleModelCpuBank bank, NativeBattleModelCpuReference reference, int extent, bool pinned, byte[] data, bool[] known)
    {
        internal NativeBattleModelCpuBank Bank { get; set; } = bank;
        internal NativeBattleModelCpuReference Reference { get; } = reference;
        internal int Extent { get; } = extent;
        internal bool Pinned { get; } = pinned;
        internal byte[] Data { get; } = data;
        internal bool[] Known { get; } = known;
    }

    private readonly Dictionary<NativeBattleModelCpuReference, Entry> entries = [];

    internal byte[] Read(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, int extent, int offset, int length)
    {
        NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, extent);
        _ = view.Slice(offset, length);
        NativeBattleModelCpuReference reference = pointer.Cpu is { IsOwnedResource: true } owned ? owned : throw new InvalidDataException("Effect314 geometry snapshots require an actual owned CPU section.");
        NativeBattleModelCpuBank bank = pointer.CpuPublication ?? cpu.GetResourceBank(reference);
        if (!entries.TryGetValue(reference, out Entry? entry) || !ReferenceEquals(entry.Bank, bank) || entry.Extent != extent || entry.Pinned != (pointer.CpuPublication is not null))
        {
            (byte[] data, bool[] known) = view.ReadSnapshot(0, extent);
            entry = new(bank, reference, extent, pointer.CpuPublication is not null, data, known);
            entries[reference] = entry;
        }
        for (int index = offset; index < offset + length; index++)
        {
            if (!entry.Known[index])
            {
                throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
            }
        }
        return entry.Data.AsSpan(offset, length).ToArray();
    }

    internal void ObserveWrite(NativeBattleModelCpuResources before, NativeBattleModelCpuResources after, NativeBattleResourcePointer pointer, int offset, ReadOnlySpan<byte> bytes)
    {
        NativeBattleModelCpuReference reference = pointer.Cpu is { IsOwnedResource: true } owned ? owned : throw new InvalidDataException("Effect314 geometry updates require an actual owned CPU section.");
        NativeBattleModelCpuBank original = before.GetResourceBank(reference);
        NativeBattleModelCpuBank updated = after.GetResourceBank(reference);
        long first = (long)reference.ResourceStorageOffset + offset;
        long last = first + bytes.Length;
        foreach (Entry entry in entries.Values)
        {
            if (entry.Pinned || entry.Reference.ResourceOnly != reference.ResourceOnly || !ReferenceEquals(entry.Bank, original) || !ReferenceEquals(after.GetResourceBank(entry.Reference), updated))
            {
                continue;
            }
            long start = Math.Max(first, entry.Reference.ResourceStorageOffset);
            long end = Math.Min(last, (long)entry.Reference.ResourceStorageOffset + entry.Extent);
            if (start < end)
            {
                int destination = checked((int)(start - entry.Reference.ResourceStorageOffset));
                int count = checked((int)(end - start));
                bytes.Slice(checked((int)(start - first)), count).CopyTo(entry.Data.AsSpan(destination, count));
                Array.Fill(entry.Known, true, destination, count);
            }
            entry.Bank = updated;
        }
    }
}
