using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private Dictionary<string, BattleGuardianFormState>? guardianForms;
    private long lastGuardianFormInvocation;

    public bool GuardianFormsEnabled => definition.GuardianFormsEnabled;

    public BattleGuardianFormState? GetGuardianForm(string actorId)
    {
        return guardianForms?.GetValueOrDefault(actorId);
    }

    public int? GetGuardianFormVisual(string actorId)
    {
        return GetGuardianForm(actorId) is { EntryCompleted: true, Phase: BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning } form ? ProfileForForm(form).ModelId : null;
    }

    public bool CanRequestGuardianForm(string actorId, int guardianForceId)
    {
        if (!GuardianFormsEnabled || IsGuardianSummonOccupied || !definition.GuardianFormResources.Any(resource => resource.GuardianForceId == guardianForceId) || !byId.TryGetValue(actorId, out BattleCombatant? actor))
        {
            return false;
        }
        return CanChoosePlayerCommand(actorId) && !IsPlayerCommandSealed(actor, 3) && CanNativeBattleActorAct(actor) && (actor.NativeStatus0 & 0x35) == 0 && (actor.NativeStatus1 & 0x14000) == 0 && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(guardianForceId) && guardianForces.GetValueOrDefault(guardianForceId) is { Acquired: true, HitPoints: > 0 };
    }

    public BattleActionDefinition GetGuardianFormActionDefinition(string actorId, BattleGuardianFormAction action)
    {
        BattleGuardianFormState form = GetGuardianForm(actorId) ?? throw new InvalidOperationException("The actor has no GF form.");
        return ResolveGuardianFormAction(form, GetGuardianFormBinding(form, action));
    }

    public IReadOnlyList<BattleGuardianFormAction> GetGuardianFormActions(string actorId)
    {
        return GetGuardianForm(actorId) is { EntryCompleted: true } form ? GetGuardianFormBindings(form).Select(binding => binding.Action).ToArray() : [];
    }

    private BattleGuardianFormDefinition ProfileForForm(BattleGuardianFormState form)
    {
        return BattleGuardianFormProfiles.ProfileFor(GuardianFormResources(form));
    }

    private BattleGuardianFormTier GuardianFormTier(BattleGuardianFormState form)
    {
        int level = FindAction(BattleAbility.GuardianForce, form.GuardianForceId).Level;
        return ProfileForForm(form).TierAtLevel(level);
    }

    private ImmutableArray<BattleGuardianFormActionBinding> GetGuardianFormBindings(BattleGuardianFormState form)
    {
        return GuardianFormTier(form).Actions.Select(action => ReadGuardianFormBinding(form, action)).ToImmutableArray();
    }

    private BattleGuardianFormActionBinding GetGuardianFormBinding(BattleGuardianFormState form, BattleGuardianFormAction action)
    {
        if (!GuardianFormTier(form).Actions.Contains(action))
        {
            throw new InvalidOperationException("This GF form action is unavailable at its current level.");
        }
        return ReadGuardianFormBinding(form, action);
    }

    private BattleGuardianFormActionBinding GetGuardianFormPhysicalBinding(BattleGuardianFormState form)
    {
        BattleGuardianFormAction action = GuardianFormTier(form).CompelledPhysical ?? throw new InvalidDataException("This form lacks its regular physical action.");
        return ReadGuardianFormBinding(form, action);
    }

    private BattleGuardianFormActionBinding? GetGuardianFormSpellBinding(BattleGuardianFormState form)
    {
        return GuardianFormTier(form).CompelledSpell is { } action ? ReadGuardianFormBinding(form, action) : null;
    }

    private BattleGuardianFormActionBinding ReadGuardianFormBinding(BattleGuardianFormState form, BattleGuardianFormAction action)
    {
        return GuardianFormResources(form).InnateBindings.SingleOrDefault(binding => binding.Action == action) ??
            throw new InvalidDataException($"GF form lacks its installed {action} action binding.");
    }

    private BattleActionDefinition ResolveGuardianFormAction(BattleGuardianFormState form, BattleGuardianFormActionBinding binding)
    {
        if (binding.Source is BattleGuardianFormActionSource.Native or BattleGuardianFormActionSource.Physical)
        {
            BattleNativeAction prototype = GuardianFormResources(form).PhysicalAction;
            byte command = binding.Command;
            BattleNativeAction native = prototype.Command == command && prototype.ActionId == binding.ResourceActionId && prototype.CrisisLevel == binding.NativeCrisisLevel ? prototype : binding.ResolveNativeAction(definition.NativeActions);
            if (binding.Power is int power)
            {
                native = native with { Power = checked((byte)power) };
            }
            bool allies = binding.Target == BattleGuardianFormTarget.Self || binding.Target == BattleGuardianFormTarget.Native && native.TargetFlags is byte flags && (flags & 0x40) == 0;
            bool allTargets = binding.Target == BattleGuardianFormTarget.AllEnemies || binding.Target == BattleGuardianFormTarget.Native && native.TargetFlags is byte targets && (targets & 0x32) == 0;
            return new(BattleAbility.EnemyAbility, native.ActionId, binding.Name, BattleActionEffect.Native, native.Power, allies, allTargets) { NativeAction = native };
        }
        return FindAction(binding.Source == BattleGuardianFormActionSource.Magic ? BattleAbility.Magic : BattleAbility.GuardianForce, binding.ResourceActionId);
    }

    private BattleGuardianFormResources[] GuardianFormVariants(int guardianForceId)
    {
        return definition.GuardianFormResources.Where(resource => resource.GuardianForceId == guardianForceId).OrderBy(resource => resource.VariantId, StringComparer.Ordinal).ToArray();
    }

    private BattleGuardianFormResources GuardianFormResources(BattleGuardianFormState form)
    {
        if (form.VariantId is { Length: > 0 } variant)
        {
            return definition.GuardianFormResources.Single(resource => resource.GuardianForceId == form.GuardianForceId && resource.VariantId == variant);
        }
        // Older single-variant forms had no separate body identity.
        return definition.GuardianFormResources.Single(resource => resource.GuardianForceId == form.GuardianForceId);
    }

    public void RequestGuardianForm(string actorId, int guardianForceId)
    {
        RequireNoPendingSlotsDeal();
        RequireNativeEntryReady();
        if (!CanRequestGuardianForm(actorId, guardianForceId))
        {
            throw new InvalidOperationException("Transformation requires a ready GF user and an available, living, junctioned GF.");
        }
        BattleGuardianFormResources[] variants = GuardianFormVariants(guardianForceId);
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.ProfileFor(variants[0]);
        foreach (BattleGuardianFormResources variant in variants)
        {
            ValidateGuardianFormResources(variant);
            if (BattleGuardianFormProfiles.ProfileFor(variant).ProfileId != profile.ProfileId)
            {
                throw new InvalidDataException("Variants of a GF must belong to the same profile.");
            }
        }
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, guardianForceId);
        if (!action.Available || action.NativeAction is null)
        {
            throw new NotSupportedException("GF transformation requires its installed summon contract.");
        }
        ValidateNativeActionContract(action.NativeAction);
        BattleCombatant actor = GetActor(actorId);
        long invocation = checked(lastGuardianFormInvocation + 1);
        BattleAttackCommand command = new(actorId, actorId, SelectBattleTarget(actor), BattleAbility.GuardianForce, guardianForceId)
        {
            NativeQueuedCommand = (byte)254,
            GuardianFormInvocation = new(invocation, guardianForceId, GuardianFormInvocationMode.EntryOnly)
        };
        // Resource and charge prerequisites must fail before input ownership or ATB changes.
        if (actor.GuardianForceCompatibility is not { Count: 16 } || definition.NativeBattleSpeed is null)
        {
            throw new NotSupportedException("GF transformation requires saved compatibility and battle speed.");
        }
        if (expandedPresentationProcess is { } process)
        {
            foreach (BattleGuardianFormResources variant in variants)
            {
                process.ValidateGuardianFormEntryResources(GuardianFormVisualOwner(actor, invocation, guardianForceId, variant.VariantId));
            }
        }
        QueueGuardianForce(actor, command);
        OnNativePlayerCommandSubmitted(actor, 3);
        (guardianForms ??= new(StringComparer.Ordinal)).Add(actorId, new(actorId, guardianForceId, profile.ProfileId, BattleGuardianFormPhase.Charging, invocation, 0));
        lastGuardianFormInvocation = invocation;
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actorId);
        Log("GuardianFormRequested", actorId, actorId, guardianForceId, "GF transformation charging.");
    }

    public void QueueGuardianFormAction(string actorId, string targetId, BattleGuardianFormAction action)
    {
        QueueGuardianFormAction(actorId, targetId, (int)action);
    }

    public void QueueGuardianFormAction(string actorId, string targetId, int commandId)
    {
        RequireNoPendingSlotsDeal();
        RequireNativeEntryReady();
        BattleGuardianFormState form = GetGuardianForm(actorId) ?? throw new InvalidOperationException("The actor has no GF form.");
        BattleCombatant actor = GetActor(actorId);
        if (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks == 0 || !CanChoosePlayerCommand(actorId) || !CanNativeBattleActorAct(actor) || ExpandedTimedLimitOwnsInput)
        {
            throw new InvalidOperationException("GF form action requires an active, ready form with remaining lifetime.");
        }
        BattleCombatant target = GetActor(targetId);
        if (commandId >= BattleGuardianFormCommand.LearnedAbilityOffset)
        {
            QueueGuardianFormLearnedCommand(actor, form, target, commandId - BattleGuardianFormCommand.LearnedAbilityOffset);
            return;
        }
        QueueGuardianFormActionCore(actor, form, target, GetGuardianFormBinding(form, (BattleGuardianFormAction)commandId));
    }

    private void QueueGuardianFormActionCore(BattleCombatant actor, BattleGuardianFormState form, BattleCombatant target, BattleGuardianFormActionBinding policy, bool compelled = false)
    {
        BattleActionDefinition action = ResolveGuardianFormAction(form, policy);
        ValidateNativeActionContract(action.NativeAction ?? throw new NotSupportedException("GF form action requires its installed action metadata."));
        if (!CanSelectGuardianFormInnateAction(actor, form, policy, action) || (compelled ? !target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target) : !IsGuardianFormInnateTarget(actor, target, policy, action)))
        {
            throw new InvalidOperationException("GF form action has an unavailable actor or target.");
        }
        bool finisher = policy.Terminal;
        BattleAttackCommand command = CreateGuardianFormCommand(actor, form, target, policy, action);
        command = EnsureMagicReservation(actor, command);
        if (policy.OncePerInvocation)
        {
            form = form with { UsedActions = form.UsedActions.Add(policy.Action) };
            guardianForms![actor.Id] = form;
        }
        StopTurnAdvance();
        actions.Enqueue(command);
        OnNativePlayerCommandSubmitted(actor, finisher ? (byte)3 : command.NativeQueuedCommand!.Value);
        actor.Atb = 0;
        actor.IsBusy = true;
        if (finisher)
        {
            guardianForms![actor.Id] = form with { Phase = BattleGuardianFormPhase.Finishing, FinisherAdmitted = true };
        }
    }

    private BattleAttackCommand CreateGuardianFormCommand(BattleCombatant actor, BattleGuardianFormState form, BattleCombatant target, BattleGuardianFormActionBinding binding, BattleActionDefinition action)
    {
        BattleNativeAction native = action.NativeAction!;
        BattleTargetRequest request = binding.Target switch
        {
            BattleGuardianFormTarget.AllEnemies => CreatePlayerTargetRequest(actor, target, 0x40),
            BattleGuardianFormTarget.Self or BattleGuardianFormTarget.SingleEnemy => SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living),
            _ when native.TargetFlags is byte flags => CreatePlayerTargetRequest(actor, target, flags),
            _ when binding.Source == BattleGuardianFormActionSource.Physical => SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living),
            _ => throw new InvalidDataException("The installed form action lacks a target selector.")
        };
        return new(actor.Id, target.Id, request, action.Ability, action.Id)
        {
            NativeAction = native,
            NativeSequence = binding.Sequence,
            NativeQueuedCommand = binding.Terminal ? (byte)254 : native.Command,
            FreeNativeMagic = binding.Source == BattleGuardianFormActionSource.Magic && binding.Cost == BattleGuardianFormActionCost.Innate,
            GuardianFormAction = binding.Action,
            GuardianFormInvocation = binding.Terminal ? new(form.InvocationId, form.GuardianForceId, GuardianFormInvocationMode.FinisherWithoutEntry) : null
        };
    }

    private BattleAttackCommand PrepareGuardianFormTurn(BattleCombatant actor, BattleAttackCommand submitted)
    {
        if (!submitted.HasGuardianFormAction || submitted.GuardianFormInvocation is not null || GetGuardianForm(actor.Id) is not { Phase: BattleGuardianFormPhase.Active } form ||
            submitted.NativeAction is not { } native || !(native.UsesPhysicalDamageFormula || native.UsesMagicDamageFormula) || native.Power == 0 ||
            actor.CurrentHp >= actor.MaximumHp || GuardianFormTier(form).HealBeforeAttack is not { } heal || form.UsedActions.Contains(heal) || submitted.GuardianFormAction == heal)
        {
            return submitted;
        }
        BattleGuardianFormActionBinding binding = ReadGuardianFormBinding(form, heal);
        BattleActionDefinition action = ResolveGuardianFormAction(form, binding);
        if (binding.RequiresGrounded && HasNativeFloat(NativeBattleStatusBits(actor)) || !IsGuardianFormInnateTarget(actor, actor, binding, action))
        {
            return submitted;
        }

        BattleAttackCommand recovery = CreateGuardianFormCommand(actor, form, actor, binding, action);
        // The submitted attack still owns this turn. Resume it ahead of later submissions after the recovery animation.
        BattleAttackCommand[] following = actions.ToArray();
        actions.Clear();
        actions.Enqueue(submitted);
        foreach (BattleAttackCommand command in following)
        {
            actions.Enqueue(command);
        }
        guardianForms![actor.Id] = form with { UsedActions = form.UsedActions.Add(heal) };
        return recovery;
    }

    private static void ValidateGuardianFormResources(BattleGuardianFormResources resources)
    {
        if (resources.ElementDefenses.IsDefault || resources.ElementDefenses.Length != 8 || resources.ElementDefenses.Any(value => value is < 0 or > 2550) || resources.StatusResistances.IsDefault || resources.StatusResistances.Length != 40)
        {
            throw new NotSupportedException("GF form requires its prototype's eight elemental defenses and forty status resistances.");
        }
        if (resources.InnateBindings.IsDefaultOrEmpty || resources.InnateBindings.Select(binding => binding.Action).Distinct().Count() != resources.InnateBindings.Length)
        {
            throw new InvalidDataException("GF form requires distinct installed innate action bindings.");
        }
    }

    private bool ActivateGuardianFormEntry(string actorId, long invocationId)
    {
        if (GetGuardianForm(actorId) is not { Phase: BattleGuardianFormPhase.Entering } form || form.InvocationId != invocationId)
        {
            return false;
        }
        BattleCombatant actor = GetActor(actorId);
        if (!actor.IsAlive || Outcome != BattleOutcome.Active || guardianForces[form.GuardianForceId].HitPoints == 0)
        {
            ReturnGuardianForm(actor, !actor.IsAlive);
            return false;
        }
        guardianForms![actorId] = form with { Phase = BattleGuardianFormPhase.Active, EntryCompleted = true, RemainingTicks = ProfileForForm(form).LifetimeTicks };
        BindGuardianFormVitals(actor, form);
        RecheckGuardianFormNegativeStatuses(actor);
        BattleNativeAction? previous = nativeAffinityAction;
        try
        {
            nativeAffinityAction = FindAction(BattleAbility.GuardianForce, form.GuardianForceId).NativeAction;
            CompleteGuardianForceAffinity(actor, new(actorId, actorId, BattleActionPhase.Recovery, SelectBattleTarget(actor), BattleAbility.GuardianForce, form.GuardianForceId));
        }
        finally
        {
            nativeAffinityAction = previous;
        }
        actor.Atb = 0;
        actor.IsBusy = false;
        return true;
    }

    private void BindGuardianFormVitals(BattleCombatant actor, BattleGuardianFormState form)
    {
        int id = form.GuardianForceId;
        actor.GuardianFormVitals = new(() => guardianForces[id], hp =>
        {
            BattleGuardianForceVital gf = guardianForces[id];
            guardianForces[id] = NativeGuardianForceShieldResult(gf, checked((ushort)Math.Clamp(hp, 0, gf.MaximumHitPoints)));
        }, ProfileForForm(form).Adjustments, GuardianFormResources(form));
        actor.RefreshNativeHpStatusFlags();
    }

    private void AdvanceGuardianFormTime()
    {
        if (guardianForms is null)
        {
            return;
        }
        foreach (BattleGuardianFormState form in guardianForms.Values.ToArray())
        {
            if (form.Phase == BattleGuardianFormPhase.Active && form.RemainingTicks > 0)
            {
                guardianForms[form.ActorId] = form with { RemainingTicks = form.RemainingTicks - 1 };
            }
        }
    }

    private void DispatchGuardianFormTransitions()
    {
        if (guardianForms is null)
        {
            return;
        }
        foreach (BattleGuardianFormState form in guardianForms.Values.ToArray())
        {
            BattleCombatant actor = GetActor(form.ActorId);
            if (form.Phase == BattleGuardianFormPhase.Returning && HasCommittedGuardianFormAction(actor.Id))
            {
                continue;
            }
            bool terminal = !actor.IsActive || (NativeSavedStatusBits(actor) & 1) != 0 || (NativeBattleStatusBits(actor) & 0x10000) != 0;
            if (terminal || guardianForces[form.GuardianForceId].HitPoints == 0 || Outcome != BattleOutcome.Active)
            {
                ReturnGuardianForm(actor, terminal);
                continue;
            }
            if (form.Phase == BattleGuardianFormPhase.Finishing && ActiveAction?.ActorId != actor.Id && !actions.Any(command => command.ActorId == actor.Id && command.GuardianFormInvocation?.Id == form.InvocationId))
            {
                ReturnGuardianForm(actor, false);
                continue;
            }
            if (form.Phase == BattleGuardianFormPhase.Active && form.RemainingTicks == 0 && (!CanNativeBattleActorAct(actor) || (actor.NativeStatus0 & 0x35) != 0 || (actor.NativeStatus1 & 0x14000) != 0 || (NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence)))
            {
                ReturnGuardianForm(actor, false);
                continue;
            }
            if (form is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 } && actor.IsReady && RequiresNativeCompelledCommand(actor) && CanNativeBattleActorAct(actor) && !ExpandedTimedLimitOwnsInput)
            {
                bool magic = HasNativeAngelWing(actor);
                bool friendly = !magic && (NativeBattleStatusBits(actor) & 0x4000) != 0;
                BattleCombatant[] recipients = Roster(friendly ? BattleSide.Allies : BattleSide.Enemies).Where(IsExpandedAiTarget).ToArray();
                BattleGuardianFormActionBinding? binding = magic ? GetGuardianFormSpellBinding(form) : GetGuardianFormPhysicalBinding(form);
                if (binding is not null && recipients.Length != 0 && (!magic || (NativeSavedStatusBits(actor) & 0x10) == 0))
                {
                    QueueGuardianFormActionCore(actor, form, recipients[NextRandomByte() % recipients.Length], binding, compelled: true);
                }
                else
                {
                    actor.Atb = 0;
                }
                continue;
            }
            if (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks != 0 || actor.IsBusy || ActiveAction?.ActorId == actor.Id || actions.Any(command => command.ActorId == actor.Id))
            {
                continue;
            }
            BattleGuardianFormActionBinding finisher = ProfileForForm(form).Finisher;
            BattleActionDefinition action = ResolveGuardianFormAction(form, finisher);
            BattleCombatant? target = combatants.FirstOrDefault(candidate => IsGuardianFormInnateTarget(actor, candidate, finisher, action));
            if (!CanNativeBattleActorAct(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence) || target is null)
            {
                ReturnGuardianForm(actor, false);
                continue;
            }
            QueueGuardianFormActionCore(actor, form, target, finisher);
        }
    }

    private void ReturnGuardianForm(BattleCombatant actor, bool terminal)
    {
        if (GetGuardianForm(actor.Id) is not { } form)
        {
            return;
        }
        if (expandedPresentationProcess is { } process && form.Phase != BattleGuardianFormPhase.Charging)
        {
            ReturnExpandedHostedGuardianForm(process, actor, terminal);
            return;
        }
        guardianForms![actor.Id] = form with { Phase = BattleGuardianFormPhase.Returning };
        actor.GuardianFormVitals = null;
        if (terminal)
        {
            actor.HumanHitPoints = 0;
        }
        if (form.Phase != BattleGuardianFormPhase.Returning)
        {
            CancelUncommittedGuardianFormCommands(actor);
            actor.Atb = 0;
        }
        if (HasCommittedGuardianFormAction(actor.Id))
        {
            return;
        }
        if (stagingExpandedHostedGuardianForms && form.Phase != BattleGuardianFormPhase.Charging)
        {
            StageExpandedHostedGuardianFormReturn(actor, form);
        }
        if (guardianForceCasts.TryGetValue(actor.Id, out BattleGuardianForceCast? cast))
        {
            guardianForceCasts[actor.Id] = cast with { Command = cast.Command with { GuardianFormInvocation = null } };
            actor.NativeStatus1 &= 0x7FFFFFFF;
        }
        actor.RefreshNativeHpStatusFlags();
        RestoreGuardianFormHumanVital(actor);
        actor.Atb = 0;
        guardianForms.Remove(actor.Id);
        if (ActiveAction is { HasGuardianFormAction: true } action && action.ActorId == actor.Id)
        {
            CompleteActionPresentation(action, "GF returned before the action was committed.");
        }
        Log("GuardianFormReturned", actor.Id, actor.Id, form.GuardianForceId, "GF form returned.");
    }

    private void FailGuardianFormPresentation(string actorId, long invocationId)
    {
        if (GetGuardianForm(actorId) is not { } form || form.InvocationId != invocationId)
        {
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        actor.GuardianFormVitals = null;
        if ((NativeSavedStatusBits(actor) & 1) != 0 || (NativeBattleStatusBits(actor) & 0x10000) != 0)
        {
            actor.HumanHitPoints = 0;
        }
        CancelUncommittedGuardianFormCommands(actor);
        guardianForms!.Remove(actorId);
        BattleAttackCommand[] retained = actions.Where(command => command.ActorId != actorId || !command.HasGuardianFormAction && command.GuardianFormInvocation is null).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
        if (ActiveAction is { } action && action.ActorId == actorId && (action.HasGuardianFormAction || action.GuardianFormInvocation is not null))
        {
            CompleteActionPresentation(action, "GF presentation failed after action admission.");
        }
        if (guardianForceCasts.TryGetValue(actorId, out BattleGuardianForceCast? cast))
        {
            guardianForceCasts[actorId] = cast with { Command = cast.Command with { GuardianFormInvocation = null } };
            actor.NativeStatus1 &= 0x7FFFFFFF;
        }
        actor.Atb = 0;
        actor.NativeCombatantFlags &= ~0xCu;
        actor.IsBusy = guardianForceCasts.ContainsKey(actorId) || QueuedActions.Any(command => command.ActorId == actorId);
        actor.RefreshNativeHpStatusFlags();
        RestoreGuardianFormHumanVital(actor);
        Log("GuardianFormFailed", actorId, actorId, form.GuardianForceId, "GF presentation resources were released after failure.");
    }

    private void RestoreGuardianFormHumanVital(BattleCombatant actor)
    {
        if (expandedPartyPresentation is { } presentation)
        {
            NativeBattlePartyPresentedVital row = RequireExpandedPartyPresentedRow(presentation, actor.Id);
            expandedPartyPresentation = presentation.SetItem(actor.Id, row with { HitPoints = checked((ushort)actor.CurrentHp), MaximumHitPoints = checked((ushort)actor.MaximumHp), DisplayedHitPointsQ12 = actor.CurrentHp << 12 });
        }
    }

    private bool HasCommittedGuardianFormAction(string actorId)
    {
        return expandedHostedEvent is { LogicallyComplete: false, ActivePresentation: { HasGuardianFormAction: true } expanded } && expanded.ActorId == actorId ||
            resolvingNativeAction is { } resolving && resolving.ActorId == actorId && nativeActionCommitDepth != 0;
    }

    private bool IsRetainedGuardianFormReturn(BattleActionPresentation action)
    {
        return action is { HasGuardianFormAction: true, Phase: BattleActionPhase.Recovery } &&
            GetGuardianForm(action.ActorId) is { Phase: BattleGuardianFormPhase.Returning } &&
            expandedHostedEvent is { LogicallyComplete: false, ActivePresentation: { } committed } &&
            committed.ActorId == action.ActorId && committed.TargetId == action.TargetId && committed.Ability == action.Ability && committed.ActionId == action.ActionId &&
            committed.GuardianFormAction == action.GuardianFormAction && committed.GuardianFormLearnedAbility == action.GuardianFormLearnedAbility && committed.GuardianFormInvocation == action.GuardianFormInvocation;
    }

    private void CancelUncommittedGuardianFormCommands(BattleCombatant actor)
    {
        foreach (BattleAttackCommand command in actions.Where(command => command.ActorId == actor.Id && command.HasGuardianFormAction))
        {
            ReleaseMagicReservation(command.MagicReservationId);
        }
        BattleAttackCommand[] retained = actions.Where(command => command.ActorId != actor.Id || !command.HasGuardianFormAction && command.GuardianFormInvocation is null).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
    }

    private void RequireNormalGuardianFormCommands(string actorId)
    {
        if (GetGuardianForm(actorId) is not null)
        {
            throw new InvalidOperationException("A GF form uses its own command bindings.");
        }
    }

    private bool ApplyGuardianFormDamage(BattleCombatant? source, BattleCombatant target, int amount, string label, bool counterEligible, bool publishDamage)
    {
        if (target.GuardianFormVitals is null)
        {
            return false;
        }
        bool terminal = (NativeSavedStatusBits(target) & 1) != 0 || (NativeBattleStatusBits(target) & 0x10000) != 0;
        if (terminal)
        {
            ReturnGuardianForm(target, true);
            return false;
        }
        MarkGuardianFormContact(target);
        int applied = Math.Min(target.CurrentHp, amount);
        target.CurrentHp -= applied;
        if (publishDamage)
        {
            Log("Damage", source?.Id, target.Id, applied, $"{label}: {target.Name} takes {applied} damage.");
        }
        if (target.CurrentHp == 0)
        {
            ReturnGuardianForm(target, false);
        }
        bool storeMemory = nativeContactWorking is null || target.CurrentHp != 0 && NativeSurvivingContactStoresMemory(target);
        if (storeMemory && source is not null && (resolvingNativeAction ?? ActiveAction) is { } action)
        {
            RecordNativeActionMemory(source, target, action);
        }
        if (counterEligible && source is not null && target.IsAlive)
        {
            QueueNativeReaction(target, 2);
        }
        return true;
    }

    private void MarkGuardianFormContact(BattleCombatant actor)
    {
        if (nativeContactWorking is not { } contact)
        {
            return;
        }
        if (contact.CommittingSecondary && contact.Secondary is { } secondary && secondary.Recipient == CreatePresentationModelOwner(actor))
        {
            contact.Secondary = secondary with { HitFlags = (byte)(secondary.HitFlags | 0x20) };
        }
        else if (!contact.CommittingSecondary && contact.Recipient == actor)
        {
            MarkNativeContactFlags(0, 0x20);
        }
    }

    private void ValidateGuardianFormCommand(BattleAttackCommand command, bool completingEntry = false)
    {
        if (command.GuardianFormLearnedAbility is int ability)
        {
            ValidateGuardianFormLearnedCommand(command, ability);
            return;
        }
        if (command.GuardianFormAction is null && command.GuardianFormInvocation is null)
        {
            if (GetGuardianForm(command.ActorId) is { EntryCompleted: true } && command.NativePlayerReaction is null && command.NativeAction is not { Command: 0, ActionId: 2 or 6 } && !(RequiresNativeCompelledCommand(GetActor(command.ActorId)) && command.Ability == BattleAbility.Attack && command.NativeQueuedCommand == 1 && command.NativeAction is null))
            {
                throw new InvalidDataException("Saved transformed actor command lacks its form binding.");
            }
            return;
        }
        BattleGuardianFormState form = GetGuardianForm(command.ActorId) ?? throw new InvalidDataException("Saved GF form command has no active form owner.");
        if (command.GuardianFormInvocation is { } invocation && (invocation.Id != form.InvocationId || invocation.GuardianForceId != form.GuardianForceId || !Enum.IsDefined(invocation.Mode) || invocation.Mode == GuardianFormInvocationMode.FullClassic))
        {
            throw new InvalidDataException("Saved GF invocation differs from its form owner.");
        }
        if (command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.EntryOnly })
        {
            if ((completingEntry ? form.Phase != BattleGuardianFormPhase.Active : form.Phase != BattleGuardianFormPhase.Charging) || command.GuardianFormAction is not null || command.ActorId != command.TargetId || command.Ability != BattleAbility.GuardianForce || command.ActionId != form.GuardianForceId || command.FreeNativeMagic)
            {
                throw new InvalidDataException("Saved GF entrance request is invalid.");
            }
            return;
        }
        if (command.GuardianFormAction is not { } binding)
        {
            throw new InvalidDataException("Saved GF action binding is unsupported.");
        }
        // An already admitted action retains its installed row across changes to the form menu.
        BattleGuardianFormActionBinding policy = ReadGuardianFormBinding(form, binding);
        BattleActionDefinition action = ResolveGuardianFormAction(form, policy);
        if (!form.EntryCompleted || policy.OncePerInvocation && !form.UsedActions.Contains(binding) || action.Ability != command.Ability || action.Id != command.ActionId || command.FreeNativeMagic != (policy.Source == BattleGuardianFormActionSource.Magic && policy.Cost == BattleGuardianFormActionCost.Innate) || policy.Terminal != (command.GuardianFormInvocation?.Mode == GuardianFormInvocationMode.FinisherWithoutEntry))
        {
            throw new InvalidDataException("Saved GF action differs from its profile binding.");
        }
    }

    private void ValidateGuardianFormPresentation(BattleActionPresentation action)
    {
        ValidateGuardianFormCommand(new(action.ActorId, action.TargetId, action.TargetRequest, action.Ability, action.ActionId)
        {
            GuardianFormAction = action.GuardianFormAction,
            GuardianFormLearnedAbility = action.GuardianFormLearnedAbility,
            GuardianFormInvocation = action.GuardianFormInvocation,
            NativeAction = action.NativeAction,
            NativePlayerReaction = action.NativePlayerReaction,
            NativeQueuedCommand = action.NativeQueuedCommand,
            NativeSequence = action.NativeSequence,
            FreeNativeMagic = action.FreeNativeMagic
        }, completingEntry: action is { Phase: BattleActionPhase.Recovery, NativeQueuedCommand: 254, GuardianFormInvocation.Mode: GuardianFormInvocationMode.EntryOnly });
    }

    private void ValidateGuardianFormOwners()
    {
        if (guardianForms is null)
        {
            if (QueuedActions.Any(command => command.GuardianFormInvocation is not null || command.HasGuardianFormAction) || ActiveAction?.GuardianFormInvocation is not null || ActiveAction?.HasGuardianFormAction == true || guardianForceCasts.Values.Any(cast => cast.Command.GuardianFormInvocation is not null))
            {
                throw new InvalidDataException("Saved GF form invocation lacks its feature owner.");
            }
            return;
        }
        foreach (BattleGuardianForceCast cast in guardianForceCasts.Values)
        {
            ValidateGuardianFormCommand(cast.Command);
        }
        foreach (BattleGuardianFormState form in guardianForms.Values)
        {
            if (form.Phase == BattleGuardianFormPhase.Returning && !HasCommittedGuardianFormAction(form.ActorId))
            {
                throw new InvalidDataException("Saved returning GF form lacks its committed action completion owner.");
            }
            bool cast = guardianForceCasts.TryGetValue(form.ActorId, out BattleGuardianForceCast? charge) && charge.Command.GuardianFormInvocation?.Id == form.InvocationId;
            bool queuedEntry = QueuedActions.Any(command => command.ActorId == form.ActorId && command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.EntryOnly } invocation && invocation.Id == form.InvocationId);
            bool finishing = QueuedActions.Any(command => command.ActorId == form.ActorId && command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } invocation && invocation.Id == form.InvocationId) || ActiveAction is { } action && action.ActorId == form.ActorId && action.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } active && active.Id == form.InvocationId;
            if (form.Phase == BattleGuardianFormPhase.Charging && cast == queuedEntry || form.Phase == BattleGuardianFormPhase.Finishing && !finishing || form.Phase == BattleGuardianFormPhase.Active && (cast || queuedEntry || finishing))
            {
                throw new InvalidDataException("Saved GF transformation phase lacks its matching charge or action owner.");
            }
        }
    }

    private BattleGuardianFormsSnapshot? CaptureGuardianForms()
    {
        return GuardianFormsEnabled ? new(true, lastGuardianFormInvocation, guardianForms?.Values.OrderBy(form => form.ActorId, StringComparer.Ordinal).ToImmutableArray() ?? []) : null;
    }

    private void RestoreGuardianForms(BattleGuardianFormsSnapshot? saved, BattleOutcome outcome)
    {
        foreach (BattleCombatant actor in combatants)
        {
            actor.GuardianFormVitals = null;
        }
        guardianForms = null;
        lastGuardianFormInvocation = 0;
        if (saved is null)
        {
            if (GuardianFormsEnabled)
            {
                throw new InvalidDataException("Enabled GF transformation battle lacks its saved feature state.");
            }
            return;
        }
        if (!GuardianFormsEnabled || !saved.Enabled || saved.LastInvocationId < 0 || saved.Forms.IsDefault)
        {
            throw new InvalidDataException("Invalid GF transformation feature metadata.");
        }
        HashSet<int> owners = [];
        foreach (BattleGuardianFormState form in saved.Forms)
        {
            BattleGuardianFormResources[] variants = GuardianFormVariants(form.GuardianForceId);
            BattleGuardianFormResources? resource = form.Phase == BattleGuardianFormPhase.Charging && string.IsNullOrEmpty(form.VariantId) ? variants.FirstOrDefault() :
                variants.SingleOrDefault(candidate => candidate.VariantId == form.VariantId || string.IsNullOrEmpty(form.VariantId) && variants.Length == 1);
            if (resource is null)
            {
                throw new InvalidDataException("Saved GF transformation variant is unsupported.");
            }
            BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.ProfileFor(resource);
            if (!byId.TryGetValue(form.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || actor.HumanHitPoints == 0 && form.Phase != BattleGuardianFormPhase.Returning || !actor.EquipmentIds.Contains(form.GuardianForceId) || !owners.Add(form.GuardianForceId) || form.ProfileId != profile.ProfileId || !Enum.IsDefined(form.Phase) || form.InvocationId < 1 || form.InvocationId > saved.LastInvocationId || form.RemainingTicks < 0 || form.RemainingTicks > profile.LifetimeTicks || form.EntryCompleted != (form.Phase is BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning) || (form.Phase == BattleGuardianFormPhase.Finishing && !form.FinisherAdmitted || form.FinisherAdmitted && form.Phase is not (BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning)) || outcome != BattleOutcome.Active || !guardianForces.ContainsKey(form.GuardianForceId))
            {
                throw new InvalidDataException("Invalid saved GF transformation lifecycle or actor owner.");
            }
            ValidateGuardianFormResources(resource);
            if (form.Phase == BattleGuardianFormPhase.Charging && form.VariantId is not null || form.UsedActions.IsDefault || !form.EntryCompleted && !form.UsedActions.IsEmpty || form.UsedActions.Distinct().Count() != form.UsedActions.Length ||
                form.UsedActions.Any(action => !resource.InnateBindings.Any(binding => binding.Action == action && binding.OncePerInvocation)))
            {
                throw new InvalidDataException("Invalid saved GF form action uses.");
            }
            if (!(guardianForms ??= new(StringComparer.Ordinal)).TryAdd(form.ActorId, form))
            {
                throw new InvalidDataException("Duplicate saved GF transformation actor.");
            }
            if (form.Phase is BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing)
            {
                BindGuardianFormVitals(actor, form);
            }
        }
        lastGuardianFormInvocation = saved.LastInvocationId;
    }
}
