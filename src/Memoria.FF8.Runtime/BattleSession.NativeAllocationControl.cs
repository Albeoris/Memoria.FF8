namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAllocationControl(byte? SceneSlot, bool ActionGate, bool PauseGate);

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

    private void QueueNativeActivation(int index)
    {
        if (NativeGraphics is null)
        {
            ActivateNativeParticipant(FindNativeActor(index));
            return;
        }
        NativeGraphics = QueueNativeDescriptor(NativeGraphics, 113, 0x487670, checked((ushort)index));
    }

    private NativeBattleGraphicsState CompleteNativeAllocationStatistics(NativeBattleGraphicsState graphics)
    {
        NativeBattleAllocationControl control = nativeScriptState.AllocationControl ?? throw new InvalidDataException("Native48AD10 has no current allocation scene owner.");
        BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == nativeLastAllocatedIndex);
        if (control.SceneSlot is null || actor.SceneSlot != control.SceneSlot)
        {
            throw new InvalidDataException("Native allocation globals disagree with the current physical scene resource.");
        }
        InitializeNativeAllocatedStatistics(actor);
        return QueueNativeDescriptor(graphics, 103, NativeParticipantAddress(actor.NativeIndex), checked((ushort)actor.NativeIndex), (ushort)(1 | (actor.Hidden ? 0x100 : 0)));
    }

    private NativeBattleGraphicsState CompleteNativeActivation(NativeBattleGraphicsState graphics, int index)
    {
        BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == index);
        actor.NativeCombatantFlags &= ~0xC2u;
        QueueNativeParticipantInitialization(actor);
        Log("Activated", actor.Id, null, 0, $"{actor.Name} enters battle.");
        return graphics;
    }

    private NativeBattleGraphicsState CompleteNativeAllocationDescriptor(NativeBattleGraphicsState graphics)
    {
        CompleteNativeAllocationGate();
        return graphics;
    }
}
