using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleContextBodyPresentation
{
    internal static NativeBattleGfGeometryBatch CaptureDepths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { UseSourceDepths: false } visual, VertexPayload4.Length: 4 } primitive ? attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }
}
