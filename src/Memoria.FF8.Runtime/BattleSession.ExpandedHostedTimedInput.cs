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
            return new(owner, actor.Id, token, callback, state.Phase);
        }
        if (nativeShotProcesses.Count == 0 && !CanNativeBattleActorAct(actor))
        {
            return new(owner, actor.Id, token, callback, null);
        }
        return null;
    }

    private void EmitExpandedHostedShotReadiness(uint callback)
    {
        NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted Shot readiness requires current frame facts.");
        if (services.Shot is not { } shot || shot.Callback != callback)
        {
            throw new InvalidDataException("Native Shot readiness requires its single current hosted widget owner.");
        }
        services.Emit(new NativeBattleShotReadinessReached(shot));
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
        if (state.Phase != BattleNativeShotPhase.Ending)
        {
            nativeShotProcesses[actor.Id] = state with { Phase = BattleNativeShotPhase.Ready };
        }
    }
}
