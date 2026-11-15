namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool SupportsPlayerLimitSelection(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return actor.NativeLimit is { } limit && (!UsesExpandedRoster || limit.Command is 5 or 11 or 14 or 15 or 16 ||
            HasExpandedCombine(actor) || HasExpandedAngelWing(actor) || actor.ModelId is >= 6 and <= 10 && SupportsExpandedLimit(FindInstalledNativeAction(limit.Command, NativeBattleLimitRules.GuestActionId(actor.ModelId))));
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
        if (actor.NativeLimit?.Command != native.Command || !SupportsExpandedLimit(native) && !SupportsExpandedSlotsAction(native))
        {
            throw new NotSupportedException("The expanded battle cannot execute this Limit's native invocation or contact contract.");
        }
        return CreateNativePlayerActionChoice(BattleAbility.LimitBreak, native, native.TargetFlags!.Value, native.Name);
    }

    private void QueueExpandedPlayerAction(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action, byte sequence)
    {
        BattleNativeAction native = action.NativeAction!;
        ValidateNativeActionContract(native);
        if (SupportsExpandedCombine(native))
        {
            target = SelectExpandedCombineTarget(action) ?? throw new InvalidOperationException("Combine requires an available recipient on its original side.");
        }
        if (!IsValidSelectedActionTarget(actor, action, target) || IsNativeLevelCommand(native) && !CanRebindExpandedEnemyLevel(target, ProjectNativeLevel(native, target.Level)) ||
            IsExpandedTargetedSpecial(native) && !CanTargetExpandedSpecial(native, target))
        {
            throw new InvalidOperationException("The command requires a valid target for its installed targeting mode.");
        }
        byte nativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(native.Command, sequence, native.ActionId, definition.NativeCommandSequences);
        OnNativePlayerCommandSubmitted(actor, native.Command);
        byte targetFlags = native.TargetFlags ?? actor.NativeCommands.Single(binding => binding.Command == native.Command).TargetFlags;
        BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, targetFlags);
        actions.Enqueue(new(actor.Id, target.Id, request, action.Ability, action.Id)
        {
            NativeQueuedCommand = native.Command,
            NativeAction = native,
            NativeSequence = nativeSequence
        });
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: {action.Name} queued.");
    }
}
