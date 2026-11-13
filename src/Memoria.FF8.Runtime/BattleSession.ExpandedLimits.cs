namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool SupportsPlayerLimitSelection(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return actor.NativeLimit is { } limit && (!UsesExpandedRoster || limit.Command == 15 ||
            actor.ModelId is >= 6 and <= 10 && SupportsExpandedLimit(FindInstalledNativeAction(limit.Command, NativeBattleLimitRules.GuestActionId(actor.ModelId))));
    }

    private static bool SupportsExpandedLimit(BattleNativeAction action)
    {
        // Commands that require another invocation process never enter this route.
        return action.KernelSection is 18 or 19 && action.HitCount == 1 && action.TargetFlags is byte flags && (flags & 0xB3) is 0 or 0x10 &&
            action.AttackType is 1 or 2 or 8 or 26 && (action.Statuses1 & 0x10000) == 0;
    }

    private bool CanPresentExpandedLimit(BattleNativeAction action, int recipients)
    {
        return recipients == 1 && definition.NativeEffectCatalog.Any(binding => binding.Effect == action.MagicId && binding.Entry == NativeBattleLaserEye71Profile.Entry);
    }

    private static BattleActionDefinition ExpandedLimitDefinition(BattleCombatant actor, BattleNativeAction native)
    {
        if (actor.NativeLimit?.Command != native.Command || !SupportsExpandedLimit(native))
        {
            throw new NotSupportedException("The expanded battle cannot execute this Limit's native invocation or contact contract.");
        }
        return CreateNativePlayerActionChoice(BattleAbility.LimitBreak, native, native.TargetFlags!.Value, native.Name);
    }

    private void QueueExpandedLimit(BattleCombatant actor, BattleCombatant target, BattleNativeAction native, byte sequence)
    {
        BattleActionDefinition action = ExpandedLimitDefinition(actor, native);
        ValidateNativeActionContract(native);
        if (!IsValidSelectedActionTarget(actor, action, target))
        {
            throw new InvalidOperationException("The Limit requires a valid target for its installed targeting mode.");
        }
        byte nativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(native.Command, sequence, native.ActionId, definition.NativeCommandSequences);
        OnNativePlayerCommandSubmitted(actor, native.Command);
        actions.Enqueue(new(actor.Id, target.Id, action.Ability, action.Id)
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
