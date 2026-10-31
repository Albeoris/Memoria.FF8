using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Metadata for the selected task, actual programme workspace and selected source.</summary>
public static class NativeBattleActor32Presentation
{
    public static NativeBattlePilotVisualIdentity? Resource(NativeBattlePilotVisualIdentity? identity, uint source, uint vertices = 0)
    {
        if (identity is not { Effect: 286 or 289 } selected)
        {
            return null;
        }
        return selected with { ResourceGeneration = Mix(Mix(selected.ResourceGeneration, source), vertices) };
    }

    public static NativeBattlePilotVisualIdentity? Node(NativeBattlePilotVisualIdentity? identity, NativeBattleEffectArena arena, uint workspace, uint source, int node)
    {
        if (identity is not { Effect: 286 or 289 } selected)
        {
            return null;
        }
        NativeBattlePilotVisualIdentity birth = NativeBattlePilotVisualLifetimes.Identity(selected.Effect, arena, workspace, 0);
        return selected with { Primitive = node, ResourceGeneration = Mix(Mix(Mix(selected.ResourceGeneration, birth.Generation), workspace), source) };
    }

    public static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 286 or 289 } visual } primitive && primitive.VertexPayload4.Length == 4 ? attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static long Mix(long value, long part) => unchecked((value ^ part) * 1099511628211L);
}
