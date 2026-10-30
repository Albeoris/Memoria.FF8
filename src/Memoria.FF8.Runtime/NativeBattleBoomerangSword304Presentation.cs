using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleBoomerangSword304Presentation
{
    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => attempt.Primitive?.PilotGeometry is { Identity.Effect: 304, UseSourceDepths: false }))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 304, UseSourceDepths: false } geometry } primitive && primitive.Words.Length > 2 ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Depths(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static Vector4 Depths(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new NotSupportedException("Capture requires its actual empty or four-lane native depth payload.")
        };
    }
}
