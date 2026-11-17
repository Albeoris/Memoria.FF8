namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleGunbladeMode GunbladeMode { get; private set; }
    public bool CanTriggerGunblade => Presentation is { } process && CanTriggerExpandedGunblade(process);
    public bool GunbladeTriggerWindowOpen => CanTriggerGunblade && IsNativeTriggerWindowOpen(Presentation!.TriggerWidget!);

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
        if (Presentation is not { } process || !CanTriggerExpandedGunblade(process))
        {
            return false;
        }
        NativeBattleTriggerWidget widget = process.TriggerWidget!;
        bool succeeded = IsNativeTriggerWindowOpen(widget);
        process.PressGunblade(expandedHostedActionToken!.Value, widget.Actor, widget);
        return succeeded;
    }

    private static bool IsNativeTriggerWindowOpen(NativeBattleTriggerWidget widget)
    {
        return widget.Input.Phase is 4 or 6 && NativeGunbladeAttack.TriggerSucceeded(checked((short)(widget.Input.PoseClock + widget.Input.SubframeClock - widget.Block.Timeline.InputThresholds[widget.Input.ContactIndex] + 5)));
    }

    private static bool IsGunbladeAttack(BattleCombatant actor, BattleAbility ability)
    {
        // Native48FE20 defers ordinary attacks for Squall0 and Seifer6 to485160/attacktype10.
        return ability == BattleAbility.Attack && actor.IsPlayer && NativeGunbladeAttack.HasTrigger(actor.ModelId) && actor.WeaponId is not null;
    }

    private static bool IsGunbladeAction(BattleCombatant actor, BattleActionPresentation action)
    {
        return actor.GuardianFormVitals is null && actor.IsPlayer && NativeGunbladeAttack.HasTrigger(actor.ModelId) && actor.WeaponId is not null && (action.Ability == BattleAbility.Attack || action.NativeAction?.Command is 1 or 12 or 28 or 243 or 248 or 253);
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
