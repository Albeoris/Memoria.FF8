using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleOffsetBodyVisuals
{
    internal static NativeBattlePilotVisualIdentity At(ushort effect, ImmutableArray<NativeBattleEffectTaskPool> pools, uint address, uint source, uint selected = 0, int primitive = 0)
    {
        foreach (NativeBattleEffectTaskPool pool in pools)
        {
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < (uint)(pool.Stride * pool.Slots.Length))
            {
                NativeBattleModulePoolTask owner = pool.Slots[checked((int)(offset / pool.Stride))];
                return NativeBattleSelectedProgrammeVisuals.Resource(NativeBattleSelectedProgrammeVisuals.Workspace(effect, owner, address, source, primitive), source, selected);
            }
        }
        throw new InvalidDataException("Selected offset-body presentation source leaves its actual task banks.");
    }
    internal static NativeBattleGfGeometryBatch FixedQuad(NativeBattleGfGeometryBatch batch, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        if (pilotIdentity is not { } identity)
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = NativeBattleSelectedProgrammeVisuals.Discrete(primitive, identity with { Primitive = unchecked(identity.Primitive * 397 + attempt.ByteOffsetRelative) }) } : attempt).ToImmutableArray() };
    }

}
