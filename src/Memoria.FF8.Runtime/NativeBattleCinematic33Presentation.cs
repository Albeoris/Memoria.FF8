using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCinematic33Presentation
{
    internal static bool Owns(ushort effect) => effect == 331;

    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 331 } visual } primitive && primitive.VertexPayload4.Length == 4 ? attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }
}
