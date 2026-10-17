using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResourcePointer(NativeBattleModelCpuReference? Cpu, uint? StagingAddress, int? Length = null);
public sealed record NativeBattleResourceRowReference(int? CpuRow, uint? StagingAddress);

public sealed partial record NativeBattleFileStaging
{
    public ImmutableDictionary<uint, NativeBattleResourcePointer?> ResourcePointers { get; init; } = ImmutableDictionary<uint, NativeBattleResourcePointer?>.Empty;

    private void ValidateResourcePointers()
    {
        if (ResourcePointers is null)
        {
            throw new InvalidDataException("Native resource pointer fields require an explicit retained owner.");
        }
        foreach ((uint address, NativeBattleResourcePointer? pointer) in ResourcePointers)
        {
            if (address < BaseAddress || (ulong)address + 4 > (ulong)BaseAddress + int.MaxValue || pointer is { Length: < 0 } || pointer is not null && (pointer.Cpu is not null) == (pointer.StagingAddress is not null) || pointer?.Cpu is { } cpu && (cpu.Bank >= 3 || cpu.Offset is < 0 or > NativeBattleModelCpuBank.Length))
            {
                throw new InvalidDataException("Native resource pointer field has an invalid physical address or source reference.");
            }
            if (pointer?.StagingAddress is uint numeric)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4)) != numeric)
                {
                    throw new InvalidDataException("Native resource pointer metadata differs from its actual shared staging DWORD.");
                }
            }
            else
            {
                int first = checked((int)(address - BaseAddress));
                if (KnownRanges.Any(range => range.Offset < first + 4 && range.Offset + range.Data.Length > first))
                {
                    throw new InvalidDataException("A symbolic or unknown native pointer cannot also own fabricated numeric bytes.");
                }
            }
        }
    }

    public NativeBattleFileStaging InvalidateResourcePointers(uint address, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return length == 0 ? this : this with { ResourcePointers = ResourcePointers.RemoveRange(ResourcePointers.Keys.Where(pointer => (ulong)pointer < (ulong)address + (uint)length && (ulong)pointer + 4 > address)) };
    }

    public NativeBattleResourcePointer? ReadResourcePointer(uint address)
    {
        if (ResourcePointers.TryGetValue(address, out NativeBattleResourcePointer? pointer))
        {
            return pointer;
        }
        return new(null, BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4)));
    }

    public NativeBattleFileStaging WriteResourcePointer(uint address, NativeBattleResourcePointer? pointer)
    {
        NativeBattleFileStaging current = address < BaseAddress ? Rebase(address) : this;
        if (pointer?.StagingAddress is uint numeric)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, numeric);
            current = current.Write(address, bytes);
        }
        else
        {
            int first = checked((int)(address - current.BaseAddress));
            int end = checked(first + 4);
            ImmutableArray<NativeBattleEffectMemoryRegion>.Builder ranges = ImmutableArray.CreateBuilder<NativeBattleEffectMemoryRegion>();
            foreach (NativeBattleEffectMemoryRegion range in current.KnownRanges)
            {
                int rangeEnd = checked(range.Offset + range.Data.Length);
                if (rangeEnd <= first || range.Offset >= end)
                {
                    ranges.Add(range);
                    continue;
                }
                if (range.Offset < first)
                {
                    ranges.Add(new(range.Offset, range.Data.Slice(0, first - range.Offset)));
                }
                if (rangeEnd > end)
                {
                    ranges.Add(new(end, range.Data.Slice(end - range.Offset, rangeEnd - end)));
                }
            }
            current = (current with { KnownRanges = ranges.ToImmutable() }).InvalidateResourcePointers(address, 4);
        }
        return current with { ResourcePointers = current.ResourcePointers.SetItem(address, pointer) };
    }
}
