using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect251TrailBirths
{
    private static readonly ConditionalWeakTable<object, Dictionary<(uint Address, int Ring), (long TaskGeneration, int Age)>> samples = new();

    internal static void Observe(NativeBattleEffectArena storage, uint address, NativeBattleModulePoolTask task, int ring, int age)
    {
        Dictionary<(uint Address, int Ring), (long TaskGeneration, int Age)> births = samples.GetValue(storage.PresentationStorageIdentity, _ => []);
        long generation = NativeBattlePilotVisualLifetimes.Identity(251, task).Generation;
        if (!births.TryGetValue((address, ring), out var previous) || previous.TaskGeneration != generation || previous.Age != age)
        {
            NativeBattlePilotVisualLifetimes.Allocated(storage, address, ring);
            births[(address, ring)] = (generation, age);
        }
    }

    internal static NativeBattlePilotVisualIdentity Identity(NativeBattleEffectArena storage, uint address, NativeBattleModulePoolTask task, int firstRing, int lastRing)
    {
        long first = NativeBattlePilotVisualLifetimes.Identity(251, storage, address, firstRing).Generation;
        long last = NativeBattlePilotVisualLifetimes.Identity(251, storage, address, lastRing).Generation;
        return NativeBattlePilotVisualLifetimes.Identity(251, task) with { ResourceGeneration = unchecked(first * 397 ^ last) };
    }
}
