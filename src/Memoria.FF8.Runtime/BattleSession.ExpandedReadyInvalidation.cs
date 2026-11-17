using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private ImmutableDictionary<string, NativeBattleStatusMirror>? expandedPartyReadyStatusMirrors;

    private void PublishExpandedReadyContact(BattleModelRoster roster, NativeBattleContactHalf half, string casterId)
    {
        if (PublishExpandedPartyPresentedContact(roster, half) is not { } actor)
        {
            return;
        }
        ImmutableDictionary<string, NativeBattleStatusMirror> mirrors = expandedPartyReadyStatusMirrors ?? throw new InvalidOperationException("Expanded readiness requires its initialized party status mirrors.");
        if (!mirrors.TryGetValue(actor.Id, out NativeBattleStatusMirror? prior))
        {
            throw new InvalidDataException("Expanded readiness lacks the allocated actor's previous status mirror.");
        }
        ushort saved = half.SavedStatuses4;
        uint battle = half.BattleStatuses8;
        if ((battle & 0x80000000) != 0)
        {
            ushort gfHp = RequireExpandedPartyPresentedRow(expandedPartyPresentation!, actor.Id).GuardianForceHitPoints ?? throw new NotSupportedException("Expanded readiness requires the summoned GF's actual cached HP WORD.");
            if (gfHp == 0 || (saved & 0x35) != 0 || (battle & 0x14000) != 0)
            {
                actor.NativeStatus1 &= 0x7FFFFFFF;
                actor.NativeCombatantFlags &= ~0x400u;
            }
            else if ((battle & 9) != 0)
            {
                actor.NativeCombatantFlags |= 0x400;
            }
            else
            {
                actor.NativeCombatantFlags &= ~0x400u;
            }
        }
        bool hasQueuedInput = actions.Any(command => IsExpandedQueuedReadyInput(command, actor.Id));
        NativeBattleReadyInvalidation decision = NativeBattleReadyInvalidations.EvaluateExpanded(saved, battle, hasQueuedInput, casterId == actor.Id, prior.Saved, prior.Battle);
        // The expanded command menu derives eligibility and focus by actor ID, without retaining the physical UI18 list.
        if (decision.ClearReadyBusyFlags)
        {
            actor.NativeCombatantFlags &= ~0xCu;
        }
        // 493840 observes B92/B1C before 47E2D0 refreshes it after queue and ejection handling.
        NativeBattleStatusMirror retained = actor.NativePresentationStatus;
        if (retained.Saved is null || retained.Battle is null)
        {
            throw new NotSupportedException("Expanded readiness requires retained B92/B1C status before refreshing the actor mirror.");
        }
        expandedPartyReadyStatusMirrors = mirrors.SetItem(actor.Id, retained);
        if (decision.RemoveQueuedCommands && hasQueuedInput)
        {
            RemoveExpandedQueuedReadyInputs(actor.Id);
        }
        if (decision.ResetGauge)
        {
            actor.Atb = 0;
            actor.IsBusy = false;
        }
        if ((battle & 0x10000) != 0)
        {
            EjectPartyOccupant(actor);
        }
        RefreshNativePresentationStatus(actor);
    }

    private bool IsExpandedQueuedReadyInput(BattleAttackCommand command, string actorId)
    {
        if (command.ActorId != actorId || command.NativeHeader is not null || command.RosterOperation is not null || command.NativePlayerReaction is not null)
        {
            return false;
        }
        return !(command.NativeLimitCallback is NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding && nativeShotProcesses.ContainsKey(actorId))
            && !(IsDuelCallback(command.NativeLimitCallback) && nativeDuelProcesses.ContainsKey(actorId));
    }

    private void RemoveExpandedQueuedReadyInputs(string actorId)
    {
        for (int remaining = actions.Count; remaining > 0; remaining--)
        {
            BattleAttackCommand command = actions.Dequeue();
            if (IsExpandedQueuedReadyInput(command, actorId))
            {
                RefundCancelledNativeItem(command);
                CancelUnstartedExpandedShot(command);
            }
            else
            {
                actions.Enqueue(command);
            }
        }
    }

    private void CancelUnstartedExpandedShot(BattleAttackCommand command)
    {
        string actorId = command.ActorId;
        if (command.NativeLimitCallback != NativeLimitCallback.ShotOpening ||
            !nativeShotProcesses.TryGetValue(actorId, out BattleNativeShotState? shot) || shot.Phase != BattleNativeShotPhase.Opening || shot.ActionId != command.NativeAction?.ActionId ||
            ActiveAction is { } active && active.ActorId == actorId && IsShotCallback(active.NativeLimitCallback) ||
            expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>().Any(invocation => invocation.Command.ActorId == actorId && IsShotCallback(invocation.Command.NativeLimitCallback)) ||
            actions.Any(other => other.ActorId == actorId && (other.NativeHeader is not null || other.RosterOperation is not null) && IsShotCallback(other.NativeLimitCallback)))
        {
            return;
        }
        nativeShotProcesses.Remove(actorId);
        nativeLimitLevels.Remove(actorId);
    }
}
