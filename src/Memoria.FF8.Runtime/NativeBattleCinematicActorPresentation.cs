using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorPresentation
{
    public static NativeBattlePilotVisualIdentity Object(NativeBattlePilotVisualIdentity identity, uint clone, int objectIndex, bool linked, long source)
    {
        return identity with { Primitive = unchecked(((int)clone * 397 + objectIndex) * 2 + (linked ? 1 : 0)), ResourceGeneration = source };
    }

    public static NativeBattleGfGeometryBatch NativeDepths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = primitive.VertexPayload4.Length == 4 ? new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) : Vector4.Zero, UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }
}
