using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsModel
{
    public int? LinkedModelIndex140 { get; init; }

    public int? ReadLinkedModelIndex140()
    {
        if (LinkedModelIndex140 is int index)
        {
            return index is >= 0 and < 7 && HasLinkedModelRing140 == true ? index : throw new InvalidDataException("Native model DWORD140 contradicts its physical model pointer.");
        }
        return HasLinkedModelRing140 == false ? null : throw new NotSupportedException("Native model DWORD140 pointer has not been established.");
    }

    public NativeBattleGraphicsModel WriteLinkedModelIndex140(int? index)
    {
        if (index is < 0 or >= 7)
        {
            throw new NotSupportedException("Native linked-model pointer leaves its actual seven-model bank.");
        }
        return this with { LinkedModelIndex140 = index, HasLinkedModelRing140 = index is not null };
    }
}

public static class NativeBattleModelRings
{
    public static uint PreservedStatusMask50A000(ushort sourceFlags2) => (sourceFlags2 & 0x40) != 0 ? 0x831E0u : 0x21E35F0u;

    public static ImmutableArray<NativeBattleGraphicsModel> Bind90(ImmutableArray<NativeBattleGraphicsModel> models, int source, byte selector)
    {
        if (selector >= 16)
        {
            int target = Enumerable.Range(3, 4).FirstOrDefault(index => models[index].ModelId4 == selector, -1);
            return target < 0 ? models : models.SetItem(source, models[source].WriteLinkedModelIndex140(target));
        }
        if (selector >= 3)
        {
            return models.SetItem(source, models[source].WriteLinkedModelIndex140(selector));
        }
        List<int> ring = [];
        int current = source;
        do
        {
            if (ring.Contains(current))
            {
                throw new InvalidDataException("Native90 unlink chain does not return to its source.");
            }
            ring.Add(current);
            current = models[current].ReadLinkedModelIndex140() ?? throw new InvalidDataException("Native90 unlink dereferences a NULL link before closing its ring.");
        }
        while (current != source);
        foreach (int index in ring)
        {
            models = models.SetItem(index, models[index].WriteLinkedModelIndex140(null));
        }
        return models;
    }

    public static IEnumerable<int> Walk(ImmutableArray<NativeBattleGraphicsModel> models, int source)
    {
        HashSet<int> visited = [];
        int? current = source;
        while (current is int index)
        {
            if (!visited.Add(index))
            {
                if (index == source)
                {
                    yield break;
                }
                throw new InvalidDataException("Native model ring enters a cycle that does not return to its source.");
            }
            yield return index;
            current = models[index].ReadLinkedModelIndex140();
        }
    }
}
