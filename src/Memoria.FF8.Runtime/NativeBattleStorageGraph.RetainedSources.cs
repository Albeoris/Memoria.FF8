namespace Memoria.FF8.Runtime;

internal static class NativeBattleRetainedSourceAliases
{
    internal static void Validate(BattleSnapshot snapshot)
    {
        if (snapshot.NativeEffectMemory?.Rinoa88?.NodeData is not { } source)
        {
            return;
        }
        NativeBattleFileStaging staging = snapshot.NativeFileStaging ?? throw new InvalidOperationException("Retained node storage requires its current file staging authority.");
        uint address = NativeBattleRinoa88NodeResources.Address;
        int length = NativeBattleRinoa88NodeResources.Length;
        if (source.Length != length)
        {
            throw new InvalidOperationException("Retained node storage has an invalid physical extent.");
        }
        Span<byte> actual = stackalloc byte[256];
        Span<byte> expected = stackalloc byte[256];
        Span<bool> actualKnown = stackalloc bool[256];
        Span<bool> expectedKnown = stackalloc bool[256];
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(actual.Length, length - offset);
            source.ReadWithKnownness(offset, actual[..count], actualKnown[..count]);
            staging.ReadWithKnownness(checked(address + (uint)offset), expected[..count], expectedKnown[..count]);
            if (!actual[..count].SequenceEqual(expected[..count]) || !actualKnown[..count].SequenceEqual(expectedKnown[..count]))
            {
                throw new InvalidOperationException("Retained node data disagrees with its current file staging authority.");
            }
            offset += count;
        }
        Dictionary<int, NativeBattleResourcePointer?> pointers = [];
        foreach (KeyValuePair<uint, NativeBattleResourcePointer?> pointer in staging.EnumerateResourcePointers())
        {
            if (pointer.Key >= address && (ulong)pointer.Key + 4 <= (ulong)address + (uint)length)
            {
                pointers.Add(checked((int)(pointer.Key - address)), pointer.Value);
            }
        }
        int actualCount = 0;
        foreach (KeyValuePair<int, NativeBattleResourcePointer?> pointer in source.EnumerateResourcePointers())
        {
            actualCount++;
            if (!pointers.TryGetValue(pointer.Key, out NativeBattleResourcePointer? expectedPointer) || pointer.Value != expectedPointer)
            {
                throw new InvalidOperationException("Retained node provenance disagrees with its current file staging authority.");
            }
        }
        if (actualCount != pointers.Count)
        {
            throw new InvalidOperationException("Retained node storage is missing current file staging provenance.");
        }
    }

    internal static BattleSnapshot Rebind(BattleSnapshot snapshot)
    {
        if (snapshot.NativeEffectMemory is not { Rinoa88.NodeData: { } source } memory)
        {
            return snapshot;
        }
        NativeBattleFileStaging staging = snapshot.NativeFileStaging ?? throw new InvalidOperationException("Retained node storage requires its restored file staging authority.");
        NativeBattleNodeSource rebound = source.RebindStaging(staging, NativeBattleRinoa88NodeResources.Address, NativeBattleRinoa88NodeResources.Length);
        return snapshot with { NativeEffectMemory = memory with { Rinoa88 = memory.Rinoa88 with { NodeData = rebound } } };
    }
}
