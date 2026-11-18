using System.Collections.Immutable;
using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    private void RunExpandedEnemyDeathAi(BattleCombatant actor)
    {
        actor.AiDeathHandled = true;
        // 494410 publishes stage 3 with the enemy's KO bit clear after the immediate callback.
        actor.NativeStatus0 &= 0xFFFE;
        try
        {
            RunNativeAi(actor, 3);
        }
        finally
        {
            if (actor.CurrentHp == 0)
            {
                actor.NativeStatus0 |= 1;
            }
        }
    }

    private static bool IsExpandedEnemyDeathAction(BattleCombatant actor, byte? stage, BattleTargetRequest? request)
    {
        return stage == 3 && request is not null && actor.IsActive && !actor.IsPlayer && actor.NativeAi is { } ai && ai.Scripts[3].Length > 0 &&
            (actor.AiDeathHandled || actor.CurrentHp > 0) && (actor.NativeCombatantFlags & 0x20) != 0 && (NativeBattleStatusBits(actor) & 0x10000) == 0;
    }

    private static bool CanExecuteExpandedAction(BattleCombatant actor, byte? stage, BattleTargetRequest? request)
    {
        return CanNativeBattleActorAct(actor) || IsExpandedEnemyDeathAction(actor, stage, request);
    }

    private bool CanRetainBattleActionActor(BattleCombatant actor, BattleNativePlayerReaction? reaction, byte? stage, BattleTargetRequest? request)
    {
        return CanRetainNativeSupportActor(actor, reaction) || IsExpandedEnemyDeathAction(actor, stage, request);
    }

    private bool HasExpandedEnemyDeathAction(BattleCombatant actor)
    {
        return ActiveAction is { } active && active.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, active.NativeAiCallbackStage, active.TargetRequest) ||
            actions.Any(command => command.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, command.NativeAiCallbackStage, command.TargetRequest));
    }

    private void CompleteExpandedEnemyDeath(BattleCombatant actor)
    {
        // TODO(TD-009): The expanded host uses its roster KO presentation in place of descriptor 106.
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
        if (nativeAiCallbackStage != 3)
        {
            Log("Defeated", actor.Id, actor.Id, 0, $"{actor.Name} is defeated.");
        }
        if (actor.NativeAi?.DeathSequenceReleasesAllocation == true)
        {
            // TODO(TD-009): The expanded host retains sequence 3's allocation release in the graphics FIFO.
            StageOrQueueRosterOperation(new BattleRosterMutation.Operation.DeathRelease(TargetActorReference(actor)));
        }
    }

    private AiTargetSelection ResolveExpandedModelTarget(BattleCombatant actor, int selector)
    {
        BattleCombatant? target = FindSourceEnemyModelActor(actor, selector);
        AiTargetSelection selected = SelectAiActors(target is null ? [] : [target]);
        return selected with { MissingOriginalModel = target is null };
    }

    private BattleCombatant? ExpandedAiModelActor(int model)
    {
        return ExpandedAiModelQueryPopulation(model).FirstOrDefault(candidate => NativeModelId(candidate) == model);
    }

    private IEnumerable<BattleCombatant> ExpandedAiModelQueryPopulation(int model)
    {
        if (model < 16)
        {
            return allies.Where(HasAiRosterSlot).OrderBy(candidate => candidate.Slot!.Value.Position);
        }
        return enemies.Where(HasAiRosterSlot).OrderBy(candidate => candidate.NativeActorIndex ?? int.MaxValue).ThenBy(candidate => candidate.Slot!.Value.Position);
    }

    private void ValidateRestoredRoster(BattleSnapshot snapshot)
    {
        var occupied = new HashSet<BattleRosterSlot>();
        var aliases = new HashSet<int>();
        foreach (BattleCombatant actor in combatants)
        {
            if (actor.Slot is { } slot && !occupied.Add(slot) || actor.NativeActorIndex is int original && !aliases.Add(original))
            {
                throw new InvalidDataException("Saved battle participants require distinct occupied slots and original model aliases.");
            }
            if (actor.Slot is { } occupiedSlot && snapshot.PresentationContinuation?.ModelRegistry?.Loads.TryGetValue(CreatePresentationModelOwner(actor), out HostedModelLoadAdmission? model) == true &&
                (model.Load.Slot != occupiedSlot || model.Load.OriginalSlot?.Offset != actor.NativeActorIndex))
            {
                throw new InvalidDataException("Saved participant placement disagrees with its admitted presentation model.");
            }
        }

        foreach (BattleCombatant enemy in enemies.Where(actor => actor.Slot is not null))
        {
            NativeBattleEnemyAllocation? allocation = nativeAllocations.Find(candidate => candidate.Id == enemy.Id);
            if (allocation is null && UsesOriginalEnemySlots && !IsAddedEnemy(enemy) &&
                (enemy.NativeActorIndex is null || enemy.SourceDefinition.InitialOriginalEnemyOrdinal is int ordinal && enemy.NativeActorIndex != ordinal + NativeBattleSlots.AllyCount))
            {
                throw new InvalidDataException("An original formation participant lost its entitled model alias.");
            }
            if (allocation is null && IsAddedEnemy(enemy) && enemy.NativeActorIndex.HasValue)
            {
                throw new InvalidDataException("An added formation participant cannot replace an original model alias.");
            }
            if ((allocation is not null || UsesOriginalEnemySlots) && (enemy.NativeTemplate is null ||
                (enemy.NativeAllocationPendingStatistics ? !HasPendingAllocationStatistics(enemy, allocation, snapshot) : enemy.ResolvedNativeLevel is null)))
            {
                throw new InvalidDataException("A saved enemy placement lacks resolved statistics or its pending model admission.");
            }
        }
    }

    private bool HasPendingAllocationStatistics(BattleCombatant actor, NativeBattleEnemyAllocation? allocation, BattleSnapshot snapshot)
    {
        if (actor.IsActive || allocation is null)
        {
            return false;
        }
        if (allocation.EncounterBatchId is not null)
        {
            return snapshot.EncounterAllocation is { Phase: BattleEncounterAllocationPhase.Loading, LoadingIndex: >= 0 } batch &&
                !batch.ActorIds.IsDefaultOrEmpty && batch.ActorIds.IndexOf(actor.Id) >= batch.LoadingIndex;
        }
        HostedDispatchControl.LoadEnemy expected = ReadHostedEncounterAllocationLoad(actor);
        bool ownsLastAllocation = snapshot.NativeLastAllocatedTarget switch
        {
            BattlePreviousTarget.Actor captured => captured.Reference == expected.Owner.Actor,
            NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => actor.NativeActorIndex == original.Operand && expected.OriginalSlot?.Offset == original.Operand,
            _ => false
        };
        return ownsLastAllocation &&
            snapshot.NativeScriptState?.AllocationControl is { ActionGate: true, PauseGate: true, SceneSlot: byte scene } &&
            scene == ReadNativeAllocationSourceScene(actor) &&
            snapshot.PresentationContinuation?.ModelRegistry?.Loads.TryGetValue(expected.Owner, out HostedModelLoadAdmission? admission) == true && admission.Load == expected;
    }

    private static bool IsExpandedAiTarget(BattleCombatant candidate)
    {
        return candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && (NativeSavedStatusBits(candidate) & 5) == 0;
    }

    private static bool EnemyActionTargetsKnockedOutAllies(BattleNativeAction? action)
    {
        return action is { Command: 8 or 252 } && (action.AttackFlags & 0x80) != 0;
    }

    private void QueueExpandedGraphicCommand(BattleCombatant actor, byte magicId, byte sequence, AiTargetSelection selected)
    {
        EnqueueExpandedAiAction(actor, 252, 1, sequence, selected, graphic: new(magicId));
    }

    private bool QueueExpandedAiAction(BattleCombatant actor, byte command, ushort id, byte sequence, AiTargetSelection selected)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id);
        bool randomTarget = command is 2 or 4 && (native.TargetFlags.GetValueOrDefault() & 2) != 0;
        if (selected.MissingOriginalModel && !randomTarget && !nativeScriptState.DisableAutomaticTargetReplacement)
        {
            //487DF0's failed model lookup contributes zero target bits to 48EEB0 and returns on B0E.
            actor.PreviousAiTarget = selected.PreviousTarget;
            return false;
        }
        if (!NativeBattleExpandedAi.SupportsAction(native))
        {
            throw new NotSupportedException($"Expanded AI command {command}/{id} requires an unsupported combat service.");
        }
        EnqueueExpandedAiAction(actor, native.Command, native.ActionId, sequence, selected, native);
        return true;
    }

    private void EnqueueExpandedAiAction(BattleCombatant actor, byte command, ushort actionId, byte sequence, AiTargetSelection selected, BattleNativeAction? native = null, BattleNativeGraphicCommand? graphic = null, BattleTargetEligibility eligibility = BattleTargetEligibility.SavedStatus)
    {
        if (selected.Request.Selection is BattleTargetSelection.None && graphic is null)
        {
            return;
        }

        BattleTargetSelection selection = selected.Request.Selection;
        BattleActorReference? deathSelf = nativeAiCallbackStage == 3 && selection is BattleTargetSelection.Explicit explicitActors &&
            explicitActors.Actors.Any(reference => reference.ActorId == actor.Id) && (NativeSavedStatusBits(actor) & 1) == 0 ? TargetActorReference(actor) : null;
        bool allowsKnockedOutAllies = selected.Request.AllowsKnockedOutAllies || EnemyActionTargetsKnockedOutAllies(native) || graphic is not null && (NativeGraphicsTargetFlags() & 0x80) != 0;
        if (native is { Command: 2 or 4 })
        {
            byte flags = native.TargetFlags.GetValueOrDefault();
            if (eligibility == BattleTargetEligibility.DrawnMagic || selection is BattleTargetSelection.SideGroup or NativeBattleTargetRequestAdapter.DeferredOperands)
            {
                allowsKnockedOutAllies |= (flags & 1) != 0;
            }
            if ((flags & 2) != 0 && selection is BattleTargetSelection.SideGroup or NativeBattleTargetRequestAdapter.DeferredOperands)
            {
                selection = NativeBattleTargetRequestAdapter.RandomizeOriginalSelection(selection, samplesOriginalSlots: true);
            }
        }

        if (eligibility == BattleTargetEligibility.SavedStatus)
        {
            eligibility = selection switch
            {
                BattleTargetSelection.SideGroup or NativeBattleTargetRequestAdapter.DeferredOperands => BattleTargetEligibility.SavedStatus,
                BattleTargetSelection.Explicit => BattleTargetEligibility.Living,
                _ => BattleTargetEligibility.AiAvailable
            };
        }
        BattleTargetReplacement replacement = selection is NativeBattleTargetRequestAdapter.DeferredOperands ||
            selection is BattleTargetSelection.Explicit && eligibility is not BattleTargetEligibility.DrawnMagic
            ? BattleTargetReplacement.UseCurrentBattlePolicy
            : BattleTargetReplacement.None;
        var request = new BattleTargetRequest(selection, deathSelf is not null ? BattleTargetEligibility.DeathCallback : eligibility, allowsKnockedOutAllies, replacement, deathSelf);
        actor.PreviousAiTarget = selected.PreviousTarget;
        string targetId = selected.PreviousTarget is BattlePreviousTarget.Actor first ? first.Reference.ActorId : actor.Id;
        BattleAttackCommand packet = new(actor.Id, targetId, request, BattleAbility.EnemyAbility, actionId)
        {
            NativeQueuedCommand = command,
            NativeAction = native,
            NativeGraphic = graphic,
            NativeAiCallbackStage = nativeAiCallbackStage,
            NativeSequence = sequence
        };
        if (producingExpandedAiCallback)
        {
            if (!CaptureEncounterCounterCommand(packet))
            {
                ProduceExpandedAiCallbackCommand(packet);
            }
        }
        else
        {
            actor.Atb = 0;
            actor.IsBusy = true;
            actions.Enqueue(packet);
        }
    }
}
