using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleShotVisualLifetimes
{
    private static readonly ConditionalWeakTable<object, Dictionary<(ushort Effect, int Offset), long>> generations = new();
    private static long nextGeneration;

    internal static void Allocated(ushort effect, NativeBattleModulePoolTask task, int offset)
    {
        Dictionary<(ushort Effect, int Offset), long> rows = generations.GetValue(task.RequireOwner(), _ => []);
        rows[(effect, offset)] = Interlocked.Increment(ref nextGeneration);
    }

    internal static NativeBattlePilotVisualIdentity Identity(ushort effect, NativeBattleModulePoolTask source, NativeBattlePilotVisualIdentity task, int offset)
    {
        Dictionary<(ushort Effect, int Offset), long> rows = generations.GetValue(source.RequireOwner(), _ => []);
        if (!rows.TryGetValue((effect, offset), out long generation))
        {
            generation = Interlocked.Increment(ref nextGeneration);
            rows.Add((effect, offset), generation);
        }
        return task with { Primitive = offset, ResourceGeneration = generation };
    }

    internal static NativeBattlePilotVisualIdentity? Resource(NativeBattlePilotVisualIdentity? identity, uint resource)
    {
        return identity is { } current ? current with { Primitive = unchecked(current.Primitive * 65537 + (int)resource) } : null;
    }
}
