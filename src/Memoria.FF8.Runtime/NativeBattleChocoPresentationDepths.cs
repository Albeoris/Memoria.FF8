using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleChocoPresentationDepths
{
    internal static NativeBattleModulePacketBatch Capture(NativeBattleModulePacketBatch batch)
    {
        return batch with { Primitives = batch.Primitives.Select(item => item with { Primitive = Capture(item.Primitive) }).ToImmutableArray() };
    }

    internal static NativeBattleGfGeometryBatch Capture(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = Capture(primitive) } : attempt).ToImmutableArray() };
    }

    private static NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.PilotGeometry is { } visual ? primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } : primitive;
    }
}
