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
        if (ResourcePointers is null)
        {
            return;
        }
        foreach ((int offset, NativeBattleResourcePointer pointer) in ResourcePointers)
        {
            RequirePointerRange(offset);
            if (pointer is null || pointer.Length is < 0 || (pointer.Cpu is null) == (pointer.StagingAddress is null) || pointer.Cpu is { } cpu && (cpu.Bank > 4 || cpu.Offset < 0 || cpu.Bank < 3 && cpu.Offset > NativeBattleModelCpuBank.Length))
            {
                throw new InvalidDataException("Native task resource pointer requires its actual physical source.");
            }
            uint numeric = pointer.StagingAddress ?? pointer.Cpu!.PhysicalAddress;
            if (this.ReadDword(offset) != numeric)
            {
                throw new InvalidDataException("Native task resource pointer differs from its physical DWORD.");
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
        if (pointer is null || (pointer.Cpu is null) == (pointer.StagingAddress is null))
        {
            throw new NotSupportedException("Native task pointer write requires its actual numeric address.");
        }
        uint numeric = pointer.StagingAddress ?? pointer.Cpu!.PhysicalAddress;
        NativeBattleModulePoolTask current = this.WriteDword(offset, numeric);
        current = current with { ResourcePointers = (current.ResourcePointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty).SetItem(offset, pointer) };
        current.ValidateResourcePointers();
        return current;
    }

    private void RequirePointerRange(int offset)
    {
        if (Payload.IsDefault || offset < 12 || offset > Payload.Length + 8)
        {
            throw new InvalidDataException("Native task pointer access leaves its physical payload.");
        }
    }
}
