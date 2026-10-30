using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect338Presentation
{
    internal static void Allocated(NativeBattleEffect338Frame frame, uint address)
    {
        NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena ?? throw new NotSupportedException("Effect338 capture requires its actual particle arena."), NativeBattleEffect338Profile.ArenaAddress, checked((int)((address - NativeBattleEffect338Profile.ArenaAddress) / 24)));
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleEffect338Frame frame, NativeBattleModulePoolTask task, int slot, uint source)
    {
        long parent = NativeBattlePilotVisualLifetimes.Identity(338, task).Generation;
        return NativeBattlePilotVisualLifetimes.Identity(338, frame.Memory.Arena ?? throw new NotSupportedException("Effect338 capture requires its actual particle arena."), NativeBattleEffect338Profile.ArenaAddress, slot) with { ResourceGeneration = unchecked(parent * 397 ^ source) };
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
