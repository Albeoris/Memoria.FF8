using System.Runtime.CompilerServices;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleAdditional182Visuals
{
    private static readonly ConditionalWeakTable<object, Dictionary<uint, (long TaskGeneration, short Age)>> history = new();

    internal static void Recorded(NativeBattleAdditional182Frame frame, NativeBattleModulePoolTask task, uint row, short age)
    {
        Dictionary<uint, (long TaskGeneration, short Age)> rows = history.GetValue(frame.State.Globals.PresentationStorageIdentity, _ => []);
        long generation = NativeBattlePilotVisualLifetimes.Identity(182, task).Generation;
        if (!rows.TryGetValue(row, out var previous) || previous != (generation, age))
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, row, 0);
            rows[row] = (generation, age);
        }
    }

    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Depths(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static System.Numerics.Vector4 Depths(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length switch
        {
            0 => System.Numerics.Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new NotSupportedException("Capture requires its actual empty or four-lane native depth payload.")
        };
    }

    internal static NativeBattlePilotVisualIdentity History(NativeBattleAdditional182Frame frame, NativeBattleModulePoolTask task, uint row)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(182, task);
        return NativeBattlePilotVisualLifetimes.Identity(182, frame.State.Globals, row, 0) with { ResourceGeneration = unchecked(owner.Generation * 397 ^ 0xD3AE78u) };
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleAdditional182Frame frame, NativeBattleModulePoolTask task, uint bank, int index, uint source)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(182, task);
        return NativeBattlePilotVisualLifetimes.Identity(182, frame.State.Globals, bank, index) with { ResourceGeneration = unchecked(owner.Generation * 397 ^ source) };
    }
}
