using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleActor33Presentation
{
    public static NativeBattlePilotVisualIdentity? Resource(NativeBattlePilotVisualIdentity? identity, uint source, uint selected = 0)
    {
        if (identity is not { Effect: 316 or 318 or 319 or 324 } owner)
        {
            return null;
        }
        return owner with { ResourceGeneration = Mix(Mix(owner.ResourceGeneration, source), selected) };
    }

    public static NativeBattlePilotVisualIdentity? Row(NativeBattlePilotVisualIdentity? identity, NativeBattleEffectArena arena, uint row, uint source, int part = 0)
    {
        if (identity is not { Effect: 316 or 318 or 319 or 324 } owner)
        {
            return null;
        }
        NativeBattlePilotVisualIdentity birth = NativeBattlePilotVisualLifetimes.Identity(owner.Effect, arena, row, 0);
        return owner with { Primitive = part, ResourceGeneration = Mix(Mix(Mix(owner.ResourceGeneration, birth.Generation), row), source) };
    }

    public static NativeBattlePilotVisualIdentity? Workspace(NativeBattlePilotVisualIdentity? identity, ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, uint source, int node)
    {
        if (identity is not { Effect: 318 or 319 } owner)
        {
            return null;
        }
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            uint relative = unchecked(address - pool.BankAddress);
            if (relative < (uint)(pool.Stride * pool.Slots.Length))
            {
                NativeBattleModulePoolTask task = pool.Slots[checked((int)(relative / pool.Stride))];
                return NativeBattleSelectedProgrammeVisuals.Workspace(owner.Effect, task, address, source, node);
            }
        }
        throw new InvalidDataException("Selected programme presentation leaves its actual task workspace owner.");
    }

    public static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 316 or 318 or 319 or 324 } visual } primitive && primitive.VertexPayload4.Length == 4 ? attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static long Mix(long value, long part) => unchecked((value ^ part) * 1099511628211L);
}
