namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int length)
    {
        if (Transaction is { } transaction)
        {
            return transaction.ReadSnapshot(address, length);
        }
        Validate();
        return ReadSnapshotStorage(address, length);
    }

    internal (byte[] Data, bool[] Known) ReadSnapshotStorage(uint address, int length)
    {
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new InvalidDataException("Native shared memory snapshot leaves its physical address extent.");
        }
        byte[] data = new byte[length];
        bool[] known = new bool[length];
        foreach (NativeBattleLoaderZeroRegion region in LoaderZeroRanges)
        {
            ulong start = Math.Max(address, region.Address);
            ulong last = Math.Min((ulong)address + (uint)length, (ulong)region.Address + (uint)region.Length);
            if (last > start)
            {
                Array.Fill(known, true, checked((int)(start - address)), checked((int)(last - start)));
            }
        }
        long first = (long)address - BaseAddress;
        long end = first + length;
        foreach (NativeBattleEffectMemoryRegion range in KnownRanges)
        {
            long start = Math.Max(first, range.Offset);
            long last = Math.Min(end, (long)range.Offset + range.Data.Length);
            if (last > start)
            {
                int count = checked((int)(last - start));
                int target = checked((int)(start - first));
                range.Data.AsSpan().Slice(checked((int)(start - range.Offset)), count).CopyTo(data.AsSpan(target));
                Array.Fill(known, true, target, count);
            }
        }
        return (data, known);
    }
}
