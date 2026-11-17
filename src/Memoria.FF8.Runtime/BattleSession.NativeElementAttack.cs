using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public ImmutableArray<byte?> NativeElementAttack => nativeElementAttack;

    private byte ReadNativeElementAttack(BattleCombatant actor)
    {
        if (actor.NativeActorIndex is not int slot)
        {
            throw new InvalidDataException("Native48FE20 element source lies outside its seven physical participant slots.");
        }
        return nativeElementAttack[slot] ?? throw new NotSupportedException("Native48FE20 requires the retained participant BYTE BD5 element attack; migrate the unknown physical byte explicitly.");
    }

    private void PublishNativeElementAttack(BattleCombatant actor)
    {
    }

    private void ReprojectNativePlayerStatistics(BattleCombatant actor)
    {
        actor.ReprojectNativePlayerStatistics();
        PublishNativeElementAttack(actor);
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
