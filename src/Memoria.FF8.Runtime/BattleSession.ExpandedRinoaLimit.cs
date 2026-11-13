namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasExpandedCombine(BattleCombatant actor)
    {
        return actor.NativeLimit is { Command: 19, InvocationChoices.Length: 2 } &&
            (definition.NativeSavedBattleFlagsKnownMask & 0x30) == 0x30 && (CurrentNativeSavedBattleFlags() & 0x10) == 0 &&
            definition.LimitProgress is { AngeloFlags: >= 0x10 };
    }

    private static bool SupportsExpandedCombine(BattleNativeAction action)
    {
        return action is { Command: 19, KernelSection: 25, AttackType: 1 or 2, TargetFlags: byte flags } &&
            (flags & 0xB3) is 0 or 0x10 && (action.Statuses1 & 0x2000000) == 0 && ExpandedBattleActionRules.SupportsContacts(action);
    }

    private BattleCombatant? SelectExpandedCombineTarget(BattleActionDefinition action)
    {
        byte flags = action.NativeAction!.TargetFlags!.Value;
        BattleSide side = (flags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies;
        BattleCombatant[] candidates = Roster(side).Where(candidate => !candidate.Untargetable && HasValidTargetLifeState(action, candidate)).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }
        return (flags & 0x30) == 0x10 ? candidates[NextRandomByte() % candidates.Length] : candidates[0];
    }
}
