using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleContact(string RecipientId, NativeBattleContactPacket Packet)
{
    public NativeBattleContactHalf Contact => Packet.Primary;
}

public sealed partial class BattleSession
{
    private NativeBattleEffectPresentation? expandedEffect;
    private BattleActionPresentation? expandedEffectCommand;
    private NativeBattleEffectContactBridge<string>? expandedContactBridge;
    private int expandedExpectedContacts;
    private bool deferringExpandedImpacts;
    private ImmutableArray<ExpandedBattleContact> expandedModelContacts = [];
    private BattleModelRoster? expandedModelRoster;
    private readonly HashSet<int> expandedPresentedOccurrences = [];
    public NativeBattleEffectPresentation? ExpandedEffect => expandedGuardianFormEntry?.Effect ?? expandedEffect;

    public bool IsExpandedEffectReplacingModel(string actorId)
    {
        if (ExpandedEffect is { } effect && (effect.IsGuardianFormCinematic || effect.IsGuardianSummon) && GetActor(actorId).IsPlayer)
        {
            return true;
        }
        if (expandedEffect is null || ActiveAction is null)
        {
            return false;
        }
        if (actorId == ActiveAction.ActorId)
        {
            return expandedEffect.ReplacesHostModel(expandedEffect.Source.Index);
        }
        return expandedModelRoster is { } roster && Enumerable.Range(0, roster.ActorCount).Any(row =>
            StringComparer.Ordinal.Equals(roster.Rows[row]?.ActorId, actorId) && expandedEffect.ReplacesHostModel(row));
    }

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
        if (IsDuelCallback(command.NativeLimitCallback))
        {
            StartExpandedDuelMove(command);
            return;
        }
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.AiRecipients) && !CanNativeSupportCommandAct(actor, command.NativePlayerReaction) ||
            !CanExecuteMagicReservation(actor, command) || command.NativeGraphic is null && command.ExpandedMagicTargets is null && !IsShotCallback(command.NativeLimitCallback) && ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.AiRecipients, command.NativeAction).Length == 0)
        {
            RefundCancelledNativeItem(command);
            actor.IsBusy = false;
            return;
        }
        if (command.Ability == BattleAbility.Item || IsExpandedIndependentSupport(command.NativeAction, command.NativePlayerReaction))
        {
            command = PrepareIndependentGuardianItem(actor, command);
        }
        ActiveAction = NativeCommandPresentation(command) with
        {
            ExpandedMagicTargets = command.ExpandedMagicTargets,
            RemainingFrames = NativeActionWindupFrames(actor, command.NativeSequence),
            NativeSequenceFrame = NativeControlTrack(actor, command.NativeSequence) is null ? null : 0
        };
        if (OwnsExpandedHostedInvocation)
        {
            BeginNativeBattleActionPause();
        }
        string name = command.NativeGraphic is not null ? "Native graphical effect" : command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log("Windup", actor.Id, command.TargetId, 0, $"{actor.Name}: {name}.");
    }

    private void AdvanceExpandedAction(BattleActionPresentation command)
    {
        if (expandedHostedEvent is not null)
        {
            return;
        }
        if (expandedEffect is { } effect)
        {
            BattleActionPresentation effectCommand = expandedEffectCommand!;
            AdvanceExpandedGuardianBoost();
            PublishExpandedEffectStep(effectCommand, effect.AdvanceCurrent());
            if (effect.Complete && nativeGuardianBoost is null)
            {
                if (effect.IsGuardianSummon)
                {
                    PublishExpandedEffectStep(effectCommand, effect.CompleteInvocation());
                }
                ValidateExpandedEffectContactCompletion();
                ReleaseExpandedEffect();
                if (expandedMagicPresentation is not null)
                {
                    AdvanceExpandedMagicPresentation(command);
                }
                else
                {
                    CompleteActionPresentation(command, "Expanded effect completed.");
                }
            }
            return;
        }
        BattleCombatant actor = GetActor(command.ActorId);
        if (command.GuardianFormAction is not null && GetGuardianForm(actor.Id) is null)
        {
            CompleteActionPresentation(command, "GF returned before the action was committed.");
            return;
        }
        if (command.NativeSequenceFrame is int frame && NativeControlTrack(actor, command.NativeSequence) is { } track && frame < track.CompletionFrame)
        {
            command = AdvanceNativeControlSequence(command);
            ActiveAction = command;
            if (command.NativeSequenceFrame < track.CompletionFrame)
            {
                return;
            }
        }
        else if (command.RemainingFrames > 1)
        {
            ActiveAction = command with { RemainingFrames = command.RemainingFrames - 1 };
            return;
        }
        if (expandedPresentationHost is null && expandedPresentationProcess is null)
        {
            CommitExpandedAction(command);
        }
        else
        {
            PrepareAndPublishExpandedAction(command);
        }
    }

    private void CommitExpandedAction(BattleActionPresentation command)
    {
        bool hosted = OwnsExpandedHostedInvocation;
        if (hosted)
        {
            RequireOrdinaryExpandedHostedCommand(command);
        }
        BattleCombatant actor = GetActor(command.ActorId);
        BattleCombatant target = GetActor(command.TargetId);
        if (IsDuelCallback(command.NativeLimitCallback))
        {
            CompleteExpandedDuelMove(command);
            return;
        }
        if (command.NativeLimitCallback is NativeLimitCallback.ShotOpening or NativeLimitCallback.ShotEnding)
        {
            Log("NativeLimitControl", actor.Id, target.Id, 0, command.NativeLimitCallback == NativeLimitCallback.ShotOpening ? "Shot started." : "Shot ended.");
            CompleteActionPresentation(command, "Shot control completed.");
            return;
        }
        if (command.ExpandedMagicTargets is { } magicTargets)
        {
            ResolveExpandedMagicGroup(actor, command, magicTargets);
            return;
        }
        BattleNativeAction? graphicAction = command.NativeGraphic is { } graphic ? NativeGraphicContactAction(actor, graphic.MagicId) : null;
        BattleCombatant[] recipients = ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.AiRecipients, graphicAction ?? command.NativeAction);
        bool silencedMagic = (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2) && (NativeSavedStatusBits(actor) & 0x10) != 0;
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.AiRecipients) && !CanNativeSupportCommandAct(actor, command.NativePlayerReaction) ||
            !CanExecuteMagicReservation(actor, new(command.ActorId, command.TargetId, command.Ability, command.ActionId) { MagicReservationId = command.MagicReservationId }, admitSilencedInvocation: hosted) || graphicAction is null && recipients.Length == 0 || !hosted && silencedMagic)
        {
            if (silencedMagic && (command.FreeNativeMagic && HasNativeAngelWing(actor) || command.AiRecipients?.TargetPolicy == BattleAiTargetPolicy.Compelled) && CanNativeBattleActorAct(actor))
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(100), BattleTextIdentity.Global(100));
            }
            RefundCancelledNativeItem(command);
            CompleteActionPresentation(command, "Expanded action cancelled.");
            return;
        }
        if (graphicAction is not null && recipients.Length == 0)
        {
            CompleteActionPresentation(command, "Native graphical effect completed.");
            return;
        }
        if (command.Ability == BattleAbility.Attack && actor.NativeAttack is null)
        {
            if (hosted)
            {
                throw new NotSupportedException("Hosted ordinary Attack requires its installed native attack record.");
            }
            CommitExpandedEnemyDraw(target.Id, ref pendingEnemyDraw);
            ResolveAction(actor, target, command);
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }
        BattleActionDefinition? drawSpell = null;
        BattleActionDefinition? stockDraw = null;
        if (command.Ability == BattleAbility.Draw)
        {
            if (command.DrawCast)
            {
                drawSpell = FindAction(BattleAbility.Magic, command.ActionId);
                recipients = ExpandedActionTargets(actor, command.DrawCastTargetId!, BattleAbility.Magic, command.ActionId, null);
                if (recipients.Length == 0)
                {
                    CompleteActionPresentation(command, "Expanded Draw Cast has no supported recipient.");
                    return;
                }
            }
            BattleAttackCommand draw = PrepareNativeDrawCommand(actor, new(actor.Id, target.Id, BattleAbility.Draw, command.ActionId, command.DrawCast) { DrawCastTargetId = command.DrawCastTargetId });
            command = command with { NativeDraw = draw.NativeDraw, NativeAction = draw.NativeAction };
            if (draw.NativeDraw!.Failed || drawSpell is null)
            {
                BattleNativeAction stock = NativeStockDrawAction(draw.NativeDraw);
                stockDraw = new(BattleAbility.Draw, command.ActionId, "Draw", BattleActionEffect.Native, 0, false) { NativeAction = stock };
                drawSpell = null;
                recipients = [target];
            }
        }

        if (command.IndependentGuardian?.FailureMessage is int failureMessage)
        {
            PublishIndependentGuardianFailure(actor, target, failureMessage);
            CompleteNativeActionInventory(actor, command, false);
            CompleteNativeActionStatuses(actor);
            CompleteActionPresentation(command, "Guardian item completed.");
            return;
        }
        BattleActionDefinition action = graphicAction is null
            ? stockDraw ?? drawSpell ?? ExpandedActionDefinition(actor, command)
            : new(BattleAbility.EnemyAbility, graphicAction.ActionId, graphicAction.Name!, BattleActionEffect.Native, 0, false) { NativeAction = graphicAction };
        if (command.IndependentGuardian is { Action: { } child } independent)
        {
            action = action with { Name = child.Name ?? action.Name, Level = independent.Level, NativeAction = child };
        }
        BattleNativeAction originalNative = action.NativeAction!;
        BattleNativeAction native = originalNative;
        bool hostedGunblade = hosted && NativeDeferredContact(actor, command, native) == NativeBattleDeferredContactKind.OrdinaryGunblade;
        if (!hostedGunblade && command.GuardianFormAction is null && IsGunbladeAttack(actor, command.Ability))
        {
            // 485160 recomputes the ordinary trigger contact with the installed weapon and type 10.
            native = native with { Command = command.NativeQueuedCommand == 12 ? (byte)248 : (byte)253, AttackType = 10, HitCount = 1, Critical = 0, DamagePresentation = 4 };
            command = command with { GunbladeTriggerTiming = ReadGunbladeTiming(actor, command) };
        }
        bool miniMog = IsExpandedMiniMogAction(native);
        int? miniMogFailure = miniMog ? PrepareNativeMiniMog() : null;
        if (hosted && miniMog && miniMogFailure is null)
        {
            // TODO(TD-009): Successful MiniMog still requires its native effect ownership.
            throw new NotSupportedException("Hosted presentation has no admission for successful MiniMog.");
        }
        if (!hosted && miniMogFailure is int miniMogMessage)
        {
            QueueNativeSpecialText(actor, target, NativeBattleGlobalText(miniMogMessage), BattleTextIdentity.Global(miniMogMessage));
            CompleteNativeActionStatuses(actor);
            CompleteActionPresentation(command, "MiniMog failed.");
            return;
        }
        if (command.Ability == BattleAbility.NativeCommand && IsExpandedTargetedSpecial(native))
        {
            // The expanded host prepares and applies the contact at the same callback, after windup revalidation.
            BattleNativeSpecialContext special = PrepareNativeSpecialTarget(recipients.Single(), native.AttackType);
            if (!hosted && special.Failed)
            {
                int message = native.AttackType == 17 ? 50 : 112;
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(message), BattleTextIdentity.Global(message));
                CompleteNativeActionStatuses(actor);
                CompleteActionPresentation(command, $"{action.Name} failed.");
                return;
            }
            command = command with { NativeSpecial = special };
        }
        if (command.Ability == BattleAbility.GuardianForce && (native.TargetFlags.GetValueOrDefault() & 2) != 0)
        {
            BattleCombatant recipient = recipients[NextRandomByte() % recipients.Length];
            recipients = [recipient];
            command = command with { TargetId = recipient.Id };
        }
        if (command is { Ability: BattleAbility.GuardianForce, GuardianFormInvocation: null } && expandedPresentationHost is not null && action.NativeGuardianBoostAvailable.HasValue && nativeBattleUiRandom is not null && CanPresentExpandedGuardianSummon(native))
        {
            BeginExpandedGuardianSummon(actor, command with { NativeAction = native }, recipients);
            return;
        }
        bool hostedSilence = hosted && NativeMagicInvocationSilenced(actor, command, originalNative);
        bool failedSpecial = hosted && (miniMogFailure.HasValue || NativeSpecialInvocationFailed(command, originalNative));
        NativeBattleActionHeaderPreparation? hostedHeader = hosted ? BuildExpandedActionHeaderPreparation(command, originalNative, miniMogFailure.HasValue) : null;
        BattleActionPresentation resolved = command with { NativeAction = miniMog && !failedSpecial ? native with { ActionId = (ushort)native.KernelIndex } : native };
        BattleActionPresentation formulaCommand = resolved;
        BattleActionDefinition formulaAction = action;
        if (failedSpecial || hostedSilence)
        {
            native = NativeFailedInvocationControl(originalNative);
            formulaCommand = resolved with { NativeQueuedCommand = 0, NativeAction = native };
            formulaAction = action with { NativeAction = native };
        }
        bool randomTarget = command.AiRecipients?.NativeSelectionMask is ushort nativeMask
            ? ExpandedNativeTargetRequest(nativeMask, native).SelectsRandomTarget
            : command.AiRecipients?.RandomSide is not null ||
                command.IndependentGuardian is null && (drawSpell is not null || command.Ability is BattleAbility.Magic or BattleAbility.Item or BattleAbility.NativeCommand or BattleAbility.EnemyAbility) && (native.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleCombatant[][] hitPlan = PlanExpandedHits(recipients, native.HitCount, randomTarget);
        if (hostedGunblade && (hitPlan.Length != 1 || hitPlan[0].Length != 1))
        {
            throw new InvalidDataException("Ordinary hosted Gunblade requires its single placeholder recipient.");
        }
        bool hasReflector = hitPlan.Any(targets => targets.Any(recipient => NativeActionReflects(recipient, native)));
        bool spellPresentation = command.Ability == BattleAbility.Magic || drawSpell is not null || command.FreeNativeMagic && native.Command == 2;
        BattleNativeAction? reflected = null;
        if (hasReflector)
        {
            // 487DF0 reads a magic record for the first reflected contact.
            reflected = definition.NativeActions.SingleOrDefault(action => action.Command == 247 && action.ActionId == unchecked((byte)native.ActionId) && action.CrisisLevel is null);
            if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected) && !(command is { NativeQueuedCommand: 2, AiRecipients: not null } && ExpandedBattleActionRules.SupportsEnemyMagicCure(reflected)))
            {
                RefundCancelledNativeItem(command);
                CompleteActionPresentation(command, "The expanded host has no supported reflected spell record.");
                return;
            }
        }

        int firstHitRecipientCount = hitPlan[0].Length;
        bool presentsNativeEffect = hosted || expandedPresentationHost is not null && (command.GuardianFormInvocation?.Mode == GuardianFormInvocationMode.FinisherWithoutEntry ||
            stockDraw is not null && command.NativeDraw is { Failed: false } ||
            spellPresentation && ExpandedBattleActionRules.HasNativeSpellPresentation(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.LimitBreak && CanPresentExpandedLimit(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.Item && native is { Command: 4, MagicId: 50, HitCount: 1 } && firstHitRecipientCount == 1 ||
            CanPresentExpandedEnemyEffect(actor, command, native, firstHitRecipientCount));

        bool independentSupport = IsExpandedIndependentSupport(native, command.NativePlayerReaction);
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = actor.IsPlayer && !independentSupport ? (byte)2 : (byte)0, Command = stockDraw is not null || native.Command is 0 or 248 or 253 ? native.Command : command.NativeQueuedCommand ?? native.Command, Action = native.ActionId };
        if (command.Ability == BattleAbility.GuardianForce)
        {
            // TODO(TD-009): A logical-only host has no summon presentation callback to own Boost.
            nativeActionScratch = nativeActionScratch with { GuardianFormulaE1E = true, GuardianBoostPercent = 100 };
        }
        ImmutableArray<ExpandedBattleContact> contacts;
        ImmutableArray<ExpandedBattleContact> reflectedContacts = [];
        BattleActionPresentation? reflectedCommand = null;
        bool presentsReflection = false;
        NativeBattleActionHeaderPreparation? reflectedHeader = null;
        (BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts)? phoenixRevival = null;
        bool phoenixPinion = command is { Ability: BattleAbility.Item, ActionId: 31, IndependentGuardian.Action: not null };
        resolvingNativeAction = formulaCommand;
        expandedRecoverSourceHp = native.AttackType == 25 ? actor.CurrentHp : null;
        BeginNativeReactionBatch();
        nativeActionCommitDepth++;
        try
        {
            if (command.NativeLimitCallback == NativeLimitCallback.ShotEffect || command.NativeSlots is not null)
            {
                PrepareNativeLimitCommand(new(command.ActorId, command.TargetId, command.Ability, command.ActionId)
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
            BattleCombatant? reflector;
            deferringExpandedImpacts = presentsNativeEffect && !IsExpandedOriginalGraphicPresentation(actor, command, native);
            (contacts, reflector) = ResolveExpandedHitPlan(actor, formulaCommand, formulaAction, formulaCommand.NativeAction!, hitPlan, preliminary: hostedGunblade);
            deferringExpandedImpacts = false;
            if (command.NativeDraw is { Failed: true } || failedSpecial || hostedSilence)
            {
                contacts = [];
            }
            if (failedSpecial)
            {
                int message = miniMogFailure ?? (originalNative.AttackType == 17 ? 50 : 112);
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(message), BattleTextIdentity.Global(message));
            }
            else if (miniMog && !contacts.IsEmpty)
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(54), BattleTextIdentity.Global(54));
            }
            (reflectedCommand, reflectedContacts, presentsReflection, reflectedHeader) = ResolveExpandedReflection(actor, resolved, action, reflector, reflected);
            resolvingNativeAction = resolved;
            CompleteNativeOrdinaryCommand(actor, resolved);
            bool continueAffinity = CompleteNativeActionInventory(actor, resolved, hostedSilence);
            if (continueAffinity && HasNativeAffinityAction(actor, resolved, native))
            {
                nativeAffinityAction = native;
                CompleteGuardianForceAffinity(actor, resolved);
            }
            if (command.NativeDraw is { Failed: false, Selection: >= 64 })
            {
                RefreshNativeEnemyDrawSlots();
            }
            if (phoenixPinion)
            {
                phoenixRevival = ResolveExpandedPhoenixRevival(actor, resolved);
            }
            if (!hosted && !independentSupport)
            {
                CompleteNativeActionStatuses(actor);
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
        if (hosted)
        {
            if (!contacts.IsEmpty || !reflectedContacts.IsEmpty)
            {
                hostedDraw = TakePendingHostedEnemyDraw();
            }
        }
        else if (presentsNativeEffect && !contacts.IsEmpty && !IsExpandedOriginalGraphicPresentation(actor, command, native))
        {
            // A reaction to this action can queue another Draw before its graphics emit contact.
            expandedEffectEnemyDraw = pendingEnemyDraw;
            pendingEnemyDraw = null;
        }
        else
        {
            ExpandedBattleContact? first = contacts.FirstOrDefault() ?? reflectedContacts.FirstOrDefault() ?? phoenixRevival?.Contacts.FirstOrDefault();
            if (first is not null)
            {
                CommitExpandedEnemyDraw(first.RecipientId, ref pendingEnemyDraw);
            }
        }
        if (!hosted)
        {
            FlushExpandedEnemyReactions();
        }
        if (hostedHeader is not null)
        {
            StageExpandedHostedInvocationCore(resolved, hostedHeader, contacts, contacts.IsEmpty ? null : hostedDraw, pendingGunblade: hostedGunblade);
            if (reflectedCommand is not null)
            {
                StageExpandedHostedInvocation(reflectedCommand, reflectedHeader!, reflectedContacts, contacts.IsEmpty ? hostedDraw : null);
            }
            ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
            return;
        }
        if (command.Ability == BattleAbility.Attack)
        {
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }
        if (!presentsNativeEffect && !presentsReflection)
        {
            // TODO(TD-009): Actions without a native presentation bridge use the host impact UI.
            PublishExpandedImpacts(resolved, contacts);
            if (reflectedCommand is not null)
            {
                PublishExpandedImpacts(reflectedCommand, reflectedContacts);
            }
            if (phoenixRevival is { } revival)
            {
                PublishExpandedImpacts(revival.Command, revival.Contacts, "PhoenixRevivalContact");
            }
            CompleteActionPresentation(command, "Action completed.");
            return;
        }
        BattleNativeAction presentationAction = drawSpell is null ? native : native with { Command = 6 };
        byte presentationSequence = drawSpell is null ? command.NativeSequence ?? 1 : NativeBattleActionGraphics.ResolvePlayerSequence(9, 0, native.ActionId, definition.NativeCommandSequences);
        if (reflectedCommand is not null)
        {
            expandedMagicPresentation = new([new(resolved with { NativeAction = presentationAction, NativeSequence = presentationSequence }, contacts, presentsNativeEffect && !contacts.IsEmpty),
                new(reflectedCommand, reflectedContacts, presentsReflection && !reflectedContacts.IsEmpty)]);
            expandedMagicCompletionMessage = "Expanded effect completed.";
            ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
            AdvanceExpandedMagicPresentation(ActiveAction, preparing: true);
            return;
        }
        BeginExpandedEffect(actor, resolved, presentationAction, presentationSequence, contacts);
        ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
    }

    private void PublishExpandedEffectStep(BattleActionPresentation command, NativeBattleEffectPresentationStep step)
    {
        foreach (NativeBattleForeignSequenceRequest request in step.Sequences)
        {
            string recipient = ReadExpandedEffectModelActor(request.ActorIndex);
            int sequence = request.Kind == NativeBattleForeignSequenceKind.Hit ? 4 : 1;
            Log("EffectModelSequence", command.ActorId, recipient, sequence, "Native effect model sequence.", actionContext: command);
        }
        foreach (NativeBattleEffectActivation activation in step.Activations)
        {
            BattleCombatant actor = GetActor(ReadExpandedEffectModelActor(activation.PhysicalIndex));
            if (!UsesOriginalEnemySlots || actor.Id != activation.ActorId || actor.NativeActorIndex != activation.PhysicalIndex ||
                !actor.IsActive || !actor.Suspended || !actor.Hidden || !actor.Untargetable)
            {
                throw new InvalidDataException("Native activation no longer matches its original host participant.");
            }
            ActivateNativeParticipant(actor);
        }
        foreach (NativeBattleModuleSound sound in step.Sounds)
        {
            PublishNativeModuleSound(sound, command.ActorId);
        }
        foreach (NativeBattleContactPacket packet in step.Contacts)
        {
            AcknowledgeExpandedEffectContact(command, packet);
        }
    }

    private void AcknowledgeExpandedEffectContact(BattleActionPresentation command, NativeBattleContactPacket packet)
    {
        bool hasSecondary = HasExpandedSecondaryContact(command, packet);
        (int ordinal, string recipient) = expandedContactBridge!.ResolveOccurrence(packet);
        PublishExpandedEffectContact(command, ordinal, recipient, packet.Primary);
        if (hasSecondary)
        {
            PublishExpandedImpact(command, command.ActorId, packet.Secondary, "SecondaryEffectContact", deferredImpact: true);
        }
    }

    private NativeBattleEffectPresentation CreateExpandedEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts)
    {
        return PrepareExpandedEffect(actor, action, sequence, contacts, command.GuardianFormInvocation?.Mode, ReadExpandedPresentationRoster());
    }

    private NativeBattleEffectPresentation PrepareExpandedEffect(BattleCombatant actor, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, GuardianFormInvocationMode? mode, BattleModelRoster roster)
    {
        ValidateExpandedEffectContacts(actor, contacts, roster);
        NativeBattleEffectPresentation prepared = expandedPresentationHost!.PrepareEffect(this, actor, action, sequence, contacts, mode);
        ValidatePreparedExpandedEffectBinding(prepared, actor, roster);
        return prepared;
    }

    private static void ValidatePreparedExpandedEffectBinding(NativeBattleEffectPresentation prepared, BattleCombatant actor, BattleModelRoster roster)
    {
        try
        {
            NativeBattleGraphicsState graphics = prepared.Graphics;
            if (graphics.PresentationRoster is not { } declared || !declared.Rows.SequenceEqual(roster.Rows) ||
                prepared.Source.Index != RequireExpandedPresentationRow(roster, actor) ||
                !graphics.ActorIds.SequenceEqual(roster.Rows.Select(row => row?.ActorId), StringComparer.Ordinal))
            {
                throw new InvalidDataException("The prepared expanded effect does not match its current roster and caster.");
            }
        }
        catch
        {
            prepared.Dispose();
            throw;
        }
    }

    private void BeginExpandedEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, NativeBattleEffectPresentation? prepared = null)
    {
        BattleModelRoster roster = ReadExpandedPresentationRoster();
        if (prepared is null)
        {
            prepared = PrepareExpandedEffect(actor, action, sequence, contacts, command.GuardianFormInvocation?.Mode, roster);
        }
        else
        {
            ValidatePreparedExpandedEffectBinding(prepared, actor, roster);
            ValidateExpandedEffectContacts(actor, contacts, roster);
        }
        SetExpandedEffectContacts(contacts, roster);
        expandedEffect = prepared;
        expandedEffectCommand = command;
        bool graphicsOnly = IsExpandedOriginalGraphicPresentation(actor, command, action);
        expandedExpectedContacts = graphicsOnly ? 0 : contacts.Length;
        if (graphicsOnly)
        {
            PublishExpandedImpacts(command, contacts);
        }
    }

    private void SetExpandedEffectContacts(BattleCombatant actor, ImmutableArray<ExpandedBattleContact> contacts)
    {
        BattleModelRoster roster = ReadExpandedPresentationRoster();
        ValidateExpandedEffectContacts(actor, contacts, roster);
        SetExpandedEffectContacts(contacts, roster);
    }

    private void SetExpandedEffectContacts(ImmutableArray<ExpandedBattleContact> contacts, BattleModelRoster roster)
    {
        expandedContactBridge = new(contacts.Select(contact => (contact.RecipientId, contact.Packet)), roster);
        expandedModelContacts = contacts;
        expandedModelRoster = roster;
    }

    private static void ValidateExpandedEffectContacts(BattleCombatant actor, ImmutableArray<ExpandedBattleContact> contacts, BattleModelRoster roster)
    {
        byte source = ExpandedPresentationRow(actor);
        if (!StringComparer.Ordinal.Equals(roster[source].ActorId, actor.Id))
        {
            throw new InvalidDataException("The expanded effect source does not own its presentation row.");
        }
        foreach (ExpandedBattleContact contact in contacts)
        {
            if (!StringComparer.Ordinal.Equals(roster[contact.Contact.Recipient].ActorId, contact.RecipientId) ||
                contact.Packet.Secondary.Recipient != byte.MaxValue && contact.Packet.Secondary.Recipient != source)
            {
                throw new InvalidDataException("The expanded contact differs from its bound recipient or source presentation row.");
            }
        }
    }

    private string ReadExpandedEffectModelActor(int index)
    {
        BattleModelRoster roster = expandedModelRoster ?? throw new InvalidDataException("The expanded effect has no bound model roster.");
        return roster[index].ActorId;
    }

    private bool HasExpandedSecondaryContact(BattleActionPresentation command, NativeBattleContactPacket packet)
    {
        if (packet.Secondary.Recipient == byte.MaxValue)
        {
            return false;
        }
        if (packet.Secondary.Recipient != ExpandedPresentationRow(GetActor(command.ActorId)))
        {
            throw new InvalidDataException("The expanded contact secondary row must name its actual caster.");
        }
        return true;
    }

    private void ValidateExpandedEffectContactCompletion()
    {
        if (expandedPresentedOccurrences.Count != expandedExpectedContacts)
        {
            throw new InvalidDataException("The expanded effect completed before publishing all prepared contacts.");
        }
    }

    private void ReleaseExpandedEffect()
    {
        expandedEffect?.Dispose();
        expandedEffect = null;
        expandedEffectCommand = null;
        expandedContactBridge = null;
        expandedModelContacts = [];
        expandedModelRoster = null;
        expandedExpectedContacts = 0;
        expandedPresentedOccurrences.Clear();
        expandedGuardianFormSkipRequested = false;
    }

    private (BattleActionPresentation? Command, ImmutableArray<ExpandedBattleContact> Contacts, bool PresentsNativeEffect, NativeBattleActionHeaderPreparation? HostedHeader) ResolveExpandedReflection(BattleCombatant actor, BattleActionPresentation source, BattleActionDefinition action, BattleCombatant? reflector, BattleNativeAction? reflected)
    {
        if (reflector is null || reflected is null)
        {
            return (null, [], false, null);
        }
        BattleCombatant[] opposite = ExpandedRandomTargets(reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies);
        if (opposite.Length == 0)
        {
            return (null, [], false, null);
        }
        BattleCombatant redirected = opposite[NextRandomByte() % opposite.Length];
        bool randomReflection = (reflected.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleCombatant[][] reflectionPlan = PlanExpandedHits(randomReflection ? opposite : [redirected], reflected.HitCount, randomReflection);
        BattleActionPresentation command = source with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, TargetId = redirected.Id, DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeSequence = 0, NativeQueuedCommand = 247, NativeAction = reflected, FreeNativeMagic = true, MagicReservationId = null };
        BattleActionDefinition reflectionAction = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? action.Name, BattleActionEffect.Native, reflected.Power, actor.IsPlayer == redirected.IsPlayer) { NativeAction = reflected };
        NativeBattleActionScratch sourceScratch = nativeActionScratch;
        resolvingNativeAction = command;
        nativeActionScratch = nativeActionScratch with { Command = 247, Action = reflected.ActionId };
        NativeBattleActionHeaderPreparation? hostedHeader = OwnsExpandedHostedInvocation ? BuildExpandedActionHeaderPreparation(command, reflected) : null;
        bool presentsNativeEffect = hostedHeader is not null || expandedPresentationHost is not null && ExpandedBattleActionRules.HasNativeSpellPresentation(reflected, reflectionPlan[0].Length);
        try
        {
            deferringExpandedImpacts = presentsNativeEffect;
            (ImmutableArray<ExpandedBattleContact> contacts, _) = ResolveExpandedHitPlan(actor, command, reflectionAction, reflected, reflectionPlan);
            return (command, contacts, presentsNativeEffect, hostedHeader);
        }
        finally
        {
            deferringExpandedImpacts = false;
            nativeActionScratch = nativeActionScratch with { Command = sourceScratch.Command, Action = sourceScratch.Action };
            resolvingNativeAction = source;
        }
    }

    private BattleCombatant[][] PlanExpandedHits(BattleCombatant[] recipients, byte hitCount, bool randomTarget)
    {
        BattleCombatant[][] hitPlan = new BattleCombatant[hitCount][];
        for (int hit = 0; hit < hitPlan.Length; hit++)
        {
            // 48E830 resolves every random target before the first formula consumes RNG.
            hitPlan[hit] = randomTarget ? [recipients[NextRandomByte() % recipients.Length]] : recipients;
        }
        return hitPlan;
    }

    private (ImmutableArray<ExpandedBattleContact> Contacts, BattleCombatant? Reflector) ResolveExpandedHitPlan(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, BattleCombatant[][] hitPlan, bool preliminary = false)
    {
        IGrouping<BattleCombatant, BattleCombatant>[] recipientHits = hitPlan.SelectMany(targets => targets).GroupBy(recipient => recipient).ToArray();
        BattleModelRoster roster = ReadExpandedPresentationRoster();
        _ = RequireExpandedPresentationRow(roster, actor);
        Dictionary<BattleCombatant, byte> recipientRows = recipientHits.ToDictionary(entry => entry.Key, entry => RequireExpandedPresentationRow(roster, entry.Key));
        foreach (IGrouping<BattleCombatant, BattleCombatant> entry in recipientHits)
        {
            entry.Key.NativeRemainingHits = checked((byte)entry.Count());
        }

        ImmutableArray<ExpandedBattleContact>.Builder contacts = ImmutableArray.CreateBuilder<ExpandedBattleContact>(hitPlan.Sum(targets => targets.Length));
        BattleCombatant? firstReflector = null;
        try
        {
            for (int hit = 0; hit < hitPlan.Length; hit++)
            {
                foreach (BattleCombatant recipient in hitPlan[hit])
                {
                    // Native48E830 stops the remaining pass when a previously selected target is KO.
                    bool deathSelfContact = recipient == actor && recipient.Id == command.AiRecipients?.DeathSelfTargetId && command.NativeAiCallbackStage == 3;
                    bool targetsKnockedOutAlly = recipient.IsPlayer && (IsNativeRevive(action) || EnemyActionTargetsKnockedOutAllies(native) ||
                        command.AiRecipients?.NativeSelectionMask is ushort mask && ExpandedNativeTargetRequest(mask, native).IncludesKnockedOutAllies);
                    if (!recipient.IsActive || (NativeSavedStatusBits(recipient) & 1) != 0 && !deathSelfContact && !targetsKnockedOutAlly)
                    {
                        return (contacts.ToImmutable(), firstReflector);
                    }
                    if (hit == 0 && command.NativeQueuedCommand == 12)
                    {
                        ResolveNativeMug(actor, recipient);
                    }
                    byte row = recipientRows[recipient];
                    NativeBattleContactPacket packet;
                    if (NativeActionReflects(recipient, native))
                    {
                        DecrementNativeRemainingHits(recipient);
                        firstReflector ??= recipient;
                        recipient.NativeCombatantFlags |= 0x4000;
                        recipient.LastNativeHealing = 1;
                        RecordNativeActionMemory(actor, recipient, command);
                        DispatchNativeActionCounter(recipient);
                        packet = CreateNativeReflectedContact(recipient, native, row);
                        Log("Reflect", actor.Id, recipient.Id, 0, $"{recipient.Name} reflects {action.Name}.");
                    }
                    else
                    {
                        bool previousSuppression = suppressNativeImmediateCounter;
                        suppressNativeImmediateCounter |= preliminary;
                        try
                        {
                            packet = ResolveInstalledNativeHitWithContact(actor, recipient, command, action, native, 255, preliminary, presentationRow: row);
                        }
                        finally
                        {
                            suppressNativeImmediateCounter = previousSuppression;
                        }
                    }
                    contacts.Add(new(recipient.Id, packet));
                }
            }
            return (contacts.ToImmutable(), firstReflector);
        }
        finally
        {
            foreach (IGrouping<BattleCombatant, BattleCombatant> entry in recipientHits)
            {
                entry.Key.NativeRemainingHits = 0;
            }
        }
    }

    private void PublishExpandedImpacts(BattleActionPresentation command, ImmutableArray<ExpandedBattleContact> contacts, string contactKind = "EffectContact")
    {
        foreach (ExpandedBattleContact contact in contacts)
        {
            bool hasSecondary = HasExpandedSecondaryContact(command, contact.Packet);
            PublishExpandedImpact(command, contact.RecipientId, contact.Contact, contactKind);
            if (hasSecondary)
            {
                PublishExpandedImpact(command, command.ActorId, contact.Packet.Secondary, "SecondaryEffectContact");
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
            BattleAbility.EnemyAbility when command.AiRecipients is not null => new(BattleAbility.EnemyAbility, command.ActionId, command.NativeAction!.Name ?? $"Enemy ability {command.ActionId}", BattleActionEffect.Native, command.NativeAction.Power, false) { NativeAction = command.NativeAction },
            BattleAbility.EnemyAbility when IsExpandedIndependentSupport(command.NativeAction, command.NativePlayerReaction) => new(BattleAbility.EnemyAbility, command.ActionId, command.NativeAction!.Name ?? "Automatic Support", BattleActionEffect.Native, command.NativeAction.Power, false) { NativeAction = command.NativeAction },
            BattleAbility.NativeCommand when command.NativeAction is { } ordinary => ExpandedOrdinaryCommandDefinition(actor, ordinary),
            BattleAbility.LimitBreak when command.NativeAction is { } limit => ExpandedLimitDefinition(actor, limit),
            _ => FindAction(command.Ability, command.ActionId)
        };
    }

    private BattleCombatant[] ExpandedActionTargets(BattleCombatant actor, string targetId, BattleAbility ability, int actionId, BattleAiRecipients? aiRecipients, BattleNativeAction? native = null)
    {
        if (aiRecipients?.Selection == BattleAiSelection.NativeMask)
        {
            return CurrentExpandedAiTargets(aiRecipients, native);
        }
        if (ability == BattleAbility.EnemyAbility && IsExpandedAutomaticGuardianAction(native))
        {
            return ExpandedAutomaticGuardianTargets();
        }
        if (aiRecipients is not null)
        {
            if (aiRecipients.TargetPolicy != BattleAiTargetPolicy.Compelled && aiRecipients.RandomSide is null && (native?.TargetFlags.GetValueOrDefault() & 2) == 2)
            {
                return Roster(GetActor(targetId).Side).Where(IsExpandedAiTarget).ToArray();
            }
            return CurrentExpandedAiTargets(aiRecipients, native);
        }
        BattleCombatant target = GetActor(targetId);
        if (ability == BattleAbility.EnemyAbility && actor.GuardianFormVitals is not null && native is { Command: 8 })
        {
            return target.IsActive && target.IsAlive && !target.Untargetable ? [target] : [];
        }
        if (ability == BattleAbility.EnemyAbility && actor.IsPlayer && SupportsExpandedAngelWingSpell(native))
        {
            byte spellFlags = native!.TargetFlags!.Value;
            if ((spellFlags & 0x30) == 0x10)
            {
                return IsExpandedAiTarget(target) && !target.IsPlayer ? [target] : [];
            }
            return enemies.Where(IsExpandedAiTarget).ToArray();
        }
        if (ability == BattleAbility.EnemyAbility && IsExpandedAngeloAction(native))
        {
            return IsExpandedAngeloTarget(target, native!) ? [target] : [];
        }
        if (ability == BattleAbility.NativeCommand && IsExpandedMiniMogAction(native))
        {
            // Menu A0 produces 80FF, which admits both sides after the living target filter.
            return combatants.Where(candidate => candidate.IsAlive && !candidate.Untargetable).ToArray();
        }
        if (ability == BattleAbility.LimitBreak && IsExpandedShotAction(native))
        {
            return native!.Command is 14 or 238 ? [target] : ExpandedShotTargets(target, native);
        }
        if (ability is BattleAbility.Attack or BattleAbility.Draw)
        {
            return target.IsActive && target.IsAlive && !target.Untargetable ? [target] : [];
        }
        BattleActionDefinition action = ability switch
        {
            BattleAbility.LimitBreak when native is not null => ExpandedLimitDefinition(actor, native),
            BattleAbility.NativeCommand when native is not null => ExpandedOrdinaryCommandDefinition(actor, native),
            _ => FindAction(ability, actionId)
        };
        if (ability == BattleAbility.NativeCommand && native is not null && IsNativeLevelCommand(native) &&
            !CanRebindExpandedEnemyLevel(target, ProjectNativeLevel(native, target.Level)))
        {
            return [];
        }
        if (ability == BattleAbility.NativeCommand && native is not null && IsExpandedTargetedSpecial(native) && !CanTargetExpandedSpecial(native, target))
        {
            return [];
        }
        if (ability == BattleAbility.GuardianForce)
        {
            return ExpandedGuardianForceTargetGroup(action);
        }
        if (action.NativeAction?.TargetFlags is byte flags)
        {
            if ((flags & 2) != 0)
            {
                // The original mixed-side selection word 0x80FF picks the enemy side when randomizing.
                BattleSide side = (flags & 0x30) == 0x20 ? BattleSide.Enemies : target.Side;
                return ExpandedRandomTargets(side);
            }
            if ((flags & 0x30) == 0x20)
            {
                return allies.Concat(enemies).Where(candidate => !candidate.Untargetable && (candidate.IsPlayer || candidate.IsAlive) && HasValidTargetLifeState(action, candidate)).ToArray();
            }
        }
        return action.AllTargets
            ? Roster(target.Side).Where(candidate => IsValidSelectedActionTarget(actor, action, candidate)).ToArray()
            : IsValidSelectedActionTarget(actor, action, target) ? [target] : [];
    }

    private BattleCombatant[] ExpandedRandomTargets(BattleSide side)
    {
        return Roster(side).Where(candidate => candidate.IsAlive && !candidate.Untargetable).ToArray();
    }

    private void PublishExpandedEffectContact(BattleActionPresentation command, int ordinal, string recipient, NativeBattleContactHalf contact)
    {
        if (!expandedPresentedOccurrences.Add(ordinal))
        {
            throw new InvalidDataException("The expanded effect repeated a published contact occurrence.");
        }
        CommitExpandedEnemyDraw(recipient, ref expandedEffectEnemyDraw);
        PublishExpandedImpact(command, recipient, contact, "EffectContact", deferredImpact: true);
    }

    private void PublishExpandedImpact(BattleActionPresentation command, string recipient, NativeBattleContactHalf contact, string eventKind, bool deferredImpact = false)
    {
        string actionName = command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log(eventKind, command.ActorId, recipient, contact.PresentedAmount, actionName, actionContext: command);
        if (contact.PresentedAmount > 0)
        {
            bool healing = (contact.HitFlags3 & 1) != 0;
            Log(healing ? "EffectHealing" : "EffectDamage", command.ActorId, recipient, contact.PresentedAmount, actionName, actionContext: command, deferredImpact: deferredImpact);
        }
    }
}
