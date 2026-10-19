namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int length)
    {
        Validate();
        if (length < 0 || (ulong)address + (uint)length > (ulong)uint.MaxValue + 1)
        {
            throw new InvalidDataException("Native shared memory snapshot leaves its physical address extent.");
        }
        byte[] data = new byte[length];
        bool[] known = new bool[length];
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
