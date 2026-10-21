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
        return GetGuardianForm(actorId) is { EntryCompleted: true, Phase: BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning } form ? BattleGuardianFormProfiles.Find(form.GuardianForceId)!.ModelId : null;
    }

    public bool CanRequestGuardianForm(string actorId, int guardianForceId)
    {
        if (!GuardianFormsEnabled || BattleGuardianFormProfiles.Find(guardianForceId) is null || !byId.TryGetValue(actorId, out BattleCombatant? actor))
        {
            return false;
        }
        return CanChoosePlayerCommand(actorId) && CanNativeBattleActorAct(actor) && (actor.NativeStatus0 & 0x35) == 0 && (actor.NativeStatus1 & 0x14000) == 0 && actor.Abilities.Contains(BattleAbility.GuardianForce) && actor.EquipmentIds.Contains(guardianForceId) && GetGuardianForm(actorId) is null && guardianForces.GetValueOrDefault(guardianForceId) is { Acquired: true, HitPoints: > 0 } && !guardianForceCasts.Values.Any(cast => cast.Command.ActionId == guardianForceId) && !(guardianForms?.Values.Any(form => form.GuardianForceId == guardianForceId) ?? false) && !QueuedActions.Any(command => command.Ability == BattleAbility.GuardianForce && command.ActionId == guardianForceId) && !(ActiveAction is { Ability: BattleAbility.GuardianForce } active && active.ActionId == guardianForceId);
    }

    public BattleActionDefinition GetGuardianFormActionDefinition(string actorId, BattleGuardianFormAction action)
    {
        BattleGuardianFormState form = GetGuardianForm(actorId) ?? throw new InvalidOperationException("The actor has no GF form.");
        BattleGuardianFormActionBinding binding = BattleGuardianFormProfiles.Find(form.GuardianForceId)!.Binding(action);
        if (binding.Source == BattleGuardianFormActionSource.Physical)
        {
            BattleNativeAction native = GuardianFormResources(form.GuardianForceId).PhysicalAction;
            return new(BattleAbility.EnemyAbility, native.ActionId, binding.Name, BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        }
        return FindAction(binding.Source == BattleGuardianFormActionSource.Magic ? BattleAbility.Magic : BattleAbility.GuardianForce, binding.ResourceActionId);
    }

    public void RequestGuardianForm(string actorId, int guardianForceId)
    {
        RequireNativeEntryReady();
        if (!CanRequestGuardianForm(actorId, guardianForceId))
        {
            throw new InvalidOperationException("Transformation requires a ready GF user and an available, living, junctioned GF.");
        }
        BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(guardianForceId)!;
        _ = GuardianFormResources(guardianForceId);
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, guardianForceId);
        if (!action.Available || action.NativeAction is null)
        {
            throw new NotSupportedException("GF transformation requires its installed summon contract.");
        }
        ValidateNativeActionContract(action.NativeAction);
        BattleCombatant actor = GetActor(actorId);
        long invocation = checked(lastGuardianFormInvocation + 1);
        BattleAttackCommand command = new(actorId, actorId, BattleAbility.GuardianForce, guardianForceId)
        {
            NativeQueuedCommand = NativeGraphics is null ? (byte)254 : (byte)3,
            GuardianFormInvocation = new(invocation, guardianForceId, GuardianFormInvocationMode.EntryOnly)
        };
        // Resource and charge prerequisites must fail before input ownership or ATB changes.
        if (actor.GuardianForceCompatibility is not { Count: 16 } || definition.NativeBattleSpeed is null)
        {
            throw new NotSupportedException("GF transformation requires saved compatibility and battle speed.");
        }
        _ = nativeCommandBanks.Allocate(2);
        if (!Enumerable.Range(0, 16).Any(index => !nativeScriptState.Tasks.Allocated(index)))
        {
            throw new InvalidOperationException("GF transformation requires a free charge task slot.");
        }
        if (nativePartyPresentation is not null)
        {
            _ = RequireNativePartyPresentedRow(actor.NativeIndex);
        }
        PrepareGuardianFormPresentation(actor, guardianForceId);
        if (NativeGraphics is null)
        {
            QueueGuardianForce(actor, command);
        }
        else
        {
            EnqueueNativeInputCommand(command);
        }
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
        RequireNativeEntryReady();
        BattleGuardianFormState form = GetGuardianForm(actorId) ?? throw new InvalidOperationException("The actor has no GF form.");
        BattleCombatant actor = GetActor(actorId);
        if (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks == 0 || !CanChoosePlayerCommand(actorId) || !CanNativeBattleActorAct(actor))
        {
            throw new InvalidOperationException("GF form action requires an active, ready form with remaining lifetime.");
        }
        QueueGuardianFormActionCore(actor, form, GetActor(targetId), action);
    }

    private void QueueGuardianFormActionCore(BattleCombatant actor, BattleGuardianFormState form, BattleCombatant target, BattleGuardianFormAction binding)
    {
        BattleGuardianFormActionBinding policy = BattleGuardianFormProfiles.Find(form.GuardianForceId)!.Binding(binding);
        BattleActionDefinition action = GetGuardianFormActionDefinition(actor.Id, binding);
        BattleNativeAction native = action.NativeAction ?? throw new NotSupportedException("GF form action requires its installed action metadata.");
        ValidateNativeActionContract(native);
        if (!target.IsActive || target.Untargetable || !HasValidTargetLifeState(action, target) || (actor.IsPlayer == target.IsPlayer) != action.Allies || policy.Source != BattleGuardianFormActionSource.Physical && ((NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence)))
        {
            throw new InvalidOperationException("GF form action has an unavailable actor or target.");
        }
        bool finisher = policy.Terminal;
        ushort mask = policy.Source == BattleGuardianFormActionSource.Physical ? checked((ushort)(1 << target.NativeIndex)) : finisher ? NativeAutomaticActionTargetMask(native.TargetFlags ?? throw new NotSupportedException("GF finisher requires original target rules.")) : NativeBattleLimitRules.PlayerTargetMask(native.TargetFlags ?? throw new NotSupportedException("GF spell requires original target rules."), target.NativeIndex);
        BattleAttackCommand command = new(actor.Id, target.Id, action.Ability, action.Id)
        {
            NativeAction = finisher ? null : native,
            NativeSequence = policy.Sequence,
            NativeQueuedCommand = finisher ? (byte)254 : native.Command,
            NativeTargetMask = mask,
            FreeNativeMagic = policy.Cost == BattleGuardianFormActionCost.Innate,
            GuardianFormAction = binding,
            GuardianFormInvocation = finisher ? new(form.InvocationId, form.GuardianForceId, GuardianFormInvocationMode.FinisherWithoutEntry) : null
        };
        _ = nativeCommandBanks.Allocate(finisher ? (byte)1 : (byte)2);
        if (finisher)
        {
            PrepareGuardianFormFinisher(actor, form);
        }
        EnqueueNativeInputCommand(command);
        OnNativePlayerCommandSubmitted(actor, finisher ? (byte)3 : command.NativeQueuedCommand!.Value);
        actor.Atb = 0;
        actor.IsBusy = true;
        if (finisher)
        {
            guardianForms![actor.Id] = form with { Phase = BattleGuardianFormPhase.Finishing, FinisherAdmitted = true };
        }
    }

    private BattleGuardianFormResources GuardianFormResources(int id)
    {
        BattleGuardianFormResources resources = definition.GuardianFormResources.SingleOrDefault(resource => resource.GuardianForceId == id) ?? throw new NotSupportedException("GF form requires its prepared original resources.");
        if (resources.ElementDefenses.IsDefault || resources.ElementDefenses.Length != 8 || resources.ElementDefenses.Any(value => value is < 0 or > 2550) || resources.StatusResistances.IsDefault || resources.StatusResistances.Length != 40)
        {
            throw new NotSupportedException("GF form requires its prototype's eight elemental defenses and forty status resistances.");
        }
        return resources;
    }

    private bool BeginCompletedGuardianFormCharge(BattleCombatant actor, BattleGuardianForceCast cast)
    {
        if (cast.Command.GuardianFormInvocation is not { Mode: GuardianFormInvocationMode.EntryOnly } invocation)
        {
            return false;
        }
        BattleGuardianFormState form = GetGuardianForm(actor.Id) ?? throw new InvalidDataException("Transformation charge lost its GF form owner.");
        if (form.Phase == BattleGuardianFormPhase.Charging)
        {
            guardianForms![actor.Id] = form with { Phase = BattleGuardianFormPhase.Entering };
            BeginGuardianFormEntry(actor, form.GuardianForceId, invocation.Id);
        }
        return true;
    }

    private void CompleteGuardianFormEntry(string actorId, long invocationId)
    {
        if (GetGuardianForm(actorId) is not { Phase: BattleGuardianFormPhase.Entering } form || form.InvocationId != invocationId)
        {
            return;
        }
        BattleCombatant actor = GetActor(actorId);
        if (!actor.IsAlive || Outcome != BattleOutcome.Active || guardianForces[form.GuardianForceId].HitPoints == 0)
        {
            ReturnGuardianForm(actor, true);
            return;
        }
        guardianForms![actorId] = form with { Phase = BattleGuardianFormPhase.Active, EntryCompleted = true, RemainingTicks = BattleGuardianFormProfiles.Find(form.GuardianForceId)!.LifetimeTicks };
        BindGuardianFormVitals(actor, form.GuardianForceId);
        BattleNativeAction? previous = nativeAffinityAction;
        try
        {
            nativeAffinityAction = FindAction(BattleAbility.GuardianForce, form.GuardianForceId).NativeAction;
            CompleteGuardianForceAffinity(actor, new(actorId, actorId, BattleActionPhase.Recovery, 0, BattleAbility.GuardianForce, form.GuardianForceId));
        }
        finally
        {
            nativeAffinityAction = previous;
        }
        actor.Atb = 0;
        actor.IsBusy = false;
        if (NativeGraphics is not null)
        {
            SetNativeActionEvent(actor, 1);
            nativeActionScratch = nativeActionScratch with { Command = 254, Action = checked((ushort)(64 + form.GuardianForceId)), ActorIndex = checked((byte)actor.NativeIndex) };
            QueueNativeActionClockCompletion(0x47DE70);
        }
        else
        {
            CompleteNativeActionStatuses(actor);
        }
        Log("GuardianFormActive", actorId, actorId, form.GuardianForceId, "GF form entrance completed.");
    }

    private void BindGuardianFormVitals(BattleCombatant actor, int id)
    {
        actor.GuardianFormVitals = new(() => guardianForces[id], hp =>
        {
            BattleGuardianForceVital gf = guardianForces[id];
            guardianForces[id] = NativeGuardianForceShieldResult(gf, checked((ushort)Math.Clamp(hp, 0, gf.MaximumHitPoints)));
        }, BattleGuardianFormProfiles.Find(id)!.Adjustments, GuardianFormResources(id));
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
            bool terminal = !actor.IsActive || (NativeSavedStatusBits(actor) & 1) != 0 || (NativeBattleStatusBits(actor) & 0x10000) != 0;
            if (terminal || guardianForces[form.GuardianForceId].HitPoints == 0 || Outcome != BattleOutcome.Active || HasPendingNativeOutcome)
            {
                ReturnGuardianForm(actor, terminal);
                continue;
            }
            if (form.Phase == BattleGuardianFormPhase.Active && form.RemainingTicks == 0 && (!CanNativeBattleActorAct(actor) || (actor.NativeStatus0 & 0x35) != 0 || (actor.NativeStatus1 & 0x14000) != 0 || (NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence)))
            {
                ReturnGuardianForm(actor, false);
                continue;
            }
            if (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks != 0 || actor.IsBusy || ActiveAction is not null || nativeTimedAction is not null || QueuedActions.Count != 0 || NativeGraphics is { } graphics && NativeOriginalEffectBusy(graphics))
            {
                continue;
            }
            BattleCombatant? target = combatants.FirstOrDefault(candidate => candidate.IsAlive && !candidate.IsPlayer && !candidate.Untargetable);
            if (!CanNativeBattleActorAct(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence) || target is null)
            {
                ReturnGuardianForm(actor, false);
                continue;
            }
            QueueGuardianFormActionCore(actor, form, target, BattleGuardianFormAction.HellFire);
        }
    }

    private void ReturnGuardianForm(BattleCombatant actor, bool terminal)
    {
        if (GetGuardianForm(actor.Id) is not { } form)
        {
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
            RemoveNativeUiReady(actor);
            actor.Atb = 0;
        }
        if (HasCommittedGuardianFormAction(actor.Id))
        {
            return;
        }
        ReturnGuardianFormPresentation(actor, form);
        if (guardianForceCasts.TryGetValue(actor.Id, out BattleGuardianForceCast? cast))
        {
            guardianForceCasts[actor.Id] = cast with { Command = cast.Command with { GuardianFormInvocation = null } };
            actor.NativeStatus1 &= 0x7FFFFFFF;
        }
        actor.RefreshNativeHpStatusFlags();
        RestoreGuardianFormHumanVital(actor);
        actor.Atb = 0;
        guardianForms.Remove(actor.Id);
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
        BattleAttackCommand[] retained = actions.Where(command => command.ActorId != actorId || command.GuardianFormAction is null && command.GuardianFormInvocation is null).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
        if (ActiveAction is { } action && action.ActorId == actorId && (action.GuardianFormAction is not null || action.GuardianFormInvocation is not null))
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
        RemoveNativeUiReady(actor);
        Log("GuardianFormFailed", actorId, actorId, form.GuardianForceId, "GF presentation resources were released after failure.");
    }

    private void RestoreGuardianFormHumanVital(BattleCombatant actor)
    {
        if (nativePartyPresentation is { } presentation)
        {
            NativeBattlePartyPresentedVital row = RequireNativePartyPresentedRow(actor.NativeIndex);
            nativePartyPresentation = presentation with { Rows = presentation.Rows.SetItem(actor.NativeIndex, row with { HitPoints = checked((ushort)actor.CurrentHp), MaximumHitPoints = checked((ushort)actor.MaximumHp), DisplayedHitPointsQ12 = actor.CurrentHp << 12 }) };
        }
    }

    private bool HasCommittedGuardianFormAction(string actorId)
    {
        return ActiveAction is { NativeHeaderIndex: not null } action && action.ActorId == actorId || actions.Any(command => command.ActorId == actorId && command.NativeHeaderIndex is not null) || resolvingNativeAction is { } resolving && resolving.ActorId == actorId && nativeActionCommitDepth != 0;
    }

    private void CancelUncommittedGuardianFormCommands(BattleCombatant actor)
    {
        BattleAttackCommand[] retained = actions.Where(command => command.ActorId != actor.Id || command.NativeHeaderIndex is not null || command.GuardianFormAction is null && command.GuardianFormInvocation is null).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in retained)
        {
            actions.Enqueue(command);
        }
        for (int bank = 0; bank < 3; bank++)
        {
            for (int slot = 0; slot < 11; slot++)
            {
                NativeBattleCommandLocation location = new(checked((byte)bank), checked((byte)slot));
                if (nativeDispatchCommandLocation == location)
                {
                    continue;
                }
                BattleAttackCommand? command = nativeCommandBanks.Banks[bank].LogicalCommands[2 * slot];
                if (command?.ActorId == actor.Id && command.NativeHeaderIndex is null && (command.GuardianFormAction is not null || command.GuardianFormInvocation is not null))
                {
                    nativeCommandBanks = nativeCommandBanks.Release(location);
                }
            }
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
        if (contact.CommittingSecondary && contact.Secondary is { } secondary && secondary.ActorIndex == actor.NativeIndex)
        {
            contact.Secondary = secondary with { HitFlags3 = (byte)(secondary.HitFlags3 | 0x20) };
        }
        else if (!contact.CommittingSecondary && contact.Recipient == actor)
        {
            MarkNativeContactFlags(0, 0x20);
        }
    }

    private void ValidateGuardianFormCommand(BattleAttackCommand command)
    {
        if (command.GuardianFormAction is null && command.GuardianFormInvocation is null)
        {
            if (GetGuardianForm(command.ActorId) is { EntryCompleted: true } && command.NativeCallback is null && command.NativePlayerReaction is null && command.NativeAction is not { Command: 0, ActionId: 2 or 6 } && !(RequiresNativeCompelledCommand(GetActor(command.ActorId)) && command.Ability == BattleAbility.Attack && command.NativeQueuedCommand == 1 && command.NativeAction is null))
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
            if (form.Phase != BattleGuardianFormPhase.Charging || command.GuardianFormAction is not null || command.ActorId != command.TargetId || command.Ability != BattleAbility.GuardianForce || command.ActionId != form.GuardianForceId || command.FreeNativeMagic)
            {
                throw new InvalidDataException("Saved GF entrance request is invalid.");
            }
            return;
        }
        if (command.GuardianFormAction is not { } binding || !Enum.IsDefined(binding))
        {
            throw new InvalidDataException("Saved GF action binding is unsupported.");
        }
        BattleGuardianFormActionBinding policy = BattleGuardianFormProfiles.Find(form.GuardianForceId)!.Binding(binding);
        BattleActionDefinition action = GetGuardianFormActionDefinition(command.ActorId, binding);
        if (!form.EntryCompleted || action.Ability != command.Ability || action.Id != command.ActionId || command.FreeNativeMagic != (policy.Cost == BattleGuardianFormActionCost.Innate) || policy.Terminal != (command.GuardianFormInvocation?.Mode == GuardianFormInvocationMode.FinisherWithoutEntry))
        {
            throw new InvalidDataException("Saved GF action differs from its profile binding.");
        }
    }

    private void ValidateGuardianFormPresentation(BattleActionPresentation action)
    {
        ValidateGuardianFormCommand(new(action.ActorId, action.TargetId, action.Ability, action.ActionId)
        {
            GuardianFormAction = action.GuardianFormAction,
            GuardianFormInvocation = action.GuardianFormInvocation,
            NativeAction = action.NativeAction,
            NativePlayerReaction = action.NativePlayerReaction,
            FreeNativeMagic = action.FreeNativeMagic
        });
    }

    private void ValidateGuardianFormOwners()
    {
        if (NativeGraphics?.GuardianFormHumanFocalToRestore is not null && (!GuardianFormsEnabled || lastGuardianFormInvocation == 0))
        {
            throw new InvalidDataException("Saved GF camera restoration lacks its feature invocation owner.");
        }
        if (guardianForms is null)
        {
            if (NativeGraphics?.GuardianFormPresentation is not null || QueuedActions.Any(command => command.GuardianFormInvocation is not null || command.GuardianFormAction is not null) || ActiveAction?.GuardianFormInvocation is not null || ActiveAction?.GuardianFormAction is not null || guardianForceCasts.Values.Any(cast => cast.Command.GuardianFormInvocation is not null))
            {
                throw new InvalidDataException("Saved GF form invocation lacks its feature owner.");
            }
            return;
        }
        if (NativeGraphics?.GuardianFormPresentation is { } presentation)
        {
            presentation.Validate();
            BattleGuardianFormState owner = GetGuardianForm(presentation.ActorId) ?? throw new InvalidDataException("Saved GF presentation lacks its logical form owner.");
            bool queuedFinisher = owner.Phase == BattleGuardianFormPhase.Finishing && presentation.Mode == GuardianFormInvocationMode.EntryOnly && presentation.Phase == 3 && QueuedActions.Any(command => command.ActorId == owner.ActorId && command.NativeHeaderIndex is null && command.GuardianFormInvocation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry } invocation && invocation.Id == owner.InvocationId);
            if (owner.InvocationId != presentation.InvocationId || GetActor(owner.ActorId).NativeIndex != presentation.ActorIndex || owner.Phase == BattleGuardianFormPhase.Entering && presentation.Mode != GuardianFormInvocationMode.EntryOnly || owner.Phase == BattleGuardianFormPhase.Finishing && presentation.Mode != GuardianFormInvocationMode.FinisherWithoutEntry && !queuedFinisher)
            {
                throw new InvalidDataException("Saved GF presentation differs from its logical invocation.");
            }
        }
        foreach (BattleGuardianForceCast cast in guardianForceCasts.Values)
        {
            ValidateGuardianFormCommand(cast.Command);
        }
        foreach (BattleGuardianFormState form in guardianForms.Values)
        {
            if (NativeGraphics is not null && form.Phase is BattleGuardianFormPhase.Entering or BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing && NativeGraphics.GuardianFormPresentation?.ActorId != form.ActorId)
            {
                throw new InvalidDataException("Saved GF transformation phase lacks its native presentation owner.");
            }
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
            BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(form.GuardianForceId) ?? throw new InvalidDataException("Saved GF transformation profile is unsupported.");
            if (!byId.TryGetValue(form.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || actor.HumanHitPoints == 0 && form.Phase != BattleGuardianFormPhase.Returning || !actor.EquipmentIds.Contains(form.GuardianForceId) || !owners.Add(form.GuardianForceId) || form.ProfileId != profile.ProfileId || !Enum.IsDefined(form.Phase) || form.InvocationId < 1 || form.InvocationId > saved.LastInvocationId || form.RemainingTicks < 0 || form.RemainingTicks > profile.LifetimeTicks || form.EntryCompleted != (form.Phase is BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning) || (form.Phase == BattleGuardianFormPhase.Finishing && !form.FinisherAdmitted || form.FinisherAdmitted && form.Phase is not (BattleGuardianFormPhase.Finishing or BattleGuardianFormPhase.Returning)) || outcome != BattleOutcome.Active || !guardianForces.ContainsKey(form.GuardianForceId))
            {
                throw new InvalidDataException("Invalid saved GF transformation lifecycle or actor owner.");
            }
            _ = GuardianFormResources(form.GuardianForceId);
            if (!(guardianForms ??= new(StringComparer.Ordinal)).TryAdd(form.ActorId, form))
            {
                throw new InvalidDataException("Duplicate saved GF transformation actor.");
            }
            if (form.Phase is BattleGuardianFormPhase.Active or BattleGuardianFormPhase.Finishing)
            {
                BindGuardianFormVitals(actor, form.GuardianForceId);
            }
        }
        lastGuardianFormInvocation = saved.LastInvocationId;
    }
}
