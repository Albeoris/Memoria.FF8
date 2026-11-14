namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PrepareNativeLimitCommand(BattleAttackCommand command)
    {
        byte learnedShot = command.NativeAction is { } shot ? NativeBattleLimitLearningRules.ShotMask(shot) : (byte)0;
        if (command.NativeQueuedCommand == 237 && learnedShot != 0)
        {
            byte shotBefore = nativeScriptState.ShotFlagsBefore ?? definition.NativeShotAvailability?.Flags770 ?? throw new NotSupportedException("Shot dispatch requires its original saved BYTE1CFE770 owner.");
            byte shotMask = (byte)(nativeScriptState.ShotFlagsSetMask | learnedShot);
            nativeScriptState = nativeScriptState with { ShotFlagsBefore = shotBefore, ShotFlagsAfter = (byte)(shotBefore | shotMask), ShotFlagsSetMask = shotMask };
        }
        byte learnedSlots = command.NativeAction is { } slots ? NativeBattleLimitLearningRules.SlotsMask(slots) : (byte)0;
        if (command.NativeQueuedCommand != 16 || learnedSlots == 0)
        {
            return;
        }
        //48D200 case16 sets BYTE1CFE771 before the selected magic's effect/animation begins.
        byte before = nativeScriptState.LimitSpecialFlagsBefore ?? definition.LimitProgress?.SpecialMagicFlags ?? throw new NotSupportedException("Special Slots magic requires the actual saved learned BYTE1CFE771 owner.");
        byte mask = (byte)(nativeScriptState.LimitSpecialSetMask | learnedSlots);
        nativeScriptState = nativeScriptState with { LimitSpecialFlagsBefore = before, LimitSpecialFlagsAfter = (byte)(before | mask), LimitSpecialSetMask = mask };
    }
}
