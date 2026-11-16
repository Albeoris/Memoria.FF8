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
}

public sealed partial class BattleSession
{

    public static bool CanSelectEitherTargetSide(BattleActionDefinition action)
    {
        return action.NativeAction?.TargetFlags is byte flags ? NativeBattleLimitRules.CanSelectEitherSide(flags) : !action.AllTargets;
    }

    public static bool IsValidSelectedActionTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant target)
    {
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target))
        {
            return false;
        }
        if (action.NativeAction?.TargetFlags is byte flags && NativeBattleLimitRules.IsFixedAllyTarget(flags))
        {
            return target.Id == actor.Id;
        }
        return CanSelectEitherTargetSide(action) || (target.IsPlayer == actor.IsPlayer) == action.Allies;
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
            !(!UsesExpandedRoster && GetNativeMagicCastCounts(actor.Id, action.Id).Contains(targets.Count) || actor.IsPlayer && GetPlayerMagicCastCounts(actor.Id, action.Id).Any(count => targets.Count <= count)))
        {
            throw new InvalidOperationException("Magic target groups require the current native Double/Triple status, sufficient stock and one installed spell.");
        }
        ImmutableArray<BattleMagicCastInvocation>.Builder casts = ImmutableArray.CreateBuilder<BattleMagicCastInvocation>(targets.Count);
        BattleActorReference anchor = TargetActorReference(GetActor(firstTarget));
        foreach (string targetId in targets)
        {
            BattleCombatant target = GetActor(targetId);
            if (!IsValidSelectedActionTarget(actor, action, target))
            {
                throw new InvalidOperationException("Every selected magic target must satisfy the original spell's target contract.");
            }
            BattleTargetRequest request = CreatePlayerTargetRequest(actor, target, flags);
            BattleTargetSelection preflight = UsesExpandedRoster
                ? new BattleTargetSelection.Explicit([TargetActorReference(target)])
                : NativeBattleTargetRequestAdapter.CreatePlayerRequest(flags, target.RequireNativeActorIndex()).OriginalProjection!.Operands;
            casts.Add(new(request, preflight, UsesExpandedRoster ? TargetActorReference(target) : anchor));
        }
        return UsesExpandedRoster && targets.Count == 1 ? [] : casts.MoveToImmutable();
    }

    private BattleCombatant ReadMagicPreflightAnchor(BattleMagicCastInvocation cast, bool requireActive)
    {
        BattleCombatant? active = cast.PreflightSelection switch
        {
            BattleTargetSelection.Explicit explicitActors => explicitActors.Actors.Select(FindTargetActor).FirstOrDefault(actor => actor is not null && (!requireActive || actor.IsActive)),
            NativeBattleTargetRequestAdapter.DeferredOperands original => ReadOriginalSelectedActor(original, requireActive),
            _ => throw new InvalidDataException("Magic cast preflight has no recognized target selection.")
        };
        return active ?? FindTargetActor(cast.FallbackAnchor) ?? throw new InvalidDataException("Magic cast preflight lost its fallback actor.");
    }

    private BattleCombatant? ReadOriginalSelectedActor(NativeBattleTargetRequestAdapter.DeferredOperands selection, bool activeOnly)
    {
        BattleActorReference? candidate = NativeBattleTargetRequestAdapter.ReadFirstActiveMagicAnchor(selection, ReadOriginalTargetOccupant, reference => FindTargetActor(reference) is { } actor && (!activeOnly || actor.IsActive));
        return candidate is null ? null : FindTargetActor(candidate);
    }

    private void ResolveNativeMagicGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        ResolveMagicGroupInvocations(actor, command, action, (invocation, index) =>
        {
            BattleCombatant target = ReadMagicPreflightAnchor(invocation, requireActive: true);
            if (!target.IsAlive && (command.SmartAttack is not null || !combatants.Any(candidate => candidate.IsActive && candidate.IsAlive && !candidate.Untargetable && candidate.IsPlayer == target.IsPlayer)))
            {
                return false;
            }
            BattleActionPresentation cast = MagicGroupInvocation(command, action, invocation, target);
            //485610 submits one104 for the group; the effect consumes the following headers itself.
            return ResolveInstalledNativeAction(actor, target, cast, action, queueGraphics: index == 0);
        });
    }

    private void ResolveMagicGroupInvocations(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, Func<BattleMagicCastInvocation, int, bool> publish)
    {
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

    private static BattleActionPresentation MagicGroupInvocation(BattleActionPresentation command, BattleActionDefinition action, BattleMagicCastInvocation invocation, BattleCombatant target)
    {
        return command with { TargetId = target.Id, TargetRequest = invocation.Targets, MagicCasts = [], NativeMagicCastMode = 0, NativeAction = action.NativeAction };
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
        bool hosted = OwnsExpandedHostedInvocation;
        BattleActionDefinition action = FindAction(BattleAbility.Magic, command.ActionId);
        List<(BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts)> componentImpacts = [];
        bool producedContacts = false;
        int firstOperation = expandedOperationCursor.Operations.Length;

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
                if (!CanNativeBattleActorAct(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0)
                {
                    return false;
                }
                BattleCombatant selected = ReadMagicPreflightAnchor(invocation, requireActive: false);
                if (command.SmartAttack is not null && !IsValidSelectedActionTarget(actor, action, selected))
                {
                    return false;
                }
                BattleCombatant? target = SelectExpandedMagicGroupTarget(actor, action, selected);
                if (target is null)
                {
                    return false;
                }
                BattleActionPresentation cast = MagicGroupInvocation(command, action, invocation, target);
                (bool hasContacts, bool completed) = PublishExpandedMagicGroupInvocation(actor, cast, action, hosted, componentImpacts);
                producedContacts |= hasContacts;
                return completed;
            });
            if (hosted && producedContacts && (actor.IsPlayer || HasEncounterPolicy(actor)))
            {
                bool grouped = TryGroupNativeMagicCasts(firstOperation, action.NativeAction!);
                if (!grouped && !actor.IsPlayer)
                {
                    throw new NotSupportedException("The compiled repeat requires its contiguous native spell headers and a supported presentation group.");
                }
            }
            if (!hosted && producedContacts)
            {
                resolvingNativeAction = command;
                CompleteNativeActionStatuses(actor);
            }
        }
        finally
        {
            deferringExpandedImpacts = false;
            resolvingNativeAction = null;
            nativeActionCommitDepth--;
        }
        if (!hosted)
        {
            FlushExpandedEnemyReactions();
        }
        if (producingExpandedAiCallback)
        {
            return;
        }
        if (!producedContacts)
        {
            if (hosted)
            {
                nativeReactionOrder = null;
            }
            CompleteActionPresentation(command, "Magic group has no contacts.");
            return;
        }

        if (hosted)
        {
            ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
            return;
        }
        foreach ((BattleActionPresentation cast, ImmutableArray<ExpandedBattleContact> contacts) in componentImpacts)
        {
            PublishExpandedImpacts(cast, contacts);
        }
        CompleteActionPresentation(command, "Repeated magic completed.");
    }

    private bool TryGroupNativeMagicCasts(int firstOperation, BattleNativeAction native)
    {
        ImmutableArray<ExpandedHostedOperation> operations = expandedOperationCursor.Operations;
        if (operations.Length - firstOperation <= 1)
        {
            return true;
        }
        if (!SupportsHostedNativeMagicRepeat(native, definition.NativeEffectCatalog) ||
            operations[firstOperation] is not ExpandedHostedInvocation { Header.Command: 2 } first)
        {
            return false;
        }

        ImmutableArray<ExpandedHostedInvocation>.Builder following = ImmutableArray.CreateBuilder<ExpandedHostedInvocation>(operations.Length - firstOperation - 1);
        foreach (ExpandedHostedOperation operation in operations.Skip(firstOperation + 1))
        {
            if (operation is not ExpandedHostedInvocation next || next.Header.Command != 2 || next.Header.Source != first.Header.Source ||
                next.Header.Effect != first.Header.Effect || next.Header.Action != first.Header.Action || next.EnemyDraw is not null)
            {
                return false;
            }
            following.Add(next);
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
                (!action.NativeDouble || SupportsHostedNativeMagicRepeat(spell.NativeAction, catalog)));
    }

    private static bool SupportsHostedNativeMagicRepeat(BattleNativeAction action, ImmutableArray<NativeBattleEffectBinding> catalog)
    {
        return action is { Command: 2, MagicId: > 0 } && !catalog.IsDefaultOrEmpty && action.MagicId <= catalog.Length &&
            catalog[action.MagicId - 1].Entry == NativeBattleFireEffect.Entry;
    }

    private (bool HasContacts, bool Completed) PublishExpandedMagicGroupInvocation(BattleCombatant actor, BattleActionPresentation cast, BattleActionDefinition action, bool hosted,
        List<(BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts)> componentImpacts)
    {
        BattleNativeAction native = action.NativeAction!;
        var hitPlan = PlanNativeHits(actor, cast, native);
        if (hitPlan.Length == 0 || hitPlan[0].Targets.Length == 0)
        {
            return (false, false);
        }
        BattleNativeAction? reflected = null;
        if (hitPlan.Any(hit => hit.Targets.Any(recipient => NativeActionReflects(recipient, native))))
        {
            reflected = definition.NativeActions.SingleOrDefault(candidate => candidate.Command == 247 && candidate.ActionId == native.ActionId && candidate.CrisisLevel is null);
            if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected))
            {
                return (false, false);
            }
        }

        resolvingNativeAction = cast;
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = 2, Command = 2, Action = native.ActionId };
        NativeBattleActionHeaderPreparation? hostedHeader = hosted ? BuildExpandedActionHeaderPreparation(cast, native) : null;
        int operationStart = expandedOperationCursor.Operations.Length;
        ReserveExpandedAiInvocation(cast, hostedHeader);
        deferringExpandedImpacts = hosted;
        NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, cast, action, native, hitPlan);
        deferringExpandedImpacts = false;
        if (produced.Contacts.IsEmpty)
        {
            if (hostedHeader is not null)
            {
                expandedOperationCursor.Operations = expandedOperationCursor.Operations.RemoveAt(operationStart);
                producingExpandedAiInvocation = null;
            }
            return (false, false);
        }
        ImmutableArray<ExpandedBattleContact> contacts = ToHostedContacts(produced);
        (BattleActionPresentation? reflection, ImmutableArray<ExpandedBattleContact> reflectedContacts, NativeBattleActionHeaderPreparation? reflectionHeader) =
            ResolveExpandedReflection(actor, cast, action, produced.Reflector, reflected);
        if (hostedHeader is not null)
        {
            StageExpandedHostedInvocation(cast, hostedHeader, contacts, TakePendingHostedEnemyDraw());
            if (reflection is not null)
            {
                StageExpandedHostedInvocation(reflection, reflectionHeader!, reflectedContacts, null);
            }
        }
        else
        {
            CommitExpandedEnemyDraw(contacts[0].RecipientId, ref pendingEnemyDraw);
            componentImpacts.Add((cast, contacts));
            if (reflection is not null)
            {
                componentImpacts.Add((reflection, reflectedContacts));
            }
        }
        // Preserve contacts from a partial invocation, while leaving its stock and affinity uncommitted.
        return (true, !produced.RepeatAborted && produced.PublishedCount != 0);
    }

    private BattleCombatant? SelectExpandedMagicGroupTarget(BattleCombatant actor, BattleActionDefinition action, BattleCombatant selected)
    {
        if (IsValidSelectedActionTarget(actor, action, selected))
        {
            return selected;
        }
        if (nativeScriptState.DisableAutomaticTargetReplacement && !action.AllTargets)
        {
            return null;
        }
        int position = selected.Slot?.Position ?? -1;
        BattleCombatant[] available = Roster(selected.Side).Where(candidate => IsValidSelectedActionTarget(actor, action, candidate)).ToArray();
        return available.FirstOrDefault(candidate => candidate.Slot?.Position > position) ?? available.LastOrDefault();
    }

    private void ValidateExpandedMagicTargets(string actorId, string targetId, BattleAbility ability, int actionId, ImmutableArray<BattleMagicCastInvocation> casts, int mode)
    {
        ValidateNativeMagicTargets(ability, actionId, casts, mode);
        if (!UsesExpandedRoster || casts.IsEmpty)
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
            casts.Any(cast => cast is null || cast.Targets.Selection is BattleTargetSelection.None || FindTargetActor(cast.FallbackAnchor) is null))
        {
            throw new InvalidDataException("Native magic groups require one to three bound target requests.");
        }
        if (casts.Length != 0 && FindAction(BattleAbility.Magic, actionId).NativeAction is not { Command: 2 })
        {
            throw new InvalidDataException("Native repeated magic requires its original installed command2 effect.");
        }
    }
}
