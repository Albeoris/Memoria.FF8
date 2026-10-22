using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, NativeBattleResourcePointer>? ResourcePointers { get; init; }

    internal NativeBattleModulePoolTask WithoutResourcePointers(int offset, int length)
    {
        if (ResourcePointers is null)
        {
            return this;
        }
        ImmutableDictionary<int, NativeBattleResourcePointer> updated = ResourcePointers.RemoveRange(ResourcePointers.Keys.Where(field => field < (long)offset + length && field + 4L > offset));
        return updated.Count == ResourcePointers.Count ? this : this with { ResourcePointers = updated.IsEmpty ? null : updated };
    }

    internal void ValidateResourcePointers()
    {
        ValidateUnknownPayloadRanges();
        if (ResourcePointers is null)
        {
            return;
        }
        foreach ((int offset, NativeBattleResourcePointer pointer) in ResourcePointers)
        {
            RequirePointerRange(offset);
            if (pointer is null || !pointer.HasValidOwner())
            {
                throw new InvalidDataException("Native task resource pointer requires its actual physical source.");
            }
            if ((pointer.StagingAddress ?? pointer.Cpu?.PhysicalAddress) is uint numeric)
            {
                if (this.ReadDword(offset) != numeric)
                {
                    throw new InvalidDataException("Native task resource pointer differs from its physical DWORD.");
                }
            }
            else if (UnknownPayloadRanges is not { } ranges || !ranges.Any(range => range.Offset <= offset && range.Offset + (long)range.Length >= offset + 4L))
            {
                throw new InvalidDataException("Native task symbolic resource row must retain unknown numeric bytes.");
            }
        }
    }

    public NativeBattleResourcePointer ReadResourcePointer(int offset)
    {
        RequirePointerRange(offset);
        ValidateResourcePointers();
        return ResourcePointers is not null && ResourcePointers.TryGetValue(offset, out NativeBattleResourcePointer? pointer) ? pointer : new(null, this.ReadDword(offset));
    }

    public NativeBattleModulePoolTask WriteResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequirePointerRange(offset);
        if (pointer is null || !pointer.HasValidOwner())
        {
            throw new NotSupportedException("Native task pointer write requires its actual source owner.");
        }
        NativeBattleModulePoolTask current = (pointer.StagingAddress ?? pointer.Cpu?.PhysicalAddress) is uint numeric ? this.WriteDword(offset, numeric) : MarkPayloadUnknown(offset, 4);
        current = current with { ResourcePointers = (current.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty).SetItem(offset, pointer) };
        current.ValidateResourcePointers();
        return current;
    }

    public NativeBattleResourceRowReference ReadResourceRowReference(int offset)
    {
        NativeBattleResourcePointer pointer = ReadResourcePointer(offset);
        if (pointer.Cpu is not null || pointer.CpuRow is null && pointer.StagingAddress is null)
        {
            throw new NotSupportedException("Native task field does not identify a model resource row.");
        }
        return new(pointer.CpuRow, pointer.StagingAddress);
    }

    public NativeBattleModulePoolTask WriteResourceRowReference(int offset, NativeBattleResourceRowReference row)
    {
        return WriteResourcePointer(offset, new(null, row.StagingAddress) { CpuRow = row.CpuRow });
    }

    private void RequirePointerRange(int offset)
    {
        if (Payload.IsDefault || offset < 12 || offset > Payload.Length + 8)
        {
            throw new InvalidDataException("Native task pointer access leaves its physical payload.");
        }
    }
}
