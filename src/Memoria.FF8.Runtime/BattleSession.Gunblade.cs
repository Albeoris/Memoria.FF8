namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleGunbladeMode GunbladeMode { get; private set; }
    public bool CanTriggerGunblade => NativeGraphics is not null ? CanTriggerNativeWidget : CanTriggerNativeRenzokuken || Outcome == BattleOutcome.Active && GunbladeMode == BattleGunbladeMode.Manual && ActiveAction is { Phase: BattleActionPhase.Windup, GunbladeTriggerTiming: null } action && IsGunbladeAction(byId[action.ActorId], action);
    public bool GunbladeTriggerWindowOpen => NativeGraphics is not null ? NativeTriggerWindowOpen : nativeRenzokukenProcesses.Count != 0 ? NativeRenzokukenBarVisible && NativeRenzokukenTriggerWindowOpen : CanTriggerGunblade && NativeGunbladeAttack.TriggerSucceeded(ProjectGunbladeTiming(ActiveAction!.RemainingFrames));

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
        if (NativeGraphics is { } graphics)
        {
            if (!CanTriggerNativeWidget)
            {
                return false;
            }
            bool succeeded = NativeTriggerWindowOpen;
            NativeGraphics = graphics with { TriggerWidget = graphics.TriggerWidget! with { PendingTrigger = true } };
            return succeeded;
        }
        if (nativeRenzokukenProcesses.Count != 0)
        {
            return TriggerNativeRenzokuken();
        }
        if (!CanTriggerGunblade)
        {
            return false;
        }
        BattleActionPresentation action = ActiveAction!;
        short timing = ProjectGunbladeTiming(action.RemainingFrames);
        ActiveAction = action with { GunbladeTriggerTiming = NativeGunbladeAttack.TriggerSucceeded(timing) ? timing : (short)1 };
        return NativeGunbladeAttack.TriggerSucceeded(timing);
    }

    private bool CanTriggerNativeWidget => Outcome == BattleOutcome.Active && NativeGraphics?.TriggerWidget is { ClosingPhase: 0, PendingTrigger: false } widget && widget.Input.TriggerTiming == 0 && (definition.LimitProgress?.Options978 is ushort options ? (options & 1) == 0 : throw new NotSupportedException("Native trigger input requires its saved options WORD.")) && (nativeRenzokukenProcesses.Count != 0 || GunbladeMode == BattleGunbladeMode.Manual);

    private bool NativeTriggerWindowOpen => CanTriggerNativeWidget && NativeGraphics!.TriggerWidget is { } widget && widget.Input.Phase is 4 or 6 && NativeGunbladeAttack.TriggerSucceeded(checked((short)(widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[widget.Input.ContactIndex] + 5)));

    private static bool IsGunbladeAttack(BattleCombatant actor, BattleAbility ability)
    {
        // Native48FE20 defers ordinary attacks for Squall0 and Seifer6 to485160/attacktype10.
        return ability == BattleAbility.Attack && actor.IsPlayer && actor.ModelId is 0 or 6 && actor.WeaponId is not null;
    }

    private static bool IsGunbladeAction(BattleCombatant actor, BattleActionPresentation action)
    {
        return actor.IsPlayer && actor.ModelId is 0 or 6 && actor.WeaponId is not null && (action.Ability == BattleAbility.Attack || action.NativeAction?.Command is 1 or 12 or 28 or 243 or 248 or 253);
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
        //485160 overwrites the single-hit trigger WORD with1 under Blind, without an ordinary accuracy roll.
        short timing = ReadGunbladeTiming(actor, action);
        if (NativeGunbladeAttack.TriggerSucceeded(timing))
        {
            Log("GunbladeTrigger", actor.Id, target.Id, 0, $"{actor.Name}: Gunblade trigger.");
        }
        return timing;
    }

    private short ReadGunbladeTiming(BattleCombatant actor, BattleActionPresentation action)
    {
        if (action.NativeAction?.Command is 251 or 253 or 248 or 243 && action.GunbladeTriggerTiming is short supplied)
        {
            return supplied;
        }
        if (action.NativeAction?.Command == 251)
        {
            throw new InvalidDataException("Native Renzokuken contact lacks its input-owned timing WORD.");
        }
        if (HasStatus(actor, BattleStatusKind.Blind))
        {
            return 1;
        }
        return GunbladeMode == BattleGunbladeMode.Automatic ? (short)25 : action.GunbladeTriggerTiming ?? 0;
    }
}
