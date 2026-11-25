namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool SupportsPlayerLimitSelection(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return AssignedNativeLimitFamilies(actor).Any(entry => entry.Definition.Command is 5 or 11 or 14 or 15 or 16 ||
            entry.Definition.Command == 19 && (HasExpandedCombine(actor) || HasExpandedAngelWing(actor)) ||
            entry.Definition.CharacterId is >= 6 and <= 10 && SupportsExpandedLimit(FindInstalledNativeAction(entry.Definition.Command, NativeBattleLimitRules.GuestActionId(entry.Definition.CharacterId))));
    }

    private static bool SupportsExpandedLimit(BattleNativeAction action)
    {
        if (action.KernelSection == 25)
        {
            return SupportsExpandedCombine(action) || IsExpandedAngelWingLimit(action);
        }
        // Commands that require another invocation process never enter this route.
        return action.KernelSection is 18 or 19 && action.HitCount > 0 && action.TargetFlags is byte flags && (flags & 0xB3) is 0 or 0x10 &&
            (action.AttackType == 1 || action.HitCount == 1 && action.AttackType is 2 or 8 or 25 or 26) &&
            ((action.Statuses1 & 0x10000) == 0 || action is { KernelSection: 19, AttackType: 2, Power: 0, HitCount: 1, Statuses0: 0, Statuses1: 0x10000 } && (flags & 0x70) == 0x50);
    }

    private static BattleActionDefinition ExpandedLimitDefinition(BattleCombatant actor, BattleNativeAction native)
    {
        if (actor.NativeLimit?.Command == 14 && IsExpandedShotAction(native))
        {
            byte flags = (native.TargetFlags!.Value & 0x10) != 0 ? (byte)0x50 : (byte)0x40;
            return CreateNativePlayerActionChoice(BattleAbility.LimitBreak, native, flags, native.Name);
        }
        if (actor.NativeLimit?.Command != native.Command || !SupportsExpandedLimit(native) && !SupportsSlotsAction(native))
        {
            throw new NotSupportedException("The expanded battle cannot execute this Limit's native invocation or contact contract.");
        }
        return CreateNativePlayerActionChoice(BattleAbility.LimitBreak, native, native.TargetFlags!.Value, native.Name);
    }

    private void QueueExpandedPlayerAction(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action, byte sequence)
    {
        SubmitExpandedPlayerAction(actor, action, PrepareExpandedPlayerAction(actor, target, action, sequence));
    }

    private bool IsExpandedPlayerActionTarget(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action, byte? bindingTargetFlags = null)
    {
        BattleNativeAction native = action.NativeAction!;
        return IsValidSelectedActionTarget(actor, action, target, bindingTargetFlags) &&
            (!IsNativeLevelCommand(native) || CanRebindExpandedEnemyLevel(target, ProjectNativeLevel(native, target.Level))) &&
            (!IsExpandedTargetedSpecial(native) || CanTargetExpandedSpecial(native, target));
    }

    private BattleAttackCommand PrepareExpandedPlayerAction(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action, byte sequence, byte? bindingTargetFlags = null, byte? formSequence = null)
    {
        BattleNativeAction native = action.NativeAction!;
        ValidateNativeActionContract(native);
        if (SupportsExpandedCombine(native))
        {
            target = SelectExpandedCombineTarget(action) ?? throw new InvalidOperationException("Combine requires an available recipient on its original side.");
        }
        if (!IsExpandedPlayerActionTarget(actor, target, action, bindingTargetFlags))
        {
            throw new InvalidOperationException("The command requires a valid target for its installed targeting mode.");
        }
        byte nativeSequence = formSequence ?? NativeBattleActionGraphics.ResolvePlayerSequence(native.Command, sequence, native.ActionId, definition.NativeCommandSequences);
        byte targetFlags = bindingTargetFlags ?? native.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == native.Command).TargetFlags;
        BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, targetFlags);
        return new(actor.Id, target.Id, request, action.Ability, action.Id)
        {
            NativeQueuedCommand = native.Command,
            NativeAction = native,
            NativeSequence = nativeSequence
        };
    }

    private void SubmitExpandedPlayerAction(BattleCombatant actor, BattleActionDefinition action, BattleAttackCommand command)
    {
        OnNativePlayerCommandSubmitted(actor, command.NativeQueuedCommand!.Value);
        actions.Enqueue(command);
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        Log("Queued", actor.Id, command.TargetId, 0, $"{actor.Name}: {action.Name} queued.");
    }
}
