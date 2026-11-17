using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public ImmutableArray<byte?> NativeElementAttack => nativeElementAttack;

    private void ReprojectNativePlayerStatistics(BattleCombatant actor)
    {
        actor.ReprojectNativePlayerStatistics();
        if (actor.IsPlayer && actor.NativePlayerStatRules is not null && actor.NativeActorIndex is int slot && slot is >= 0 and < NativeBattleSlots.AllyCount)
        {
            // Original48B310 also writes the retained BD5 byte for this physical ally row.
            nativeElementAttack = nativeElementAttack.SetItem(slot, checked((byte)actor.ElementAttack));
        }
    }

    private ImmutableArray<byte?> nativeElementAttack = [null, null, null, null, null, null, null];

    private void RestoreNativeElementAttack(ImmutableArray<byte?> bytes)
    {
        if (bytes.IsDefault || bytes.Length != 7)
        {
            throw new InvalidDataException("Native participant BD5 element storage requires exactly seven nullable physical bytes.");
        }
        nativeElementAttack = bytes;
    }
}
