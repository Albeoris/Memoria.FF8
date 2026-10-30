using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleMadRush273Presentation
{
    internal static NativeBattlePilotVisualIdentity MeshIdentity(NativeBattleModulePoolTask task, uint source, int nodeOffset)
    {
        return NativeBattlePilotVisualLifetimes.Identity(273, task, nodeOffset) with { ResourceGeneration = source };
    }

    internal static NativeBattlePilotVisualIdentity ParticleIdentity(NativeBattleModulePoolTask task, uint source)
    {
        return NativeBattlePilotVisualLifetimes.Identity(273, task) with { ResourceGeneration = source };
    }

    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => attempt.Primitive is { Words.Length: > 2, PilotGeometry: { Identity.Effect: 273, UseSourceDepths: false } }))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { Words.Length: > 2, PilotGeometry: { Identity.Effect: 273, UseSourceDepths: false } } primitive ? attempt with { Primitive = Retain(primitive) } : attempt).ToImmutableArray() };
    }

    private static NativeBattleGeometryPrimitive Retain(NativeBattleGeometryPrimitive primitive)
    {
        Vector4 depths = primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new NotSupportedException("Mad Rush capture requires its actual empty or four-lane native depth payload.")
        };
        return primitive with { PilotGeometry = primitive.PilotGeometry! with { SourceDepths = depths, UseSourceDepths = true } };
    }
}
