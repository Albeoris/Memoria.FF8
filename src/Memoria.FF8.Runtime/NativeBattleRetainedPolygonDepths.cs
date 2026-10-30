using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleRetainedPolygonDepths
{
    internal static NativeBattleGfGeometryBatch Capture(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { UseSourceDepths: false } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Depths(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static Vector4 Depths(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new InvalidDataException("Presentation capture requires the emitted native depth payload.")
        };
    }
}
