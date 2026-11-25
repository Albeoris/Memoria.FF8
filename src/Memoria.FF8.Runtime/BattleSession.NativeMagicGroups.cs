using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleMagicCastInvocation
{
    public BattleMagicCastInvocation(BattleTargetRequest targets, BattleTargetSelection preflightSelection, BattleActorReference fallbackAnchor)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(fallbackAnchor);
        if (preflightSelection is not (BattleTargetSelection.Explicit or NativeBattleTargetRequestAdapter.DeferredOperands))
        {
            throw new ArgumentException("Magic cast preflight requires an explicit actor or original operand selection.", nameof(preflightSelection));
        }
        Targets = targets;
        PreflightSelection = preflightSelection;
        FallbackAnchor = fallbackAnchor;
    }

    public BattleTargetRequest Targets { get; }
    public BattleTargetSelection PreflightSelection { get; }
    public BattleActorReference FallbackAnchor { get; }
    public BattleRosterSlot? PreflightSlot { get; init; }
}

public sealed partial class BattleSession
{

    public static bool CanSelectEitherTargetSide(BattleActionDefinition action)
    {
        return action.NativeAction?.TargetFlags is byte flags ? NativeBattleLimitRules.CanSelectEitherSide(flags) : !action.AllTargets;
    }

    public static bool IsValidSelectedActionTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant target)
    {
        return IsValidSelectedActionTarget(actor, action, target, null);
    }

    private static bool IsValidSelectedActionTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant target, byte? bindingTargetFlags)
    {
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target))
        {
            return false;
        }
        byte? targetFlags = bindingTargetFlags ?? action.NativeAction?.TargetFlags;
        if (targetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags))
        {
            return target.Id == actor.Id;
        }
        return (targetFlags is byte nativeFlags ? NativeBattleLimitRules.CanSelectEitherSide(nativeFlags) : !action.AllTargets) || (target.IsPlayer == actor.IsPlayer) == action.Allies;
    }

    public static bool HasValidTargetLifeState(BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive)
        {
            return false;
        }
        if (!IsNativeRevive(action))
        {
            return target.IsAlive;
        }
        //491940/4935A0 route Life/Full-life against Zombie through491AD0 damage.
        return !target.IsAlive || action.NativeAction?.AttackType is 5 or 6 && (NativeSavedStatusBits(target) & 0x40) != 0;
    }

    public IReadOnlyList<int> GetNativeMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int stock = GetAvailableMagicCasts(actorId, spellId);
        return NativeBattleMagicRepetition.NativeCounts(NativeBattleStatusBits(actor), stock);
    }

    public IReadOnlyList<int> GetPlayerMagicCastCounts(string actorId, int spellId)
    {
        BattleCombatant actor = GetActor(actorId);
        int available = GetAvailableMagicCasts(actorId, spellId);
        return NativeBattleMagicRepetition.PlayerCounts(NativeBattleStatusBits(actor), available, actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? 0, magicVitals?.MagicSystem.InfiniteMagic == true, UnlimitedMagic);
    }

    private ImmutableArray<BattleMagicCastInvocation> PreparePlayerMagicTargets(BattleCombatant actor, BattleActionDefinition action, string firstTarget, IReadOnlyList<string>? targets)
    {
        if (targets is null)
        {
            return [];
        }
        if (action.Ability != BattleAbility.Magic || action.NativeAction is not { Command: 2, TargetFlags: byte flags } || targets.Count is < 1 or > 3 || targets[0] != firstTarget ||
            !(actor.IsPlayer && GetPlayerMagicCastCounts(actor.Id, action.Id).Any(count => targets.Count <= count)))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<BattleMagicCastInvocation>.Builder casts = ImmutableArray.CreateBuilder<BattleMagicCastInvocation>(targets.Count);
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!IsValidSelectedActionTarget(actor, action, target))
            {
                throw new InvalidOperationException("Every selected magic target must satisfy the original spell's target contract.");
            }
            BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, flags);
            BattleActorReference anchor = TargetActorReference(target);
            BattleRosterSlot slot = target.Slot ?? throw new InvalidOperationException("Repeated magic requires an occupied target slot.");
            casts.Add(new(request, new BattleTargetSelection.Explicit([anchor]), anchor) { PreflightSlot = slot });
        }
        return targets.Count == 1 ? [] : casts.MoveToImmutable();
    }

    private BattleCombatant? ReadOriginalSelectedActor(NativeBattleTargetRequestAdapter.DeferredOperands selection, bool activeOnly)
    {
        BattleActorReference? candidate = NativeBattleTargetRequestAdapter.ReadFirstActiveMagicAnchor(selection, ReadOriginalTargetOccupant, reference => FindTargetActor(reference) is { } actor && (!activeOnly || actor.IsActive));
        return candidate is null ? null : FindTargetActor(candidate);
    }

    private void ResolveMagicGroupInvocations(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, Func<BattleMagicCastInvocation, int, bool> publish)
    {
        if (command.NativeSlots is not null)
        {
            //485610 reads the admitted command16 target WORDs without command2's status or stock gates.
            for (int index = 0; index < command.MagicCasts.Length; index++)
            {
                if (!publish(command.MagicCasts[index], index))
                {
                    break;
                }
            }
            return;
        }

        uint statuses = EncounterMulticastStatusBits(actor);
        int mode = command.NativeMagicCastMode == 0 ? command.MagicCasts.Length : command.NativeMagicCastMode;
        int count = mode >= 3 && (statuses & 0x40000) != 0 ? command.MagicCasts.Length : command.MagicCasts.Length >= 2 && (statuses & 0x20000) != 0 ? 2 : 1;
        if (!actor.IsPlayer && HasEncounterPolicy(actor))
        {
            count = Math.Min(command.MagicCasts.Length, (statuses & 0x40000) != 0 ? 3 : (statuses & 0x20000) != 0 ? 2 : 1);
        }

        int costMode = command.NativeMagicCastMode != 0 && (statuses & 0x40000) != 0 ? command.NativeMagicCastMode : count;
        bool singleCost = count > 1 && actor.IsPlayer && MagicCost(actor, costMode) == 1;
        string? reservationId = command.MagicReservationId;
        int reserved = reservationId is null ? count : magicVitals!.MagicSystem.Reservations.FirstOrDefault(reservation => reservation.ActionId == reservationId)?.Count ?? 0;
        int completed = 0;
        try
        {
            for (int index = 0; index < count; index++)
            {
                if ((singleCost ? 0 : index) >= reserved || actor.IsPlayer && GetAvailableMagicCasts(actor.Id, command.ActionId, reservationId) == 0)
                {
                    break;
                }
                statuses = EncounterMulticastStatusBits(actor);
                if (index > 0 && ((NativeSavedStatusBits(actor) & 0x35) != 0 || (statuses & 0x4009) != 0 || !NativeBattleMagicRepetition.AdmitsRepeat(statuses, index + 1)))
                {
                    break;
                }
                nativeAffinityAction = null;
                if (!publish(command.MagicCasts[index], index))
                {
                    break;
                }
                completed++;
                //486A10 returns FF on the last debit;485610 then skips this cast's affinity loop.
                if (!singleCost && !SpendMagicGroupStock(actor, command.ActionId, reservationId, completed))
                {
                    break;
                }
                nativeAffinityAction = action.NativeAction;
                CompleteGuardianForceAffinity(actor, command);
            }
            if (singleCost && completed > 0)
            {
                SpendMagicGroupStock(actor, command.ActionId, reservationId, 1);
            }
        }
        finally
        {
            ReleaseMagicReservation(reservationId);
        }
    }

    private static BattleActionPresentation MagicGroupInvocation(BattleActionPresentation command, BattleActionDefinition action, BattleTargetRequest targets, BattleCombatant target)
    {
        return command with { TargetId = target.Id, TargetRequest = targets, MagicCasts = [], NativeMagicCastMode = 0, NativeAction = action.NativeAction };
    }

    private bool SpendMagicGroupStock(BattleCombatant actor, int spell, string? reservationId, int completed)
    {
        if (reservationId is not null)
        {
            CommitMagicReservation(reservationId, completed);
            return InfiniteMagic || UnlimitedMagic || actor.MagicStocks.Any(stock => stock.Id == spell && stock.Count > 0);
        }
        if (!actor.IsPlayer || UnlimitedMagic)
        {
            return true;
        }
        bool consumed = actor.TryConsumeMagic(spell);
        ReprojectNativePlayerStatistics(actor);
        return consumed && actor.NativeMagicInventory.Any(stock => stock.SpellId == spell && stock.Count > 0);
    }

    private void ResolveExpandedMagicGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        bool slots = command.NativeSlots is not null;
        BattleActionDefinition action = slots ? ExpandedLimitDefinition(actor, command.NativeAction!) : FindAction(BattleAbility.Magic, command.ActionId);
        bool producedContacts = false;
        int firstOperation = expandedOperationCursor.Operations.Length;
        List<NativeMagicReflectionReceipt>? reflections = actor.IsPlayer ? [] : null;

        //485610 produces the whole group before484180 post-action status work or pending reactions.
        if (!producingExpandedAiCallback)
        {
            BeginNativeReactionBatch();
        }
        nativeActionCommitDepth++;
        try
        {
            ResolveMagicGroupInvocations(actor, command, action, (invocation, _) =>
            {
                BattleCombatant selected = GetActor(invocation.FallbackAnchor.ActorId);
                BattleTargetRequest targets = invocation.Targets;
                BattleCombatant? target;
                if (slots)
                {
                    if ((NativeSavedStatusBits(actor) & 1) != 0)
                    {
                        return false;
                    }
                    if (targets.Selection is BattleTargetSelection.Explicit)
                    {
                        BattleCombatant? preferred = SelectMagicGroupTarget(invocation, candidate => IsBattleTargetEligible(candidate, targets));
                        target = preferred is null ? null : SelectSlotsPriorityTarget(action.NativeAction!, targets, preferred);
                    }
                    else
                    {
                        target = selected;
                    }
                }
                else
                {
                    if (!CanNativeBattleActorAct(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0 ||
                        command.SmartAttack is not null && !IsValidSelectedActionTarget(actor, action, selected))
                    {
                        return false;
                    }
                    target = SelectMagicGroupTarget(invocation, candidate => IsValidSelectedActionTarget(actor, action, candidate), action.AllTargets);
                }
                if (target is null)
                {
                    return false;
                }
                if (targets.Selection is BattleTargetSelection.Explicit { Actors.Length: 1 })
                {
                    targets = new(new BattleTargetSelection.Explicit([TargetActorReference(target)]), targets.Eligibility, targets.AllowsKnockedOutAllies, targets.Replacement, targets.DeathCallbackSelf);
                }
                BattleActionPresentation cast = MagicGroupInvocation(command, action, targets, target);
                (bool hasContacts, bool completed) = PublishExpandedMagicGroupInvocation(actor, cast, action, reflections);
                producedContacts |= hasContacts;
                return completed;
            });
            if (producedContacts && (actor.IsPlayer || HasEncounterPolicy(actor)) && !TryGroupNativeMagicCasts(firstOperation, action.NativeAction!, command: slots ? (byte)16 : (byte)2))
            {
                throw new InvalidDataException("The repeated native spell requires contiguous action headers.");
            }
            if (reflections is { Count: > 0 })
            {
                StageExpandedRepeatReflection(actor, action, reflections);
            }
        }
        finally
        {
            deferringExpandedImpacts = false;
            resolvingNativeAction = null;
            nativeActionCommitDepth--;
        }
        if (producingExpandedAiCallback)
        {
            return;
        }
        if (!producedContacts)
        {
            nativeReactionOrder = null;
            CompleteActionPresentation(command, "Magic group has no contacts.");
            return;
        }

        ActiveAction = command with { Phase = BattleActionPhase.Recovery, MagicReservationId = null };
    }

    private bool TryGroupNativeMagicCasts(int firstOperation, BattleNativeAction native, byte command = 2)
    {
        ImmutableArray<ExpandedHostedOperation> operations = expandedOperationCursor.Operations;
        if (operations.Length - firstOperation <= 1)
        {
            return true;
        }
        if (operations[firstOperation] is not ExpandedHostedInvocation first || first.Header.Command != command || first.Header.Action != native.ActionId || first.Header.Effect != native.MagicId)
        {
            return false;
        }

        bool presentsFirstHeaderOnly = !definition.NativeEffectCatalog.IsDefaultOrEmpty &&
            definition.NativeEffectCatalog.ElementAtOrDefault(first.Header.Effect - 1)?.PresentsFirstActionHeaderOnly == true;
        ImmutableArray<ExpandedHostedInvocation>.Builder following = ImmutableArray.CreateBuilder<ExpandedHostedInvocation>(operations.Length - firstOperation - 1);
        foreach (ExpandedHostedOperation operation in operations.Skip(firstOperation + 1))
        {
            if (operation is not ExpandedHostedInvocation next || next.Header.Command != command || next.Header.Caster != first.Header.Caster ||
                next.Header.Effect != first.Header.Effect || next.Header.Action != first.Header.Action || next.EnemyDraw is not null ||
                command == 2 && next.Header.Source != first.Header.Source)
            {
                return false;
            }
            following.Add(presentsFirstHeaderOnly ? next with { ContactDisposition = ExpandedHostedContactDisposition.ContextOnly } : next);
        }

        expandedOperationCursor.Operations = operations.SetItem(firstOperation, first with { FollowingCasts = following.MoveToImmutable() })
            .RemoveRange(firstOperation + 1, operations.Length - firstOperation - 1);
        return true;
    }

    /// <summary>Checks the compiled action's complete spell pool against the installed hosted action bindings.</summary>
    public static bool SupportsHostedEncounterSpellRule(EncounterCompiledAction action, ImmutableArray<NativeBattleEffectBinding> catalog)
    {
        return action.Kind is EncounterCompiledActionKind.Cast or EncounterCompiledActionKind.CastFromPool &&
            !action.Spells.IsDefaultOrEmpty && action.Dispel is null &&
            action.Spells.All(spell => ExpandedBattleActionRules.SupportsContacts(spell.NativeAction) &&
                (!action.NativeDouble || HasInstalledMagicBinding(spell.NativeAction, catalog)));
    }

    private static bool HasInstalledMagicBinding(BattleNativeAction action, ImmutableArray<NativeBattleEffectBinding> catalog)
    {
        return action is { Command: 2, MagicId: > 0 } && !catalog.IsDefaultOrEmpty && action.MagicId <= catalog.Length &&
            catalog[action.MagicId - 1] is { Entry: not 0 } binding && binding.Effect == action.MagicId;
    }

    private (bool HasContacts, bool Completed) PublishExpandedMagicGroupInvocation(BattleCombatant actor, BattleActionPresentation cast, BattleActionDefinition action, List<NativeMagicReflectionReceipt>? reflections)
    {
        BattleNativeAction native = action.NativeAction!;
        if (cast.NativeSlots is not null)
        {
            PrepareNativeLimitCommand(new(cast.ActorId, cast.TargetId, cast.TargetRequest, cast.Ability, cast.ActionId)
            {
                NativeQueuedCommand = 16,
                NativeAction = native
            });
        }
        var hitPlan = PlanNativeHits(actor, cast, native);
        if (hitPlan.Length == 0 || hitPlan[0].Targets.Length == 0)
        {
            return (false, false);
        }
        BattleNativeAction? reflected = null;
        if (hitPlan.Any(hit => hit.Targets.Any(recipient => NativeActionReflects(recipient, native))))
        {
            reflected = definition.NativeActions.SingleOrDefault(candidate => candidate.Command == 247 && candidate.ActionId == native.ActionId && candidate.CrisisLevel is null);
            if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected) && !ExpandedBattleActionRules.SupportsMagicCure(reflected))
            {
                return (false, false);
            }
        }

        resolvingNativeAction = cast;
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = 2, Command = native.Command, Action = native.ActionId };
        NativeBattleActionHeaderPreparation hostedHeader = BuildExpandedActionHeaderPreparation(cast, native);
        int operationStart = expandedOperationCursor.Operations.Length;
        ReserveExpandedAiInvocation(cast, hostedHeader);
        deferringExpandedImpacts = true;
        NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, cast, action, native, hitPlan,
            collectReflection: reflections is null ? null : reflector => reflections.Add(new(cast, reflector)));
        deferringExpandedImpacts = false;
        if (produced.Contacts.IsEmpty)
        {
            expandedOperationCursor.Operations = expandedOperationCursor.Operations.RemoveAt(operationStart);
            producingExpandedAiInvocation = null;
            return (false, false);
        }
        ImmutableArray<ExpandedBattleContact> contacts = ToHostedContacts(produced);
        if (reflections is not null)
        {
            StageExpandedHostedInvocation(cast, hostedHeader, contacts, TakePendingHostedEnemyDraw());
            return (true, !produced.RepeatAborted && produced.PublishedCount != 0);
        }

        (BattleActionPresentation? reflection, ImmutableArray<ExpandedBattleContact> reflectedContacts, NativeBattleActionHeaderPreparation? reflectionHeader) =
            ResolveExpandedReflection(actor, cast, action, produced.Reflector, reflected);
        StageExpandedHostedInvocation(cast, hostedHeader, contacts, TakePendingHostedEnemyDraw());
        if (reflection is not null)
        {
            StageExpandedHostedInvocation(reflection, reflectionHeader!, reflectedContacts, null);
        }
        // Preserve contacts from a partial invocation, while leaving its stock and affinity uncommitted.
        return (true, !produced.RepeatAborted && produced.PublishedCount != 0);
    }

    private BattleCombatant? SelectMagicGroupTarget(BattleMagicCastInvocation invocation, Func<BattleCombatant, bool> eligible, bool groupTarget = false)
    {
        BattleRosterSlot slot = invocation.PreflightSlot ?? throw new InvalidDataException("Repeated magic lost the target slot admitted with its initial actor.");
        bool allowReplacement = groupTarget || invocation.Targets.Replacement == BattleTargetReplacement.UseCurrentBattlePolicy && !nativeScriptState.DisableAutomaticTargetReplacement;
        return SelectBattleTargetFromSlot(slot, allowReplacement, eligible);
    }

    private bool HasValidMagicPreflightSlot(BattleMagicCastInvocation cast)
    {
        return cast.PreflightSlot is { } slot && FindTargetActor(cast.FallbackAnchor) is { } actor && slot.Side == actor.Side &&
            slot.Position < (slot.Side == BattleSide.Allies ? ExpandedBattleFormation.MaximumAllies : ExpandedBattleFormation.MaximumEnemies);
    }

    private void ValidateExpandedMagicTargets(string actorId, string targetId, BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode, BattleNativeAction? native, BattleNativeSlotsRoll? slots)
    {
        if (slots is not null)
        {
            ValidateSlotsGroup(GetActor(actorId), targetId, ability, actionId, casts, mode, native, slots);
            return;
        }
        ValidateNativeMagicTargets(ability, actionId, casts, mode);
        if (casts.IsEmpty)
        {
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        BattleActionDefinition action = FindAction(ability, actionId);
        bool compiledEnemy = !actor.IsPlayer && HasEncounterPolicy(actor);
        if ((!actor.IsPlayer && !compiledEnemy) || casts.Length < 2 || casts[0].FallbackAnchor.ActorId != targetId || !SupportsExpandedAction(action))
        {
            throw new InvalidDataException("Saved repeated magic lacks its original spell and logical recipient order.");
        }
        foreach (BattleMagicCastInvocation cast in casts)
        {
            if (compiledEnemy && cast.PreflightSelection is BattleTargetSelection.Explicit explicitActors &&
                !explicitActors.Actors.IsEmpty && explicitActors.Actors[0] == cast.FallbackAnchor && explicitActors.Actors.All(reference => FindTargetActor(reference) is not null))
            {
                continue;
            }
            if (cast.PreflightSelection is not BattleTargetSelection.Explicit { Actors: [var selected] } || cast.FallbackAnchor != selected || FindTargetActor(selected) is not { } target ||
                (NativeBattleLimitRules.IsFixedAllyTarget(action.NativeAction!.TargetFlags!.Value) ? target.Id != actorId : !CanSelectEitherTargetSide(action) && (target.IsPlayer == actor.IsPlayer) != action.Allies))
            {
                throw new InvalidDataException("Saved repeated magic targets an actor or side unavailable to its spell.");
            }
        }
    }

    private void ValidateNativeMagicTargets(BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode = 0)
    {
        if (mode != 0 && (mode != 3 || casts.Length != 2 || ability != BattleAbility.Magic))
        {
            throw new InvalidDataException("An abbreviated Triple command requires exactly two selected magic targets.");
        }
        if (casts.IsDefault || casts.Length > 3 || casts.Length != 0 && ability != BattleAbility.Magic ||
            casts.Any(cast => cast is null || cast.Targets.Selection is BattleTargetSelection.None || !HasValidMagicPreflightSlot(cast)))
        {
            throw new InvalidDataException("Native magic groups require one to three bound target requests.");
        }
        if (casts.Length != 0 && FindAction(BattleAbility.Magic, actionId).NativeAction is not { Command: 2 })
        {
            throw new InvalidDataException("Native repeated magic requires its original installed command2 effect.");
        }
    }
}
