using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

/// <summary>Particle identities follow physical allocations independently of emitted packet order.</summary>
internal static class NativeBattleShivaVisualIdentity
{
    private sealed record Allocation(long TaskGeneration, long Generation);
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<int, Allocation>> allocations = new();

    internal static void Allocated(NativeBattleShivaEarlyTaskFrame frame, int slot, int record)
    {
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        long generation = NativeBattlePilotVisualLifetimes.Generation(task, slot);
        allocations.GetOrCreateValue(task.RequireOwner())[record] = new(generation, NativeBattlePilotVisualLifetimes.EpochFor(new object()));
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleShivaEarlyTaskFrame frame, int slot, int record, int primitive)
    {
        NativeBattleModulePoolTask task = frame.Pool.Slots[slot];
        long generation = NativeBattlePilotVisualLifetimes.Generation(task, slot);
        Dictionary<int, Allocation> records = allocations.GetOrCreateValue(task.RequireOwner());
        if (!records.TryGetValue(record, out Allocation? allocation) || allocation.TaskGeneration != generation)
        {
            Allocated(frame, slot, record);
            allocation = records[record];
        }
        return new(185, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(record / 0x1000 * 0x1000)), record % 0x1000 / 24, allocation.Generation, primitive);
    }

    internal static NativeBattleGfGeometryAttempt Capture(NativeBattleGfGeometryAttempt attempt, NativeBattlePilotVisualIdentity identity, IReadOnlyList<NativeBattleVector> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection)
    {
        if (attempt.Primitive is not { } primitive)
        {
            return attempt;
        }
        System.Numerics.Vector3 World(int index) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, vertices[index]), projection);
        return attempt with { Primitive = primitive with { PilotGeometry = new(identity, World(0), World(1), World(2), vertices.Count == 4 ? World(3) : default, checked((byte)vertices.Count)) } };
    }

    internal static int ClipTopology(IEnumerable<NativeBattleVector> vertices, NativeBattleShivaPlane plane)
    {
        int topology = 0;
        foreach (NativeBattleVector vertex in vertices)
        {
            short distance = NativeBattleShivaPlaneClipping.Distance460860(vertex, plane);
            topology = topology * 3 + Math.Sign(distance) + 1;
        }
        return topology;
    }
}
