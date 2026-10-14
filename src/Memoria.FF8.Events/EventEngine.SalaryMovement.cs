using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Action? pendingNativePlayerInput;

    private bool NativePlayerWalking => !Scene.RunEnabled || ((Scene.ButtonInput?.Held ?? 0) & 0x10) != 0;

    private bool NativePlayerHasScriptedMode(ActorState actor) => actor.NativeMovementCompletion542 < 2 && actor.NativeScriptMovement is { Active: true, Suspended: false } || actor.NativeJump is { Active: true } || partyJoin?.Targets.Any(target => target.ObjectId == actor.ObjectId && actors[target.ObjectId].NativeMovementCompletion542 < 2) == true || partySplit?.Targets.Any(target => target.ObjectId == actor.ObjectId && actors[target.ObjectId].NativeMovementCompletion542 < 2) == true;

    private NativeSalaryResult? PrepareNativeFieldSalarySample()
    {
        if (services.SeeD is not NativeSeeDState state)
        {
            return null;
        }
        state.Validate();
        if (services.WorldControls is not NativeWorldControlState controls)
        {
            throw new NotSupportedException("Native salary sampling requires the executed DWORD104 owner.");
        }
        if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreNativeEventOwners || Scene.NativeSalaryMovementUnits is not (0 or 3 or 5))
        {
            throw new NotSupportedException("Native salary sampling requires the retained previous4789A0 result and a persistent current native field state.");
        }
        // Native529FF0 consumes the PREVIOUS body callback's scalar before VM.
        // Prepare the entire payout before changing the clock, PC or body.
        return NativeSalaryCalculator.Sample(state, services.Economy, services.CharacterVitals, Scene.NativeSalaryMovementUnits.Value, controls.EventFlags104);
    }

    private void ValidateNativePlayerInput(ActorState player)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || Scene.NativePartyTraceVersion != 1 || Scene.NativePlayerPosition is null)
        {
            throw new NotSupportedException("Ordinary player input requires the current native field body and preserved party trace; recreate the authentic journal.");
        }
        if (!NativePlayerHasScriptedMode(player) && (ResolveNativePlayerMovement is null || CanUseNativePlayerMovement?.Invoke(player) != true || Scene.NativePlayerEarlyRetryState is not (0 or 1)))
        {
            throw new NotSupportedException("Ordinary player input requires its certified current native motor and executed retry state before the callback.");
        }
    }

    private void StepNativePlayerInput(Action? input)
    {
        ActorState? leader = NativeCallbackActors().FirstOrDefault(IsNativePartyPlayerBody);
        if (leader is null || NativePlayerHasScriptedMode(leader))
        {
            // Native4789A0 skips the reset for a missing leader or nonzero572.
            return;
        }
        if (Scene.NativeSalaryMovementUnits is not null)
        {
            Scene.NativeSalaryMovementUnits = 0;
        }
        if (!IsUserControlEnabled || IsMovementBlockedByDialogue || services.IsMoviePlaying)
        {
            return;
        }
        if (input is null)
        {
            StepStationaryPartyTrace();
        }
        else
        {
            input();
        }
    }

    private void PublishNativePlayerMovementResult(bool accepted)
    {
        if (Scene.NativeSalaryMovementUnits is not null)
        {
            Scene.NativeSalaryMovementUnits = accepted ? NativePlayerWalking ? (byte)3 : (byte)5 : (byte)0;
        }
    }
}
