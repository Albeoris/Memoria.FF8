using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static partial class ScenarioDiscovery
{
    public static bool TryValidateNativeEnemyRepeatOwner(BattleNativeAction action, byte selector, NativeBattleStatusAllocation? initialStatus, ReadOnlySpan<byte> initialization, out string? blocker)
    {
        blocker = null;
        if (!HasGuaranteedNativeSelfKoProfile(action, selector))
        {
            return true;
        }
        if (initialStatus is not { Saved: 0, Battle: 0 } || !ScenarioEnemyDiscovery.InitializationPreservesParticipantStatuses(initialization))
        {
            return true;
        }
        blocker = "NON_REPEATABLE_NATIVE_OWNER: the original self selector and guaranteed KO-only native action consume the initialized enemy owner on the first invocation. Ordinary revival excludes a KO enemy, and enemy HP reset or snapshot reconstruction cannot establish a second invocation. A separately proved native owner-recovery or alternate-caster recipe is required.";
        return false;
    }

    private static bool HasGuaranteedNativeSelfKoProfile(BattleNativeAction action, byte selector) => selector == 200 && action.AttackType == 2 && action.Power == 0 && action.Statuses0 == 1 && action.Statuses1 == 0 && action.Hit == 255 && action.StatusAttack == 255 && action.AttackFlags == 3;
}
