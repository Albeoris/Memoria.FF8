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
        if (NativeGraphics is not { } graphics)
        {
            ActivateNativeParticipant(FindActiveActivationActor(target));
            return;
        }
        NativeGraphics = target switch
        {
            NativeBattleTargetRequestAdapter.OriginalPreviousOperand original when NativeBattleSlots.Contains(original.Operand) =>
                QueueNativeDescriptor(graphics, 113, 0x487670, checked((ushort)original.Operand)),
            BattlePreviousTarget.Actor captured when FindTargetActor(captured.Reference) is { } actor =>
                QueueNativeModelDescriptor(graphics, 113, 0x487670, CreatePresentationModelOwner(actor)),
            _ => throw new NotSupportedException("Native activation requires an allocated participant.")
        };
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

    private NativeBattleGraphicsState CompleteNativeAllocationStatistics(NativeBattleGraphicsState graphics)
    {
        if (hostedBattlePresentation)
        {
            return ReachHostedNativeAllocationStatistics(graphics);
        }
        NativeBattleAllocationControl control = nativeScriptState.AllocationControl ?? throw new InvalidDataException("Native48AD10 has no current allocation scene owner.");
        BattleCombatant actor = ResolveLastNativeAllocation();
        if (control.SceneSlot is null || actor.SceneSlot != control.SceneSlot)
        {
            throw new InvalidDataException("Native allocation globals disagree with the current physical scene resource.");
        }
        InitializeNativeAllocatedStatistics(actor);
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading })
        {
            graphics = QueueNativePlannedModelDescriptor(graphics, 103, actor, 0x100);
            return QueueNativeDescriptor(graphics, 10, 0x4876B0);
        }
        return QueueNativePlannedModelDescriptor(graphics, 103, actor, (ushort)(1 | (actor.Hidden ? 0x100 : 0)));
    }

    private NativeBattleGraphicsState CompleteNativeActivation(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleModelSlot? originalSlot = null)
    {
        if (hostedBattlePresentation)
        {
            NativeBattleModelLifecycleServices services = hostedFrameServices ?? throw new InvalidOperationException("Hosted activation requires its ordered receipt owner.");
            if (!graphics.ActorModels.Contains(owner) || !services.Facts.TryGetValue(owner, out NativeBattleModelActorFacts? actorFacts))
            {
                throw new InvalidDataException("Hosted activation lost its allocated model owner.");
            }
            services.Emit(new NativeBattleModelParticipantActivated(owner, actorFacts.ActorId) { OriginalSlot = originalSlot });
            return graphics;
        }
        if (presentationNativeTargets is not null)
        {
            return CompleteEffectPresentationActivation(graphics, owner);
        }
        BattleCombatant actor = GraphicsActor(graphics, owner);
        actor.NativeCombatantFlags &= ~0xC2u;
        QueueNativeParticipantInitialization(actor);
        Log("Activated", actor.Id, null, 0, $"{actor.Name} enters battle.");
        return graphics;
    }

    private NativeBattleGraphicsState CompleteNativeAllocationDescriptor(NativeBattleGraphicsState graphics)
    {
        if (hostedBattlePresentation)
        {
            (hostedFrameServices ?? throw new InvalidOperationException("Hosted native 4876B0 requires its ordered receipt owner.")).EmitAllocationGate();
            return graphics;
        }
        if (encounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading })
        {
            return CompleteEncounterAllocationLoad(graphics);
        }
        CompleteNativeAllocationGate();
        return graphics;
    }
}
