namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private byte CurrentNativeSavedBattleFlags()
    {
        return nativeScriptState.SavedBattleFlagsAfter ?? definition.NativeSavedBattleFlags ?? throw new NotSupportedException("Native saved battle flag condition requires the current1CFE97A owner.");
    }

    private void CompleteNativeSavedBattleFlags()
    {
        UpdateNativeSavedBattleFlags(8, 2);
    }

    private byte RequireCompleteNativeSavedBattleFlags()
    {
        if (definition.NativeSavedBattleFlagsKnownMask != 255)
        {
            throw new NotSupportedException("Native opcode54 requires the complete saved1CFE97A BYTE before changing its flags.");
        }
        return CurrentNativeSavedBattleFlags();
    }

    private void SetNativeSavedBattleFlag(byte mask)
    {
        if (mask != 4)
        {
            throw new NotSupportedException("Saved battle flag setter requires an original Phoenix Pinion bit0x04 producer.");
        }
        UpdateNativeSavedBattleFlags(mask, 0);
    }

    private void UpdateNativeSavedBattleFlags(byte setMask, byte clearMask)
    {
        byte current = RequireCompleteNativeSavedBattleFlags();
        byte before = nativeScriptState.SavedBattleFlagsBefore ?? current;
        byte combinedSet = (byte)(nativeScriptState.SavedBattleFlagsSetMask | setMask);
        byte combinedClear = (byte)(nativeScriptState.SavedBattleFlagsClearMask | clearMask);
        byte after = NativeSavedBattleFlags.ApplyBattleEffects(before, combinedSet, combinedClear);
        nativeScriptState = nativeScriptState with { SavedBattleFlagsBefore = before, SavedBattleFlagsAfter = after, SavedBattleFlagsSetMask = combinedSet, SavedBattleFlagsClearMask = combinedClear };
    }

    private void ValidateNativeSavedBattleFlagEffects()
    {
        if (nativeScriptState.LimitSpecialFlagsBefore is byte specialBefore && definition.LimitProgress?.SpecialMagicFlags != specialBefore)
        {
            throw new InvalidDataException("Slots learning effects disagree with their battle-entry BYTE1CFE771 owner.");
        }
        if (nativeScriptState.SavedBattleFlagsBefore is byte before && (definition.NativeSavedBattleFlagsKnownMask != 255 || definition.NativeSavedBattleFlags != before))
        {
            throw new InvalidDataException("Saved battle flag effects disagree with the complete battle-entry BYTE owner.");
        }
    }
}
