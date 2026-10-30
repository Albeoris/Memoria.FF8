using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleMorph172Presentation
{
    internal static NativeBattlePilotVisualIdentity? Domain(NativeBattlePilotVisualIdentity? identity, uint source, int role)
    {
        return identity is { } parent ? parent with { Primitive = unchecked(parent.Primitive * 397 + role), ResourceGeneration = unchecked(parent.ResourceGeneration * 397 ^ source) } : null;
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleMorph172Frame frame, NativeBattleModulePoolTask task, int slot)
    {
        long parent = NativeBattlePilotVisualLifetimes.Identity(172, task).Generation;
        return NativeBattlePilotVisualLifetimes.Identity(172, frame.State.Globals, 0x239A908, slot) with { ResourceGeneration = unchecked(parent * 397 ^ 0xD61F8Cu) };
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
                _ => throw new NotSupportedException("Morph capture requires its actual empty or four-lane native depth payload.")
            };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = depths, UseSourceDepths = true } } };
        }).ToImmutableArray() };
    }
}
