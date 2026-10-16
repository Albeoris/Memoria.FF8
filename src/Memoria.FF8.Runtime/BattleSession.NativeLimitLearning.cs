namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PrepareNativeLimitCommand(BattleAttackCommand command)
    {
        if (command.NativeQueuedCommand != 16 || command.NativeAction is not { ActionId: >= 51 and <= 56 } magic)
        {
            return;
        }
        //48D200 case16 sets BYTE1CFE771 before the selected magic's effect/animation begins.
        byte before = nativeScriptState.LimitSpecialFlagsBefore ?? definition.LimitProgress?.SpecialMagicFlags ?? throw new NotSupportedException("Special Slots magic requires the actual saved learned BYTE1CFE771 owner.");
        byte mask = (byte)(nativeScriptState.LimitSpecialSetMask | (1 << (magic.ActionId - 51)));
        nativeScriptState = nativeScriptState with { LimitSpecialFlagsBefore = before, LimitSpecialFlagsAfter = (byte)(before | mask), LimitSpecialSetMask = mask };
    }
}
