namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAllocationControl([property: System.Text.Json.Serialization.JsonRequired] byte? SceneSlot, [property: System.Text.Json.Serialization.JsonRequired] bool ActionGate, [property: System.Text.Json.Serialization.JsonRequired] bool PauseGate);

public sealed partial class BattleSession
{
    private bool NativeScriptAllocationPending => nativeScriptState.AllocationControl?.ActionGate == true;
    private bool NativeScriptAllocationPaused => nativeScriptState.AllocationControl?.PauseGate == true;

    private static void RequireNativeAllocationStatistics(BattleCombatant actor)
    {
        if (actor.NativeAllocationPendingStatistics)
        {
            throw new NotSupportedException("Native allocation statistics are not yet initialized by its113/48AD10 callback.");
        }
    }

    private void CompleteNativeAllocationGate()
    {
        SetNativeBattleClockActive(true);
        if (nativeScriptState.AllocationControl is not { } control)
        {
            throw new InvalidDataException("Native4876B0 has no reached allocation control owner.");
        }
        nativeScriptState = nativeScriptState with { AllocationControl = control with { ActionGate = false, PauseGate = false } };
    }

    private void QueueNativeActivation(BattlePreviousTarget? target)
    {
            ActivateNativeParticipant(FindActiveActivationActor(target));
        }

    private BattleCombatant FindActiveActivationActor(BattlePreviousTarget? target)
    {
        return target switch
        {
            NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => FindNativeActor(original.Operand),
            BattlePreviousTarget.Actor captured when FindTargetActor(captured.Reference) is { IsActive: true } actor => actor,
            _ => throw new NotSupportedException("Native activation requires an active allocated participant.")
        };
    }
}
