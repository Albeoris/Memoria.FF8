using System.Collections.Immutable;

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
            RunExpandedNativeAi(actor, 3);
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
        return CanRetainNativeSupportActor(actor, reaction) || UsesExpandedRoster && IsExpandedEnemyDeathAction(actor, stage, request);
    }

    private bool HasExpandedEnemyDeathAction(BattleCombatant actor)
    {
        return ActiveAction is { } active && active.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, active.NativeAiCallbackStage, active.TargetRequest) ||
            actions.Any(command => command.ActorId == actor.Id && IsExpandedEnemyDeathAction(actor, command.NativeAiCallbackStage, command.TargetRequest));
    }

    private bool HasExpandedAiAction(BattleCombatant actor)
    {
        return ActiveAction is { NativeAiCallbackStage: not null } active && active.ActorId == actor.Id ||
            actions.Any(command => command.ActorId == actor.Id && command.NativeAiCallbackStage is not null);
    }

    private void CompleteExpandedAiRemovals()
    {
        foreach (BattleCombatant actor in enemies.Where(actor => actor.ExpandedAiRemovalPending && !HasExpandedAiAction(actor)))
        {
            // TODO(TD-009): Descriptor 105 waits for the native model callback; expanded actions own the logical wait.
            actor.ExpandedAiRemovalPending = false;
            ReleaseNativeParticipant(actor);
            actor.IsBusy = false;
        }
    }

    private void ValidateExpandedAiRemovals()
    {
        if (combatants.Any(actor => actor.ExpandedAiRemovalPending && (!UsesExpandedRoster || actor.IsPlayer || !actor.IsActive || actor.NativeAi is null || !HasExpandedAiAction(actor))))
        {
            throw new InvalidDataException("Saved expanded AI removal requires its active enemy and pending action.");
        }
    }

    private void CompleteExpandedEnemyDeath(BattleCombatant actor)
    {
        if (nativeAiCallbackStage != 3 && !UsesOriginalEnemySlots)
        {
            throw new NotSupportedException("Expanded AI death projection requires the enemy death callback.");
        }
        // TODO(TD-009): The expanded host uses its roster KO presentation in place of descriptor 106.
        actor.NativeStatus0 |= 1;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.Atb = 0;
        if (nativeAiCallbackStage != 3)
        {
            Log("Defeated", actor.Id, actor.Id, 0, $"{actor.Name} is defeated.");
        }
        if (UsesOriginalEnemySlots && actor.NativeAi?.DeathSequenceReleasesAllocation == true)
        {
            // TODO(TD-009): The expanded host retains sequence 3's allocation release in the graphics FIFO.
            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.DeathRelease));
        }
    }

    private void RunExpandedNativeAi(BattleCombatant actor, int callback)
    {
        if (callback == 1)
        {
            actor.AiTurnCount = unchecked((byte)(actor.AiTurnCount + 1));
        }
        byte? previous = nativeAiCallbackStage;
        bool previousProduction = producingExpandedAiCallback;
        if (callback == 4 && preparingExpandedHostedAction && producingExpandedAiInvocation is not null)
        {
            producingExpandedAiCallback = true;
        }
        nativeAiCallbackStage = checked((byte)callback);
        try
        {
            if (actor.NativeAi?.Scripts[callback].Length > 0)
            {
                InterpretNativeAi(actor, callback);
            }
        }
        finally
        {
            producingExpandedAiCallback = previousProduction;
            nativeAiCallbackStage = previous;
        }
    }

    private AiTargetSelection ResolveExpandedModelTarget(int selector)
    {
        BattleCombatant? target = UsesOriginalEnemySlots ? ExpandedAiModelActor(selector) : combatants.FirstOrDefault(candidate => IsExpandedAiTarget(candidate) && NativeModelId(candidate) == selector);
        return SelectAiActors(target is null ? [] : [target]);
    }

    private BattleCombatant? ExpandedAiModelActor(int model)
    {
        return ExpandedAiModelQueryPopulation(model).FirstOrDefault(candidate => NativeModelId(candidate) == model);
    }

    private IEnumerable<BattleCombatant> ExpandedAiModelQueryPopulation(int model)
    {
        if (!UsesOriginalEnemySlots)
        {
            return combatants.Where(HasAiRosterSlot);
        }
        if (model < 16)
        {
            return allies.Where(HasAiRosterSlot).OrderBy(candidate => candidate.Slot!.Value.Position);
        }
        return NativeTargetActors().OrderBy(candidate => candidate.NativeActorIndex);
    }

    private void ValidateExpandedOriginalEnemySlots()
    {
        BattleCombatant[] allocated = enemies.Where(actor => !IsAddedEnemy(actor) && actor.Slot is not null).ToArray();
        if (allocated.Select(actor => actor.NativeActorIndex).Distinct().Count() != allocated.Length ||
            allocated.Any(actor => actor.NativeActorIndex is null || actor.ResolvedNativeLevel is null || actor.NativeAllocationPendingStatistics || actor.NativeTemplate is null) ||
            enemies.Any(actor => IsAddedEnemy(actor) && actor.NativeActorIndex.HasValue))
        {
            throw new InvalidDataException("Expanded original enemy slots require unique owners with resolved allocation statistics.");
        }
    }

    private static void StoreExpandedAiLastAttacker(BattleCombatant actor, int variable, bool add)
    {
        if (variable is < 220 or > 227 || add)
        {
            throw new NotSupportedException("Expanded battle AI can only assign an actor reference to a local variable.");
        }
        int index = variable - 220;
        string attackerId = actor.LastAttackerId ?? throw new NotSupportedException("Expanded AI requires the last contact owner to store an attacker reference.");
        actor.AiVariables[index] = 0;
        actor.AiActorReferences[index] = attackerId;
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

    private void QueueExpandedAiAction(BattleCombatant actor, byte command, ushort id, byte sequence, AiTargetSelection selected)
    {
        BattleNativeAction native = FindNativeCommandAction(actor, command, id);
        if (!NativeBattleExpandedAi.SupportsAction(native, UsesOriginalEnemySlots))
        {
            throw new NotSupportedException($"Expanded AI command {command}/{id} requires an unsupported combat service.");
        }
        EnqueueExpandedAiAction(actor, native.Command, native.ActionId, sequence, selected, native);
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
                selection = NativeBattleTargetRequestAdapter.RandomizeOriginalSelection(selection, samplesOriginalSlots: SamplesOriginalAiSlots);
            }
        }

        if (eligibility == BattleTargetEligibility.SavedStatus)
        {
            eligibility = selection switch
            {
                BattleTargetSelection.SideGroup or NativeBattleTargetRequestAdapter.DeferredOperands => BattleTargetEligibility.SavedStatus,
                BattleTargetSelection.Explicit when UsesOriginalEnemySlots => BattleTargetEligibility.Living,
                _ => BattleTargetEligibility.AiAvailable
            };
        }
        BattleTargetReplacement replacement = selection is NativeBattleTargetRequestAdapter.DeferredOperands ||
            selection is BattleTargetSelection.Explicit && UsesOriginalEnemySlots && eligibility is not BattleTargetEligibility.DrawnMagic
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
            if (!CaptureEncounterCounterCommand(actor, packet, "expanded AI counter", false, null))
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
