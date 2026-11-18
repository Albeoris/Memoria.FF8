using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeBattleTimedAction(BattleActionPresentation Command, int Contacts);

public sealed partial class BattleSession
{
    private NativeBattleTimedAction? nativeTimedAction;
    private bool suppressNativeImmediateCounter;

    private static void ValidateExpandedRenzokukenFinisher(BattleNativeAction finisher)
    {
        if (finisher is not { Command: 249, KernelSection: 5, ActionId: < 4, AttackType: 9, TargetFlags: 0x48 or 0x58, HitCount: > 0 })
        {
            throw new NotSupportedException("Expanded Renzokuken requires the installed hostile finisher formula and target mode.");
        }
    }

    private static BattleNativeAction NativeRenzokukenInitialAction(BattleCombatant actor, ushort magicId, ushort actionId)
    {
        return actor.NativeAttack! with { Command = 5, ActionId = actionId, KernelSection = -1, KernelIndex = -1, MagicId = magicId, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

    private void CompleteNativeTimedActionOwners(BattleCombatant actor, BattleActionPresentation command)
    {
        if (command.NativeQueuedCommand != 5)
        {
            throw new InvalidDataException("Native timed owner completion requires its original Renzokuken command5.");
        }
        //The retained timed owner is command5, not its251 contacts or250 finisher.
        //48D200's Silence rejection is exclusive to command2 and cannot reject this owner.
        CompleteNativeActionOwners(actor, command, silencedMagic: false);
    }

    private NativeBattleTimedAction? CaptureNativeTimedAction()
    {
        return nativeTimedAction;
    }

    private void LogRenzokukenBoundary(BattleActionPresentation command, string kind, string message)
    {
        BattleActionPresentation? previous = resolvingNativeAction;
        resolvingNativeAction = command;
        try
        {
            Log(kind, command.ActorId, command.TargetId, 0, message);
        }
        finally
        {
            resolvingNativeAction = previous;
        }
    }

    private void RestoreNativeTimedAction(NativeBattleTimedAction? saved, ExpandedHostedEvent? hostedEvent)
    {
        nativeTimedAction = null;
        nativeActionCommitDepth = 0;
        if (saved is null)
        {
            return;
        }
        if (saved.Command is null || saved.Command.NativeQueuedCommand != 5 || !byId.TryGetValue(saved.Command.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || !byId.ContainsKey(saved.Command.TargetId) || saved.Contacts < 0)
        {
            throw new InvalidDataException("Saved native timed command lacks its canonical actor, contact or reaction owners.");
        }
        bool ownsNativeGauge = false;
        if (actor.NativeAtb is { } gauge && OwnsHostedRenzokukenRestore(hostedEvent, actor.Id))
        {
            gauge.Validate();
            ownsNativeGauge = definition.NativeClockRules is not null && definition.NativeBattleSpeed is byte speed && gauge.Maximum == 4000 * (speed + 1);
        }
        if ((saved.Command is not { NativeAction: null, TargetRequest.Selection: BattleTargetSelection.Explicit { Actors.Length: 1 }, NativeRenzokuken.CrisisLevel: >= 1 and <= 4, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16 } || actor.Atb != 0 && !ownsNativeGauge))
        {
            throw new InvalidDataException("Saved expanded Renzokuken lacks its logical command5 transaction.");
        }
        if (hostedEvent?.Renzokuken is not null && !ReferenceEquals(saved.Command, hostedEvent.ActivePresentation))
        {
            throw new InvalidDataException("The private timed restoration lost the admitted hosted Renzokuken command.");
        }
        actor.IsBusy = true;
        nativeTimedAction = saved;
        nativeActionCommitDepth = 1;
    }
}
