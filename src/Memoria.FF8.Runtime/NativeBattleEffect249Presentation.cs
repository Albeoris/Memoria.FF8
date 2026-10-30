using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect249Presentation
{
    internal static NativeBattleGfGeometryBatch FinalizeCapturedBatch(int effect, byte drawKind, NativeBattleGfGeometryBatch batch)
    {
        if (effect != 249 || drawKind is not (2 or 5) || !batch.Attempts.Any(attempt => CapturedPolygon(attempt.Primitive)))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => CapturedPolygon(attempt.Primitive) ? attempt with { Primitive = RetainDepth(attempt.Primitive!) } : attempt).ToImmutableArray() };
    }

    private static bool CapturedPolygon(NativeBattleGeometryPrimitive? primitive)
    {
        return primitive is { Words.Length: > 2, PilotGeometry: { Identity.Effect: 249, UseSourceDepths: false } };
    }

    private static NativeBattleGeometryPrimitive RetainDepth(NativeBattleGeometryPrimitive primitive)
    {
        Vector4 depths = primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new NotSupportedException("Effect249 capture requires its actual empty or four-lane native depth payload.")
        };
        return primitive with { PilotGeometry = primitive.PilotGeometry! with { SourceDepths = depths, UseSourceDepths = true } };
    }
}
