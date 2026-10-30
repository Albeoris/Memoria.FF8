using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect76Presentation
{
    internal static NativeBattleGfGeometryBatch FinalizeCapturedBatch(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(FinalizeAttempt).ToImmutableArray() };
    }

    private static NativeBattleGfGeometryAttempt FinalizeAttempt(NativeBattleGfGeometryAttempt attempt)
    {
        if (attempt.Primitive is not { PilotGeometry: { Identity.Effect: 76, UseSourceDepths: false } geometry } primitive)
        {
            return attempt;
        }
        System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
        {
            0 => System.Numerics.Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new InvalidDataException("Effect76 capture requires its actual empty or four-lane native depth payload.")
        };
        return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
    }
}
