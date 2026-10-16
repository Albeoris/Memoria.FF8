namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleGunbladeMode GunbladeMode { get; private set; }
    public bool CanTriggerGunblade => Outcome == BattleOutcome.Active && GunbladeMode == BattleGunbladeMode.Manual && ActiveAction is { Phase: BattleActionPhase.Windup, GunbladeTriggerTiming: null } action && IsGunbladeAttack(byId[action.ActorId], action.Ability);
    public bool GunbladeTriggerWindowOpen => CanTriggerGunblade && NativeGunbladeAttack.TriggerSucceeded(ProjectGunbladeTiming(ActiveAction!.RemainingFrames));

    public void SetGunbladeMode(BattleGunbladeMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        GunbladeMode = mode;
    }

    public bool TriggerGunblade()
    {
        if (!CanTriggerGunblade)
        {
            return false;
        }
        BattleActionPresentation action = ActiveAction!;
        short timing = ProjectGunbladeTiming(action.RemainingFrames);
        ActiveAction = action with { GunbladeTriggerTiming = NativeGunbladeAttack.TriggerSucceeded(timing) ? timing : (short)1 };
        return NativeGunbladeAttack.TriggerSucceeded(timing);
    }

    private static bool IsGunbladeAttack(BattleCombatant actor, BattleAbility ability)
    {
        // Native48FE20 defers ordinary attacks for Squall0 and Seifer6 to485160/attacktype10.
        return ability == BattleAbility.Attack && actor.IsPlayer && actor.ModelId is 0 or 6 && actor.WeaponId is not null;
    }

    private static short ProjectGunbladeTiming(int remainingFrames)
    {
        // TODO(TD-009): Project the native4BA6C0 quarter-pose window onto scaffold contact. The native sequenceVM must own the actual distance-dependent clip contact
        const int nativeWindowEnd = 32;
        const int nativeQuarterPoseTicksPerSecond = 60;
        return checked((short)(nativeWindowEnd - remainingFrames * nativeQuarterPoseTicksPerSecond / FramesPerSecond));
    }

    private short ResolveGunbladeTiming(BattleCombatant actor, BattleCombatant target, BattleActionPresentation action)
    {
        // Automatic is host assistance supplying one successful native trigger, not the original probabilistic Auto option/4A7080 random owner.
        short timing = GunbladeMode == BattleGunbladeMode.Automatic ? (short)25 : action.GunbladeTriggerTiming ?? 0;
        if (NativeGunbladeAttack.TriggerSucceeded(timing))
        {
            Log("GunbladeTrigger", actor.Id, target.Id, 0, $"{actor.Name}: Gunblade trigger.");
        }
        return timing;
    }
}
