using static Memoria.FF8.Runtime.BattlePresentationProcess;
namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static uint? ReadExpandedHostedShotCallback(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        return (command.NativeLimitCallback, header.Command) switch
        {
            (NativeLimitCallback.ShotOpening, 14) => 0x48D1A0u,
            (NativeLimitCallback.ShotEffect, 237) => 0x4AD7C0u,
            (NativeLimitCallback.ShotEnding, 238) => null,
            (NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEffect or NativeLimitCallback.ShotEnding, _) => throw new InvalidDataException("Hosted Shot header differs from its timed input command."),
            _ => null
        };
    }

    private void StageExpandedHostedShotReadiness(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        if (ReadExpandedHostedShotCallback(command, header) is uint callback)
        {
            StageExpandedHostedControl(new HostedDispatchControl.ShotReadiness(callback));
        }
    }

    private HostedShotFrameOwner? CaptureExpandedHostedShotOwner(BattleModelRoster roster)
    {
        if (expandedHostedActionToken is not long token || expandedHostedEvent is null ||
            CurrentExpandedHostedInvocation is not { } invocation || ReadExpandedHostedShotCallback(invocation.Command, invocation.Header) is not uint callback)
        {
            return null;
        }
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleModelOwner owner = RequirePresentationModelOwner(roster, actor);
        if (nativeShotProcesses.Count == 1 && nativeShotProcesses.TryGetValue(actor.Id, out BattleNativeShotState? state) && state.ActionId == invocation.Command.NativeAction?.ActionId)
        {
            return new(owner, actor.Id, token, callback, state.Phase, state);
        }
        if (nativeShotProcesses.Count == 0 && !CanNativeBattleActorAct(actor))
        {
            return new(owner, actor.Id, token, callback, null);
        }
        return null;
    }

    private void ApplyExpandedHostedShotReadiness(NativeBattleModelLifecycleServices services, NativeBattleShotReadinessReached receipt, long? actionToken)
    {
        HostedShotFrameOwner shot = receipt.Shot;
        BattleCombatant actor = RequireEmittingHostedActor(services, receipt);
        if (shot != services.Shot || shot.ActionToken != actionToken || shot.ActionToken != expandedHostedActionToken ||
            expandedHostedEvent is not { } hostedEvent || hostedEvent.Command.ActorId != actor.Id || !ReferenceEquals(hostedEvent.ActivePresentation, ActiveAction) ||
            CurrentExpandedHostedInvocation is not { } invocation || invocation.Header.Source != shot.Owner ||
            ReadExpandedHostedShotCallback(invocation.Command, invocation.Header) != shot.Callback)
        {
            throw new InvalidDataException("Native Shot readiness does not belong to the current hosted action.");
        }
        if (shot.Phase is null && nativeShotProcesses.Count == 0 && !CanNativeBattleActorAct(actor))
        {
            return;
        }
        BattleNativeShotPhase expected = shot.Callback == 0x48D1A0 ? BattleNativeShotPhase.Opening : BattleNativeShotPhase.AwaitingEffect;
        if (nativeShotProcesses.Count != 1 || !nativeShotProcesses.TryGetValue(actor.Id, out BattleNativeShotState? state) ||
            state.ActionId != invocation.Command.NativeAction?.ActionId || state.Phase != shot.Phase || state.Phase != expected && state.Phase != BattleNativeShotPhase.Ending)
        {
            throw new InvalidDataException("Native Shot readiness does not match its current widget phase.");
        }
    }

    private NativeBattleShotWidgetFacts? ReadNativeShotWidgetFacts()
    {
        if (nativeShotProcesses.Count != 1)
        {
            return null;
        }
        (string actorId, BattleNativeShotState state) = nativeShotProcesses.Single();
        return new(CreatePresentationModelOwner(GetActor(actorId)), actorId, state);
    }

    private void ApplyNativeShotWidgetUpdate(NativeBattleShotWidgetUpdated update)
    {
        NativeBattleShotWidgetFacts facts = update.Facts;
        NativeBattleShotWidgetStep step = update.Step;
        if (!nativeShotProcesses.TryGetValue(facts.ActorId, out BattleNativeShotState? live) ||
            live != facts.State || CreatePresentationModelOwner(GetActor(facts.ActorId)) != facts.Owner)
        {
            throw new InvalidDataException("Shot window service lost its current logical input owner.");
        }
        BattleCombatant actor = GetActor(facts.ActorId);
        BattleAttackCommand? queued = null;
        byte ammunition = live.RemainingAmmo;
        if (step.Fired || step.QueueClosingCommand)
        {
            ushort command = step.Fired ? (ushort)237 : (ushort)238;
            BattleNativeAction action = FindInstalledNativeAction((byte)command, live.ActionId);
            ValidateNativeActionContract(action);
            queued = PrepareExpandedShot(actor, ReadExpandedShotTarget(live), action, step.Fired ? NativeLimitCallback.ShotEffect : NativeLimitCallback.ShotEnding);
            if (step.Fired)
            {
                _ = ConsumeNativeItem(live.ActionId + 101);
                ammunition = checked((byte)(ammunition - 1));
            }
        }
        BattleNativeShotPhase phase = step.Widget.Phase >= 8 ? BattleNativeShotPhase.Ending
            : step.Fired ? BattleNativeShotPhase.AwaitingEffect : step.Ready ? BattleNativeShotPhase.Ready : live.Phase;
        BattleTimedInputUiPhase ui = step.Unregistered ? BattleTimedInputUiPhase.Closed
            : step.Widget.Visible && phase != BattleNativeShotPhase.Ending ? BattleTimedInputUiPhase.Active
            : phase == BattleNativeShotPhase.Ending ? BattleTimedInputUiPhase.Closing : BattleTimedInputUiPhase.Opening;
        nativeShotProcesses[facts.ActorId] = live with { Widget = step.Widget, RemainingTimer = step.Widget.RemainingTimer, RemainingAmmo = ammunition, FireRequested = live.FireRequested && !step.Fired && phase != BattleNativeShotPhase.Ending, Phase = phase, Ui = new(ui) };
        if (queued is not null)
        {
            PublishExpandedShot(actor, queued);
        }
        if (step.Unregistered && live.ClosingCommandCompleted)
        {
            nativeShotProcesses.Remove(facts.ActorId);
            nativeLimitLevels.Remove(facts.ActorId);
        }
    }
}
