using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModulePoolTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, NativeBattleResourcePointer>? ResourcePointers { get => RequireSnapshotData().Pointers; init => RejectInitializer(); }

    public IEnumerable<KeyValuePair<int, NativeBattleResourcePointer>> EnumerateResourcePointers()
    {
        if (owner is null)
        {
            foreach (var entry in RequireSnapshotData().Pointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty)
            {
                yield return entry;
            }
            yield break;
        }
        int start = Start;
        using var pointerEntries = owner.Body.EnumerateResourcePointers();
        while (pointerEntries.MoveNext())
        {
            (int offset, NativeBattleResourcePointer? pointer) = pointerEntries.Current;
            if (offset >= start + 12 && offset + 4 <= start + owner.Stride)
            {
                yield return new(offset - start, pointer ?? throw new InvalidDataException("Native task pointer metadata lacks its physical source."));
            }
        }
    }

    internal void ValidateResourcePointers()
    {
        ValidateUnknownPayloadRanges();
        if (owner is not null)
        {
            return;
        }
        foreach ((int offset, NativeBattleResourcePointer pointer) in EnumerateResourcePointers())
        {
            RequirePayloadRange(offset, 4);
            if (pointer is null || !pointer.HasValidOwner())
            {
                throw new InvalidDataException("Native task resource pointer requires its actual physical source.");
            }
            if ((pointer.StagingAddress ?? pointer.Cpu?.PhysicalAddress) is uint numeric)
            {
                if (ReadDword(offset) != numeric)
                {
                    throw new InvalidDataException("Native task resource pointer differs from its physical DWORD.");
                }
            }
            else if (RequireSnapshotData().Unknown is not { } ranges || !ranges.Any(range => range.Offset <= offset && range.Offset + (long)range.Length >= offset + 4L))
            {
                throw new InvalidDataException("Native task symbolic resource row must retain unknown numeric bytes.");
            }
        }
    }

    public NativeBattleResourcePointer ReadResourcePointer(int offset)
    {
        RequirePayloadRange(offset, 4);
        if (owner is not null)
        {
            return owner.Body.ReadResourcePointer(Start + offset) ?? throw new InvalidDataException("Native task pointer metadata lacks its source owner.");
        }
        ValidateResourcePointers();
        return RequireSnapshotData().Pointers is { } pointers && pointers.TryGetValue(offset, out NativeBattleResourcePointer? pointer) ? pointer : new(null, ReadDword(offset));
    }

    public NativeBattleModulePoolTask WriteResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequirePayloadRange(offset, 4);
        if (pointer is null || !pointer.HasValidOwner())
        {
            throw new NotSupportedException("Native task pointer write requires its actual source owner.");
        }
        RequireOwner().Body.SetResourcePointer(Start + offset, pointer);
        return this;
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

    public NativeBattleModulePoolTask WriteResourceRowReference(int offset, NativeBattleResourceRowReference row) => WriteResourcePointer(offset, new(null, row.StagingAddress) { CpuRow = row.CpuRow });
}
