using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectArena
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers { get; init; }

    private void ValidateResourcePointerStorage()
    {
        if (ResourcePointers is null)
        {
            return;
        }
        foreach (var entry in ResourcePointers)
        {
            RequireRange(entry.Key, 4);
            NativeBattleResourcePointer? pointer = entry.Value;
            if (pointer is { Length: < 0 } || pointer is not null && (pointer.Cpu is not null) == (pointer.StagingAddress is not null) || pointer?.Cpu is { } cpu && (cpu.Bank > 4 || cpu.Offset < 0 || cpu.Bank < 3 && cpu.Offset > NativeBattleModelCpuBank.Length))
            {
                throw new InvalidDataException("Native arena resource pointer has an invalid source owner.");
            }
            if (pointer?.StagingAddress is uint numeric)
            {
                if (IntersectsUnknown(entry.Key, 4) || BinaryPrimitives.ReadUInt32LittleEndian(ReadStorage(entry.Key, 4)) != numeric)
                {
                    throw new InvalidDataException("Native arena resource pointer differs from its physical bytes.");
                }
            }
            else if (Enumerable.Range(entry.Key, 4).Any(offset => !IntersectsUnknown(offset, 1)))
            {
                throw new InvalidDataException("Native symbolic arena resource pointer must retain unknown numeric bytes.");
            }
        }
    }

    private NativeBattleEffectArena? RemoveResourcePointers(int offset, int length)
    {
        if (ResourcePointers is null || length <= 0)
        {
            return null;
        }
        var updated = ResourcePointers.RemoveRange(ResourcePointers.Keys.Where(pointer => pointer < (long)offset + length && pointer + 4L > offset));
        return updated.Count == ResourcePointers.Count ? null : this with { ResourcePointers = updated.Count == 0 ? null : updated };
    }

    public NativeBattleResourcePointer? ReadResourcePointer(int offset)
    {
        RequireRange(offset, 4);
        var pointers = Transaction is { } transaction ? transaction.ResourcePointers : ResourcePointers;
        if (pointers is not null && pointers.TryGetValue(offset, out NativeBattleResourcePointer? pointer))
        {
            var bytes = ReadSnapshot(offset, 4);
            if (pointer?.StagingAddress is uint numeric)
            {
                if (bytes.Known.Any(value => !value) || BinaryPrimitives.ReadUInt32LittleEndian(bytes.Data) != numeric)
                {
                    throw new InvalidDataException("Native arena pointer metadata differs from its physical DWORD.");
                }
            }
            else if (bytes.Known.Any(value => value))
            {
                throw new InvalidDataException("Native symbolic arena pointer has fabricated numeric bytes.");
            }
            return pointer;
        }
        return new(null, BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4)));
    }

    public NativeBattleEffectArena WriteResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequireRange(offset, 4);
        NativeBattleEffectArena current;
        if (pointer?.StagingAddress is uint numeric)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, numeric);
            current = Write(offset, bytes);
        }
        else
        {
            current = MarkUnknown(offset, 4);
        }
        if (current.Transaction is { } transaction)
        {
            transaction.SetResourcePointer(offset, pointer);
            return current;
        }
        return current with { ResourcePointers = (current.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty).SetItem(offset, pointer) };
    }
}
