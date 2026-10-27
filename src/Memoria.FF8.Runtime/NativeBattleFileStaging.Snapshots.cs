using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    public (byte[] Data, bool[] Known) ReadSnapshot(uint address, int length)
    {
        RequirePhysicalRange(address, length);
        byte[] data = new byte[length];
        bool[] known = new bool[length];
        ReadWithKnownness(address, data, known);
        return (data, known);
    }

    public void ReadWithKnownness(uint address, Span<byte> data, Span<bool> known)
    {
        RequirePhysicalRange(address, data.Length);
        if (data.Length != known.Length)
        {
            throw new ArgumentException("Native staging data and knownness destinations must have equal lengths.", nameof(known));
        }
        if (Transaction is { } transaction)
        {
            transaction.ReadWithKnownness(address, data, known);
            return;
        }
        if (!snapshot)
        {
            RequireOwner().ReadWithKnownness(address, data, known);
            return;
        }
        Validate();
        ReadFrozen(address, data, known);
    }

    internal (byte[] Data, bool[] Known) ReadSnapshotStorage(uint address, int length) => ReadSnapshot(address, length);

    private void ReadFrozen(uint address, Span<byte> data, Span<bool> known)
    {
        data.Clear();
        known.Clear();
        NativeBattleTelemetry.Record(1025, NativeBattleTrafficKind.ZeroFill, data.Length * 2L);
        foreach (NativeBattleLoaderZeroRegion region in loaderZeroRanges)
        {
            ulong first = Math.Max(address, region.Address);
            ulong last = Math.Min((ulong)address + (uint)data.Length, (ulong)region.Address + (uint)region.Length);
            if (last > first)
            {
                known.Slice(checked((int)(first - address)), checked((int)(last - first))).Fill(true);
            }
        }
        CopyFrozenRanges(knownRanges, address, data, known, true);
        CopyFrozenRanges(retainedUnknownRanges, address, data, known, false);
    }

    private void CopyFrozenRanges(ImmutableArray<NativeBattleEffectMemoryRegion> ranges, uint address, Span<byte> data, Span<bool> known, bool assigned)
    {
        foreach (NativeBattleEffectMemoryRegion range in ranges)
        {
            ulong start = (ulong)baseAddress + (uint)range.Offset;
            ulong first = Math.Max(address, start);
            ulong last = Math.Min((ulong)address + (uint)data.Length, start + (uint)range.Data.Length);
            if (last > first)
            {
                int length = checked((int)(last - first));
                int target = checked((int)(first - address));
                NativeBattleTelemetry.Copy(1026, range.Data.AsSpan().Slice(checked((int)(first - start)), length), data.Slice(target, length));
                known.Slice(target, length).Fill(assigned);
            }
        }
    }

    private bool FrozenKnown(uint address, int length)
    {
        Span<byte> data = stackalloc byte[Math.Min(length, 4096)];
        Span<bool> known = stackalloc bool[Math.Min(length, 4096)];
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(length - offset, data.Length);
            ReadFrozen(checked(address + (uint)offset), data[..count], known[..count]);
            if (known[..count].Contains(false))
            {
                return false;
            }
            offset += count;
        }
        return true;
    }

    private static void ValidateData(NativeBattleFileStagingData data)
    {
        if (data.BaseAddress > Address || data.LoaderZeroRanges.IsDefault || data.RetainedUnknownRanges.IsDefault || data.ResourcePointers is null)
        {
            throw new InvalidDataException("Native staging snapshot lacks its explicit physical owners.");
        }
        int maximum = checked((int)Math.Min(int.MaxValue, (ulong)uint.MaxValue - data.BaseAddress + 1));
        NativeKnownByteRanges.Validate(data.KnownRanges, maximum);
        NativeKnownByteRanges.Validate(data.RetainedUnknownRanges, maximum);
        ulong previousEnd = 0;
        foreach (NativeBattleLoaderZeroRegion region in data.LoaderZeroRanges)
        {
            if (region is null || region.Length <= 0 || (ulong)region.Address + (uint)region.Length > (ulong)uint.MaxValue + 1 || region.Address < previousEnd)
            {
                throw new InvalidDataException("Native loader-zero ranges must be positive, ordered and disjoint physical intervals.");
            }
            previousEnd = (ulong)region.Address + (uint)region.Length;
        }
        foreach (NativeBattleEffectMemoryRegion hidden in data.RetainedUnknownRanges)
        {
            long end = (long)hidden.Offset + hidden.Data.Length;
            foreach (NativeBattleEffectMemoryRegion assigned in data.KnownRanges)
            {
                if (assigned.Offset < end && (long)assigned.Offset + assigned.Data.Length > hidden.Offset)
                {
                    throw new InvalidDataException("Native retained unknown bytes overlap assigned staging bytes.");
                }
            }
            ulong address = (ulong)data.BaseAddress + (uint)hidden.Offset;
            foreach (NativeBattleLoaderZeroRegion zero in data.LoaderZeroRanges)
            {
                if (zero.Address < address + (uint)hidden.Data.Length && (ulong)zero.Address + (uint)zero.Length > address)
                {
                    throw new InvalidDataException("Native retained unknown bytes contradict loader-zero provenance.");
                }
            }
        }
        Span<byte> bytes = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        foreach (var entry in data.ResourcePointers)
        {
            if (entry.Key < data.BaseAddress || (ulong)entry.Key - data.BaseAddress + 4 > int.MaxValue || (ulong)entry.Key + 4 > (ulong)uint.MaxValue + 1 || entry.Value is { } pointer && !pointer.HasValidOwner())
            {
                throw new InvalidDataException("Native resource pointer field has an invalid physical address or source reference.");
            }
            bytes.Clear();
            known.Clear();
            for (int lane = 0; lane < 4; lane++)
            {
                uint address = checked(entry.Key + (uint)lane);
                foreach (NativeBattleLoaderZeroRegion zero in data.LoaderZeroRanges)
                {
                    if (address >= zero.Address && (ulong)address < (ulong)zero.Address + (uint)zero.Length)
                    {
                        known[lane] = true;
                    }
                }
                int offset = checked((int)(address - data.BaseAddress));
                foreach (NativeBattleEffectMemoryRegion assigned in data.KnownRanges)
                {
                    if (offset >= assigned.Offset && offset - assigned.Offset < assigned.Data.Length)
                    {
                        bytes[lane] = assigned.Data[offset - assigned.Offset];
                        known[lane] = true;
                    }
                }
            }
            if (entry.Value?.StagingAddress is uint numeric)
            {
                if (known.Contains(false) || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) != numeric)
                {
                    throw new InvalidDataException("Native staging pointer metadata differs from its physical DWORD.");
                }
            }
            else if (known.Contains(true))
            {
                throw new InvalidDataException("Native symbolic staging pointer cannot own fabricated numeric bytes.");
            }
        }
    }
}
