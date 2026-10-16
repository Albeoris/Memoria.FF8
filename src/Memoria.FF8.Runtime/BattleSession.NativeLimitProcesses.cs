namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private IEnumerable<BattleNativeAction> GetNativeRinoaLimitActions(BattleCombatant actor, byte crisis)
    {
        if ((definition.NativeSavedBattleFlagsKnownMask & 0x30) != 0x30)
        {
            throw new NotSupportedException("Native Rinoa Limit menu requires the saved1CFE97A bits0x10 and0x20.");
        }
        byte flags = CurrentNativeSavedBattleFlags();
        BattleNativeLimitDefinition limit = actor.NativeLimit!;
        if (limit.InvocationChoices.IsDefault || limit.InvocationChoices.Length != 2)
        {
            throw new InvalidDataException("Native Rinoa Limit menu requires both original kernel24 records.");
        }
        if ((flags & 0x10) == 0)
        {
            NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Combine requires the actual saved Angelo learned flags.");
            progress.Validate();
            ushort id = NativeBattleLimitRules.AngeloActionId(crisis, (byte)(progress.AngeloFlags >> 4));
            yield return FindInstalledNativeAction(19, id) with { Name = limit.InvocationChoices[0].Name };
        }
        if ((flags & 0x20) != 0)
        {
            yield return FindInstalledNativeAction(19, 4) with { Name = limit.InvocationChoices[1].Name };
        }
    }

    private static BattleCombatant NativePlayerSelectedTarget(BattleCombatant actor, BattleCombatant selected, byte targetFlags)
    {
        //4AB190 starts the ally cursor at its owner;4AA1D0 disables navigation for target mode2/3.
        return (targetFlags & 0x4C) == 8 && (targetFlags & 0x30) == 0x10 ? actor : selected;
    }

    private ushort NativeAutomaticActionTargetMask(byte targetFlags)
    {
        ushort modifiers = (ushort)(((targetFlags & 1) != 0 ? 0x4000 : 0) | ((targetFlags & 2) != 0 ? 0x2000 : 0));
        ushort targets;
        switch (targetFlags & 0x30)
        {
            case 0:
                targets = (targetFlags & 0x40) != 0 ? (ushort)0x80F8 : (ushort)0x8007;
                break;
            case 0x10:
                bool players = (targetFlags & 0x40) == 0;
                BattleCombatant? selected = ChooseNativeRandomActionTarget(players);
                targets = selected is null ? players ? (ushort)0 : (ushort)8 : (ushort)(1 << selected.NativeIndex);
                break;
            case 0x20:
                targets = 0x80FF;
                break;
            default:
                throw new NotSupportedException("Native automatic Limit target mode has no original483880 branch.");
        }
        return (ushort)(targets | modifiers);
    }
}
