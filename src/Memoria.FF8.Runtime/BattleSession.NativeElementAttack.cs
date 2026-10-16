using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableArray<byte?> nativeElementAttack = [null, null, null, null, null, null, null];
    public ImmutableArray<byte?> NativeElementAttack => nativeElementAttack;

    private byte ReadNativeElementAttack(BattleCombatant actor)
    {
        if (actor.NativeIndex is < 0 or >= 7)
        {
            throw new InvalidDataException("Native48FE20 element source lies outside its seven physical participant slots.");
        }
        return nativeElementAttack[actor.NativeIndex] ?? throw new NotSupportedException("Native48FE20 requires the retained participant BYTE BD5 element attack; migrate the unknown physical byte explicitly.");
    }

    private void PublishNativeElementAttack(BattleCombatant actor)
    {
        if (actor.NativeIndex is >= 0 and < 3 && actor.NativePlayerStatRules is not null)
        {
            //48B310 writes BD5 from the current junction projection; enemy48BBD0 leaves that byte retained.
            nativeElementAttack = nativeElementAttack.SetItem(actor.NativeIndex, checked((byte)actor.ElementAttack));
        }
    }

    private void ReprojectNativePlayerStatistics(BattleCombatant actor)
    {
        actor.ReprojectNativePlayerStatistics();
        PublishNativeElementAttack(actor);
    }

    private void RestoreNativeElementAttack(ImmutableArray<byte?> bytes)
    {
        if (bytes.IsDefault || bytes.Length != 7)
        {
            throw new InvalidDataException("Native participant BD5 element storage requires exactly seven nullable physical bytes.");
        }
        nativeElementAttack = bytes;
    }
}
