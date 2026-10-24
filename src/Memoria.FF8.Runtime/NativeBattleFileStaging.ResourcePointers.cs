using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResourcePointer(NativeBattleModelCpuReference? Cpu, uint? StagingAddress, int? Length = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? CpuRow { get; init; }

    internal bool HasValidOwner() => Length is not < 0 && (Cpu is not null ? 1 : 0) + (StagingAddress is not null ? 1 : 0) + (CpuRow is not null ? 1 : 0) == 1 && CpuRow is not (< 0 or >= 11) && (Cpu is not { } cpu || cpu.Bank <= 4 && cpu.Offset >= 0 && (cpu.Bank >= 3 || cpu.Offset <= NativeBattleModelCpuBank.Length));
}
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
            if (address < BaseAddress || (ulong)address + 4 > (ulong)BaseAddress + int.MaxValue || pointer is not null && !pointer.HasValidOwner())
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
                if (IntersectsLoaderZero(address, 4) || KnownRanges.Any(range => range.Offset < first + 4 && range.Offset + range.Data.Length > first))
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
        if (length == 0 || ResourcePointers.IsEmpty)
        {
            return this;
        }
        ImmutableDictionary<uint, NativeBattleResourcePointer?> pointers = ResourcePointers;
        foreach (var entry in ResourcePointers)
        {
            if ((ulong)entry.Key < (ulong)address + (uint)length && (ulong)entry.Key + 4 > address)
            {
                pointers = pointers.Remove(entry.Key);
            }
        }
        return ReferenceEquals(pointers, ResourcePointers) ? this : this with { ResourcePointers = pointers };
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
            if (current.Transaction is not null)
            {
                current = current.MarkUnknown(address, 4);
                return current with { ResourcePointers = current.ResourcePointers.SetItem(address, pointer) };
            }
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
            current = (current with { KnownRanges = ranges.ToImmutable() }).ForgetLoaderZero(address, 4).InvalidateResourcePointers(address, 4);
        }
        return current with { ResourcePointers = current.ResourcePointers.SetItem(address, pointer) };
    }
}
