using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect321Presentation
{
    internal static NativeBattlePilotVisualIdentity Row(NativeBattleEffect321Frame frame, NativeBattleModulePoolTask task, int slot, uint source)
    {
        long parent = NativeBattlePilotVisualLifetimes.Identity(321, task).Generation;
        return NativeBattlePilotVisualLifetimes.Identity(321, frame.State.Storage, NativeBattleEffect321Records.First, slot) with { ResourceGeneration = unchecked(parent * 397 ^ source) };
    }

    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { PilotGeometry: { } geometry } primitive)
            {
                return attempt;
            }
            System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Capture requires its actual empty or four-lane native depth payload.")
            };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
        }).ToImmutableArray() };
    }
}
