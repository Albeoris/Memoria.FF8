using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleContact(string RecipientId, NativeBattleContactPacket Packet)
{
    public NativeBattleContactHalf Contact => Packet.Primary;
}

public sealed partial class BattleSession
{
    private bool deferringExpandedImpacts;
    public bool SupportsExpandedAction(BattleActionDefinition action)
    {
        if (action.NativeAction is not { } native)
        {
            return false;
        }
        if (action.Ability == BattleAbility.Item && native.AttackType == 14)
        {
            return SupportsExpandedIndependentGuardianItem(native);
        }
        return action.Ability == BattleAbility.GuardianForce && native is { Command: 254, KernelSection: 2, HitCount: 1, TargetFlags: byte flags } &&
            action.Id is >= 0 and < 16 && native.ActionId == action.Id + 64 && native.KernelIndex == action.Id &&
            native.AttackType is 11 or 15 or 20 or 29 && (flags & 0x30) == 0 ||
            ExpandedBattleActionRules.SupportsPlayerAction(action.Ability, native);
    }

    private void RequireExpandedAction(BattleActionDefinition action)
    {
        if (!SupportsExpandedAction(action))
        {
            throw new NotSupportedException("The command requires a formula or invocation outside the expanded battle contact path.");
        }
    }

    private void StartExpandedAction(BattleCombatant actor, BattleAttackCommand command)
    {
        command = RevalidateSmartAttack(actor, command);
        if (command.GuardianFormAction is not null && GetGuardianForm(actor.Id) is null)
        {
            actor.IsBusy = false;
            return;
        }
        if (command.NativeLimitCallback == NativeLimitCallback.ShotEnding && !ActionHasValidTarget(command.TargetRequest, command.NativeGraphic))
        {
            // The original command admission cancels an empty close before producing its graphics header.
            CompleteExpandedProducedCommand(NativeCommandPresentation(command), "Shot ending cancelled without a target.");
            return;
        }
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.TargetRequest) && !CanNativeSupportCommandAct(actor, command.NativePlayerReaction) ||
            !CanExecuteMagicReservation(actor, command) || command.NativeGraphic is null && command.MagicCasts.IsDefaultOrEmpty && !IsShotCallback(command.NativeLimitCallback) && ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.TargetRequest, command.NativeAction).Length == 0)
        {
            RefundCancelledNativeItem(command);
            actor.IsBusy = false;
            return;
        }
        if (command.Ability == BattleAbility.Item || IsExpandedIndependentSupport(command.NativeAction, command.NativePlayerReaction))
        {
            command = PrepareIndependentGuardianItem(actor, command);
        }
        ActiveAction = NativeCommandPresentation(command);
        BeginNativeBattleActionPause();
        string name = command.NativeGraphic is not null ? "Native graphical effect" : command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log("Windup", actor.Id, command.TargetId, 0, $"{actor.Name}: {name}.");
    }

    private void AdvanceExpandedAction(BattleActionPresentation command)
    {
        if (expandedHostedEvent is not null)
        {
            return;
        }
        BattleCombatant actor = GetActor(command.ActorId);
        if (command.GuardianFormAction is not null && GetGuardianForm(actor.Id) is null)
        {
            CompleteActionPresentation(command, "GF returned before the action was committed.");
            return;
        }
        PrepareAndPublishExpandedAction(command);
    }

    private void CommitExpandedAction(BattleActionPresentation command)
    {
            RequireOrdinaryExpandedHostedCommand(command);
        BattleCombatant actor = GetActor(command.ActorId);
        BattleCombatant target = GetActor(command.TargetId);
        if (!command.MagicCasts.IsDefaultOrEmpty)
        {
            ResolveExpandedMagicGroup(actor, command);
            return;
        }
        BattleNativeAction? graphicAction = command.NativeGraphic is { } graphic ? NativeGraphicContactAction(actor, graphic.MagicId) : null;
        BattleCombatant[] recipients = ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.TargetRequest, graphicAction ?? command.NativeAction);
        bool silencedMagic = (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2) && (NativeSavedStatusBits(actor) & 0x10) != 0;
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.TargetRequest) && !CanNativeSupportCommandAct(actor, command.NativePlayerReaction) ||
            !CanExecuteMagicReservation(actor, new(command.ActorId, command.TargetId, command.TargetRequest, command.Ability, command.ActionId) { MagicReservationId = command.MagicReservationId }, admitSilencedInvocation: true) || graphicAction is null && recipients.Length == 0)
        {
            if (silencedMagic && (command.FreeNativeMagic && HasNativeAngelWing(actor) || command.TargetRequest.Eligibility == BattleTargetEligibility.CompelledAction) && CanNativeBattleActorAct(actor))
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(100), BattleTextIdentity.Global(100));
            }
            RefundCancelledNativeItem(command);
            CompleteExpandedProducedCommand(command, "Expanded action cancelled.");
            return;
        }
        if (command.Ability == BattleAbility.Attack && actor.NativeAttack is null)
        {
                throw new NotSupportedException("Hosted ordinary Attack requires its installed native attack record.");
        }
        BattleActionDefinition? drawSpell = null;
        BattleActionDefinition? stockDraw = null;
        if (command.Ability == BattleAbility.Draw)
        {
            if (command.DrawCast)
            {
                drawSpell = FindAction(BattleAbility.Magic, command.ActionId);
                recipients = ResolveBattleTargets(command.TargetRequest, sampleRandom: false);
                if (recipients.Length == 0)
                {
                    CompleteExpandedProducedCommand(command, "Expanded Draw Cast has no supported recipient.");
                    return;
                }
            }
            BattleAttackCommand draw = PrepareNativeDrawCommand(actor, new(actor.Id, target.Id, command.TargetRequest, BattleAbility.Draw, command.ActionId, command.DrawCast) { DrawCastTargetId = command.DrawCastTargetId });
            command = command with { NativeDraw = draw.NativeDraw, NativeAction = draw.NativeAction, TargetRequest = draw.TargetRequest };
            if (draw.NativeDraw!.Failed || drawSpell is null)
            {
                BattleNativeAction stock = NativeStockDrawAction(draw.NativeDraw);
                stockDraw = new(BattleAbility.Draw, command.ActionId, "Draw", BattleActionEffect.Native, 0, false) { NativeAction = stock };
                drawSpell = null;
                recipients = [target];
            }
        }

        int? independentFailure = command.IndependentGuardian?.FailureMessage;
        BattleActionDefinition action = graphicAction is null
            ? stockDraw ?? drawSpell ?? ExpandedActionDefinition(actor, command)
            : new(BattleAbility.EnemyAbility, graphicAction.ActionId, graphicAction.Name!, BattleActionEffect.Native, 0, false) { NativeAction = graphicAction };
        if (command.IndependentGuardian is { Action: { } child } independent)
        {
            action = action with { Name = child.Name ?? action.Name, Level = independent.Level, NativeAction = child };
        }
        BattleNativeAction originalNative = action.NativeAction!;
        if (originalNative is { Command: 246, ActionId: 43 })
        {
            PrepareDoomExpiration(actor);
        }
        BattleNativeAction native = originalNative;
        NativeBattleDeferredContactKind deferred = NativeDeferredContact(actor, command, native);
        bool hostedGunblade = deferred == NativeBattleDeferredContactKind.OrdinaryGunblade;
        bool hostedShotControl = deferred == NativeBattleDeferredContactKind.ShotControl;
        if (!hostedGunblade && command.GuardianFormAction is null && IsGunbladeAttack(actor, command.Ability))
        {
            // 485160 recomputes the ordinary trigger contact with the installed weapon and type 10.
            native = native with { Command = command.NativeQueuedCommand == 12 ? (byte)248 : (byte)253, AttackType = 10, HitCount = 1, Critical = 0, DamagePresentation = 4 };
            command = command with { GunbladeTriggerTiming = ReadGunbladeTiming(actor, command) };
        }
        bool miniMog = IsExpandedMiniMogAction(native);
        int? miniMogFailure = miniMog ? PrepareNativeMiniMog() : null;
        if (command.Ability == BattleAbility.NativeCommand && IsExpandedTargetedSpecial(native))
        {
            // The expanded host prepares and applies the contact at the same callback, after windup revalidation.
            BattleNativeSpecialContext special = PrepareNativeSpecialTarget(recipients.Single(), native.AttackType);
            command = command with { NativeSpecial = special };
        }
        if (command.Ability == BattleAbility.GuardianForce && command.TargetRequest.Selection is BattleTargetSelection.RandomOnSide)
        {
            BattleCombatant recipient = ResolveBattleTargets(command.TargetRequest).Single();
            recipients = [recipient];
            command = command with { TargetId = recipient.Id, TargetRequest = SelectBattleTarget(recipient, command.TargetRequest.AllowsKnockedOutAllies, command.TargetRequest.Eligibility) };
        }
        bool hostedSilence = NativeMagicInvocationSilenced(actor, command, originalNative);
        bool failedSpecial = (independentFailure.HasValue || miniMogFailure.HasValue || NativeSpecialInvocationFailed(command, originalNative));
        NativeBattleActionHeaderPreparation hostedHeader = BuildExpandedActionHeaderPreparation(command, originalNative, miniMogFailure.HasValue);
        bool hostedGuardian = command.Ability == BattleAbility.GuardianForce && hostedHeader is { Command: 254 };
        if (hostedGuardian)
        {
            if (hostedHeader.Source is not BattlePartyModelOwner || originalNative is not { Command: 254, KernelSection: 2 })
            {
                throw new InvalidDataException("Hosted Guardian Force requires its original command and a current party caster.");
            }
            if (nativeBattleUiRandom is null || native.KernelSection != 2 || native.KernelIndex != hostedHeader.Action - 64 || native.RawRecord.Length != 132 ||
                FindAction(BattleAbility.GuardianForce, hostedHeader.Action - 64).NativeGuardianBoostAvailable is null)
            {
                throw new NotSupportedException("Native Guardian Force requires its installed Boost profile and battle UI random owner.");
            }
        }
        BattleActionPresentation resolved = command with { NativeAction = miniMog && !failedSpecial ? native with { ActionId = (ushort)native.KernelIndex } : native };
        bool hostedContextOnly = ReadExpandedHostedContactDisposition(resolved, hostedHeader) == ExpandedHostedContactDisposition.ContextOnly;
        BattleActionPresentation formulaCommand = resolved;
        BattleActionDefinition formulaAction = miniMog && !failedSpecial ? action with { NativeAction = resolved.NativeAction } : action;
        if (failedSpecial || hostedSilence)
        {
            native = NativeFailedInvocationControl(originalNative);
            formulaCommand = resolved with { NativeQueuedCommand = 0, NativeAction = native };
            formulaAction = action with { NativeAction = native };
        }
        bool sampleTargets = command.Ability != BattleAbility.GuardianForce && command.TargetRequest.Selection is BattleTargetSelection.RandomOnSide;
        var hitPlan = PlanNativeHits(actor, formulaCommand, native, sampleTargets ? null : () => recipients);
        if (hostedGunblade && (hitPlan.Length != 1 || hitPlan[0].Targets.Length != 1))
        {
            throw new InvalidDataException("Ordinary hosted Gunblade requires its single placeholder recipient.");
        }
        bool hasReflector = hitPlan.Any(hit => hit.Targets.Any(recipient => NativeActionReflects(recipient, native)));
        BattleNativeAction? reflected = null;
        if (hasReflector)
        {
            // 487DF0 reads a magic record for the first reflected contact.
            reflected = definition.NativeActions.SingleOrDefault(action => action.Command == 247 && action.ActionId == unchecked((byte)native.ActionId) && action.CrisisLevel is null);
            if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected) && !ExpandedBattleActionRules.SupportsMagicCure(reflected))
            {
                RefundCancelledNativeItem(command);
                CompleteExpandedProducedCommand(command, "The expanded host has no supported reflected spell record.");
                return;
            }
        }

        bool independentSupport = IsExpandedIndependentSupport(native, command.NativePlayerReaction);
        byte? eventLane = producingExpandedAiCallback ? nativeActionScratch.EventLane : actor.IsPlayer && !independentSupport && originalNative.Command != 246 ? (byte)2 : (byte)0;
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = hostedGuardian, EventLane = eventLane, Command = stockDraw is not null || native.Command is 0 or 248 or 253 ? native.Command : command.NativeQueuedCommand ?? native.Command, Action = native.ActionId };
        if (command.Ability == BattleAbility.GuardianForce)
        {
            nativeActionScratch = nativeActionScratch with { GuardianFormulaE1E = !hostedGuardian, GuardianBoostPercent = hostedGuardian ? (byte)0 : (byte)100 };
        }
        ImmutableArray<ExpandedBattleContact> contacts;
        ImmutableArray<ExpandedBattleContact> reflectedContacts = [];
        BattleActionPresentation? reflectedCommand = null;
        NativeBattleActionHeaderPreparation? reflectedHeader = null;
        (BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts, NativeBattleActionHeaderPreparation Header)? phoenixRevival = null;
        bool phoenixPinion = command is { Ability: BattleAbility.Item, ActionId: 31, IndependentGuardian.Action: not null };
        ReserveExpandedAiInvocation(resolved, hostedHeader);
        resolvingNativeAction = formulaCommand;
        expandedRecoverSourceHp = native.AttackType == 25 ? actor.CurrentHp : null;
        if (!producingExpandedAiCallback)
        {
            BeginNativeReactionBatch();
        }
        nativeActionCommitDepth++;
        try
        {
            if (command.NativeLimitCallback == NativeLimitCallback.ShotEffect || command.NativeSlots is not null)
            {
                PrepareNativeLimitCommand(new(command.ActorId, command.TargetId, command.TargetRequest, command.Ability, command.ActionId)
                {
                    NativeQueuedCommand = command.NativeQueuedCommand,
                    NativeAction = native
                });
            }
            if (phoenixPinion)
            {
                SetNativeSavedBattleFlag(4);
            }
            if (hostedSilence && definition.NativeGlobalMessages.Length != 0)
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(100));
            }
            deferringExpandedImpacts = !hostedContextOnly;
            NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, formulaCommand, formulaAction, formulaCommand.NativeAction!, hitPlan, preliminary: hostedGunblade || hostedGuardian || hostedShotControl);
            contacts = ToHostedContacts(produced);
            deferringExpandedImpacts = false;
            if (command.NativeDraw is { Failed: true } || failedSpecial || hostedSilence)
            {
                contacts = [];
            }
            if (independentFailure is int guardianMessage)
            {
                PublishIndependentGuardianFailure(actor, target, guardianMessage);
            }
            else if (failedSpecial)
            {
                int message = miniMogFailure ?? (originalNative.AttackType == 17 ? 50 : 112);
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(message), BattleTextIdentity.Global(message));
            }
            else if (miniMog && !contacts.IsEmpty)
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(54), BattleTextIdentity.Global(54));
            }
            (reflectedCommand, reflectedContacts, reflectedHeader) = ResolveExpandedReflection(actor, resolved, action, produced.Reflector, reflected);
            resolvingNativeAction = resolved;
            nativeAffinityAction = HasNativeAffinityAction(actor, resolved, native) ? native : null;
            CompleteNativeActionOwners(actor, resolved, hostedSilence);
            if (command.NativeDraw is { Failed: false, Selection: >= 64 })
            {
                RefreshNativeEnemyDrawSlots();
            }
            if (phoenixPinion)
            {
                phoenixRevival = ResolveExpandedPhoenixRevival(actor, resolved);
            }
        }
        finally
        {
            deferringExpandedImpacts = false;
            nativeActionCommitDepth--;
            expandedRecoverSourceHp = null;
            resolvingNativeAction = null;
        }
        BattlePendingEnemyDraw? hostedDraw = null;
        if (!hostedContextOnly)
        {
            if (!contacts.IsEmpty || !reflectedContacts.IsEmpty)
            {
                hostedDraw = TakePendingHostedEnemyDraw();
            }
        }
        else
        {
            ExpandedBattleContact? first = contacts.FirstOrDefault() ?? reflectedContacts.FirstOrDefault() ?? phoenixRevival?.Contacts.FirstOrDefault();
            if (first is not null)
            {
                CommitExpandedEnemyDraw(first.RecipientId, ref pendingEnemyDraw);
            }
        }
        if (hostedContextOnly && command.NativeGraphic is not null)
        {
            PublishExpandedImpacts(resolved, contacts);
        }
        StageExpandedHostedInvocationCore(resolved, hostedHeader, contacts, contacts.IsEmpty ? null : hostedDraw, pendingGunblade: hostedGunblade, pendingGuardian: hostedGuardian);
        StageExpandedHostedShotReadiness(resolved, hostedHeader);
        if (reflectedCommand is not null)
        {
            StageExpandedHostedInvocation(reflectedCommand, reflectedHeader!, reflectedContacts, contacts.IsEmpty ? hostedDraw : null);
        }
        if (phoenixRevival is { } revival)
        {
            StageExpandedHostedInvocation(revival.Command, revival.Header, revival.Contacts, null);
        }
        if (!producingExpandedAiCallback)
        {
            // Contact preparation has consumed the special result into the staged invocation.
            ActiveAction = command with { Phase = BattleActionPhase.Recovery, MagicReservationId = null, NativeSpecial = null };
        }
    }

    private void CompleteExpandedProducedCommand(BattleActionPresentation command, string message)
    {
        if (!producingExpandedAiCallback)
        {
            CompleteActionPresentation(command, message);
        }
    }

    private static void ValidateExpandedEffectContacts(BattleModelOwner source, ImmutableArray<ExpandedBattleContact> contacts, BattleModelRoster roster)
    {
        foreach (ExpandedBattleContact contact in contacts)
        {
            BattleModelOwner recipient = contact.Contact.Recipient;
            if (!roster.Contains(recipient) || !StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(recipient), contact.RecipientId) ||
                contact.Packet.Secondary is { } secondary && secondary.Recipient != source)
            {
                throw new InvalidDataException("The expanded contact differs from its bound recipient or source model.");
            }
        }
    }

    private NativeBattleContactHalf? ReadExpandedSecondaryContact(BattleActionPresentation command, NativeBattleContactPacket packet)
    {
        if (packet.Secondary is { } secondary && secondary.Recipient != CreatePresentationModelOwner(GetActor(command.ActorId)))
        {
            throw new InvalidDataException("The expanded contact secondary recipient must name its actual caster.");
        }
        return packet.Secondary;
    }

    private void ValidateExpandedEffectContactCompletion()
    {
        ExpandedHostedInvocation invocation = CurrentExpandedHostedInvocation ?? throw new InvalidDataException("The retired expanded effect has no current invocation.");
        if (invocation.ContactDisposition != ExpandedHostedContactDisposition.ContextOnly &&
            (expandedInvocationProgress is not { } progress || progress.Occurrences.Count != progress.ExpectedContacts))
        {
            NativeBattleActionHeaderPreparation header = invocation.Header;
            int delivered = expandedInvocationProgress?.Occurrences.Count ?? 0;
            throw new InvalidDataException($"Expanded effect {header.Effect} for native command {header.Command}/{header.Action} completed with {delivered} of {invocation.ContactCount} prepared contacts.");
        }
        if (!invocation.RoutedGraphicsContacts.IsEmpty && expandedInvocationProgress?.PresentedRoutedContacts != invocation.RoutedGraphicsContacts.Length)
        {
            throw new InvalidDataException("The native action completed before publishing its routed graphics contacts.");
        }
    }

    private (BattleActionPresentation? Command, ImmutableArray<ExpandedBattleContact> Contacts, NativeBattleActionHeaderPreparation? HostedHeader) ResolveExpandedReflection(BattleCombatant actor, BattleActionPresentation source, BattleActionDefinition action, BattleCombatant? reflector, BattleNativeAction? reflected)
    {
        if (reflector is null || reflected is null)
        {
            return (null, [], null);
        }
        BattleSide side = reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies;
        if (ReadNativeRandomActionTarget(side) is not { } redirected)
        {
            return (null, [], null);
        }
        bool randomReflection = (reflected.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleActionPresentation command = source with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, TargetId = redirected.Id, TargetRequest = SelectBattleTarget(redirected, source.TargetRequest.AllowsKnockedOutAllies), DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeSequence = 0, NativeQueuedCommand = 247, NativeAction = reflected, FreeNativeMagic = true, MagicReservationId = null };
        RecordDiagnosticReflection(source, command, reflector, redirected, action.NativeAction!);
        var reflectionPlan = PlanNativeHits(actor, command, reflected, () => randomReflection && ReadNativeRandomActionTarget(side) is { } randomTarget ? [randomTarget] : [redirected]);
        BattleActionDefinition reflectionAction = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? action.Name, BattleActionEffect.Native, reflected.Power, actor.IsPlayer == redirected.IsPlayer) { NativeAction = reflected };
        NativeBattleActionScratch sourceScratch = nativeActionScratch;
        resolvingNativeAction = command;
        nativeActionScratch = nativeActionScratch with { Command = 247, Action = reflected.ActionId };
        NativeBattleActionHeaderPreparation hostedHeader = BuildExpandedActionHeaderPreparation(command, reflected);
        ReserveExpandedAiInvocation(command, hostedHeader);
        try
        {
            deferringExpandedImpacts = true;
            NativeContactPlanResult produced = ExecuteHostedContactPlan(actor, command, reflectionAction, reflected, reflectionPlan);
            return (command, ToHostedContacts(produced), hostedHeader);
        }
        finally
        {
            deferringExpandedImpacts = false;
            nativeActionScratch = nativeActionScratch with { Command = sourceScratch.Command, Action = sourceScratch.Action };
            resolvingNativeAction = source;
        }
    }

    private NativeContactPlanResult ExecuteHostedContactPlan(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, (BattleCombatant[] Targets, BattleModelOwner? CoverSource)[] plan, bool preliminary = false, Action<BattleCombatant>? collectReflection = null)
    {
        BattleModelRoster roster = ReadBattlePresentationRoster();
        _ = RequirePresentationModelOwner(roster, actor);
        foreach (BattleCombatant recipient in plan.SelectMany(hit => hit.Targets).Distinct())
        {
            _ = RequirePresentationModelOwner(roster, recipient);
        }
        return ExecuteNativeContactPlan(actor, command, action, native, plan, preliminary, collectReflection: collectReflection);
    }

    private static ImmutableArray<ExpandedBattleContact> ToHostedContacts(NativeContactPlanResult produced)
    {
        return produced.Contacts.Select(packet => new ExpandedBattleContact(RequireCanonicalGraphicsActorId(packet.Primary.Recipient), packet)).ToImmutableArray();
    }

    private void PublishExpandedImpacts(BattleActionPresentation command, ImmutableArray<ExpandedBattleContact> contacts)
    {
        foreach (ExpandedBattleContact contact in contacts)
        {
            NativeBattleContactHalf? secondary = ReadExpandedSecondaryContact(command, contact.Packet);
            PublishExpandedImpact(command, contact.RecipientId, contact.Contact, "EffectContact");
            if (secondary is not null)
            {
                PublishExpandedImpact(command, command.ActorId, secondary, "SecondaryEffectContact");
            }
        }
    }

    private BattleActionDefinition ExpandedActionDefinition(BattleCombatant actor, BattleActionPresentation command)
    {
        if (command.GuardianFormAction is { } formAction)
        {
            return GetGuardianFormActionDefinition(actor.Id, formAction);
        }
        if (command.Ability == BattleAbility.Attack)
        {
            BattleNativeAction attack = command.NativeAction ?? actor.NativeAttack!;
            return new(BattleAbility.Attack, 0, attack.Name ?? "Attack", BattleActionEffect.Native, attack.Power, false) { NativeAction = attack };
        }
        return command.Ability switch
        {
            BattleAbility.EnemyAbility when actor.IsPlayer && command.FreeNativeMagic && SupportsExpandedAngelWingSpell(command.NativeAction) => FindAction(BattleAbility.Magic, command.ActionId),
            BattleAbility.EnemyAbility when command.NativeAction is { } enemyAction => new(BattleAbility.EnemyAbility, command.ActionId, enemyAction.Name ?? $"Enemy ability {command.ActionId}", BattleActionEffect.Native, enemyAction.Power, false) { NativeAction = enemyAction },
            BattleAbility.NativeCommand when command.NativeAction is { } ordinary => ExpandedOrdinaryCommandDefinition(actor, ordinary),
            BattleAbility.LimitBreak when command.NativeAction is { } limit => ExpandedLimitDefinition(actor, limit),
            _ => FindAction(command.Ability, command.ActionId)
        };
    }

    private BattleCombatant[] ExpandedActionTargets(BattleCombatant actor, string targetId, BattleAbility ability, int actionId, BattleTargetRequest request, BattleNativeAction? native = null)
    {
        if (ability == BattleAbility.Draw)
        {
            BattleCombatant source = GetActor(targetId);
            if (!source.IsActive || !source.IsAlive || source.Untargetable)
            {
                return [];
            }
            if (request.Selection is BattleTargetSelection.None)
            {
                return [source];
            }
        }
        BattleCombatant[] targets = ResolveBattleTargets(request, sampleRandom: false);
        if (ability == BattleAbility.NativeCommand && native is not null && IsNativeLevelCommand(native))
        {
            return targets.Where(target => CanRebindExpandedEnemyLevel(target, ProjectNativeLevel(native, target.Level))).ToArray();
        }
        if (ability == BattleAbility.NativeCommand && native is not null && IsExpandedTargetedSpecial(native))
        {
            return targets.Where(target => CanTargetExpandedSpecial(native, target)).ToArray();
        }
        return targets;
    }

    private void PublishExpandedEffectContact(ExpandedInvocationProgress progress, int ordinal, string recipient, NativeBattleContactHalf contact)
    {
        if (!progress.Occurrences.Add(ordinal))
        {
            throw new InvalidDataException("The expanded effect repeated a published contact occurrence.");
        }
        CommitExpandedEnemyDraw(recipient, ref progress.EnemyDraw);
        ObserveDiagnosticDoomContact(progress.Command, recipient, contact);
        PublishExpandedImpact(progress.Command, recipient, contact, "EffectContact", deferredImpact: true);
    }

    private void PublishExpandedImpact(BattleActionPresentation command, string recipient, NativeBattleContactHalf contact, string eventKind, bool deferredImpact = false)
    {
        string actionName = command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log(eventKind, command.ActorId, recipient, contact.PresentedAmount, actionName, actionContext: command);
        if (contact.PresentedAmount > 0)
        {
            bool healing = (contact.HitFlags & 1) != 0;
            Log(healing ? "EffectHealing" : "EffectDamage", command.ActorId, recipient, contact.PresentedAmount, actionName, actionContext: command, deferredImpact: deferredImpact);
        }
    }
}
