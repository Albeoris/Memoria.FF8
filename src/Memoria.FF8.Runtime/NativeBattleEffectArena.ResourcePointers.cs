using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public partial record NativeBattleEffectArena
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, NativeBattleResourcePointer?>? ResourcePointers
    {
        get
        {
            RequireSnapshotEnumeration();
            return initialPointers;
        }
        init
        {
            RequireImportMetadata();
            initialPointers = value;
        }
    }

    public bool TryGetResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        RequireRange(offset, 4);
        if (TryEffectiveRoute(offset, 4, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            return transaction!.TryGetResourcePointer(routedOffset, out pointer);
        }
        if (!IsSnapshot)
        {
            return Working.TryGetResourcePointer(workingOffset + offset, out pointer);
        }
        Validate();
        pointer = null;
        return initialPointers is not null && initialPointers.TryGetValue(offset, out pointer);
    }

    public NativeBattleResourcePointer? ReadResourcePointer(int offset)
        => TryGetResourcePointer(offset, out NativeBattleResourcePointer? pointer) ? pointer : new(null, ReadUInt32(offset));

    public IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers()
    {
        if (TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            foreach ((int offset, NativeBattleResourcePointer? pointer) in transaction!.EnumerateResourcePointers())
            {
                int relative = offset - routedOffset;
                if (relative >= 0 && relative <= bound - 4)
                {
                    yield return new(relative, pointer);
                }
            }
        }
        else if (IsSnapshot)
        {
            Validate();
            if (initialPointers is not null)
            {
                foreach (var pointer in initialPointers)
                {
                    yield return pointer;
                }
            }
        }
        else
        {
            using var pointers = Working.EnumerateResourcePointers();
            while (pointers.MoveNext())
            {
                int offset = pointers.Current.Key - workingOffset;
                if (offset >= 0 && offset <= bound - 4)
                {
                    yield return new(offset, pointers.Current.Value);
                }
            }
        }
    }

    public NativeBattleEffectArena WriteResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        RequireWritable();
        RequireRange(offset, 4);
        ValidatePointer(pointer);
        if (TryEffectiveRoute(offset, 4, out NativeBattleEffectArenaTransaction? transaction, out int routedOffset))
        {
            transaction!.WriteResourcePointer(routedOffset, pointer);
        }
        else
        {
            PrepareCoverage(1);
            long previousVersion = Working.Version;
            try
            {
                Working.SetResourcePointer(workingOffset + offset, pointer);
            }
            finally
            {
                if (Working.Version != previousVersion)
                {
                    RecordCoverage(offset, 4);
                    bool numeric = (pointer?.StagingAddress ?? pointer?.Cpu?.PhysicalAddress) is not null;
                    if (numeric)
                    {
                        RecordDataCoverage(offset, 4);
                    }
                    RecordKnowledge(offset, 4, numeric);
                }
            }
        }
        return this;
    }

    internal static void ValidatePointer(NativeBattleResourcePointer? pointer)
    {
        if (pointer is not null && !pointer.HasValidOwner())
        {
            throw new InvalidDataException("Native arena resource pointer has an invalid source owner.");
        }
    }

    private void ValidateResourcePointerStorage()
    {
        if (initialPointers is null)
        {
            return;
        }
        Span<byte> bytes = stackalloc byte[4];
        foreach ((int offset, NativeBattleResourcePointer? pointer) in initialPointers)
        {
            RequireRange(offset, 4);
            ValidatePointer(pointer);
            if ((pointer?.StagingAddress ?? pointer?.Cpu?.PhysicalAddress) is uint numeric)
            {
                ReadImportData(offset, bytes);
                if (IntersectsUnknown(offset, 4) || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != numeric)
                {
                    throw new InvalidDataException("Native arena resource pointer differs from its physical bytes.");
                }
            }
            else
            {
                for (int index = 0; index < 4; index++)
                {
                    if (!IntersectsUnknown(offset + index, 1))
                    {
                        throw new InvalidDataException("Native symbolic arena resource pointer must retain unknown numeric bytes.");
                    }
                }
            }
        }
    }

}
