using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleContact(string RecipientId, NativeBattleContactPacket Packet)
{
    public NativeBattleContactHalf Contact => Packet.Primary;
}

public sealed partial class BattleSession
{
    private Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation>? expandedEffectFactory;
    private NativeBattleEffectPresentation? expandedEffect;
    private BattleActionPresentation? expandedEffectCommand;
    private NativeBattleEffectContactBridge<string>? expandedContactBridge;
    private int expandedExpectedContacts;
    private bool deferringExpandedImpacts;
    private ImmutableArray<ExpandedBattleContact> expandedSharedContacts = [];
    private ImmutableArray<ExpandedBattleContact> expandedModelContacts = [];
    private Dictionary<int, string>? expandedModelRecipients;
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
        ExpandedBattleContact? target = expandedModelContacts.FirstOrDefault(contact => contact.RecipientId == actorId);
        return target is not null && expandedEffect.ReplacesHostModel(target.Contact.ActorIndex);
    }

    public void AttachExpandedEffectFactory(Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation> factory)
    {
        if (!UsesExpandedRoster || expandedEffectFactory is not null)
        {
            throw new InvalidOperationException("The expanded battle presentation owner is unavailable or already attached.");
        }
        expandedEffectFactory = factory;
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
        string name = command.NativeGraphic is not null ? "Native graphical effect" : command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log("Windup", actor.Id, command.TargetId, 0, $"{actor.Name}: {name}.");
    }

    private void AdvanceExpandedAction(BattleActionPresentation command)
    {
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
        if (expandedEffectFactory is null)
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
            !CanExecuteMagicReservation(actor, new(command.ActorId, command.TargetId, command.Ability, command.ActionId) { MagicReservationId = command.MagicReservationId }) || graphicAction is null && recipients.Length == 0 || silencedMagic)
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
        BattleNativeAction native = action.NativeAction!;
        if (command.GuardianFormAction is null && IsGunbladeAttack(actor, command.Ability))
        {
            // 485160 recomputes the ordinary trigger contact with the installed weapon and type 10.
            native = native with { Command = command.NativeQueuedCommand == 12 ? (byte)248 : (byte)253, AttackType = 10, HitCount = 1, Critical = 0, DamagePresentation = 4 };
            command = command with { GunbladeTriggerTiming = ReadGunbladeTiming(actor, command) };
        }
        bool miniMog = IsExpandedMiniMogAction(native);
        if (miniMog && PrepareNativeMiniMog() is int miniMogFailure)
        {
            QueueNativeSpecialText(actor, target, NativeBattleGlobalText(miniMogFailure), BattleTextIdentity.Global(miniMogFailure));
            CompleteNativeActionStatuses(actor);
            CompleteActionPresentation(command, "MiniMog failed.");
            return;
        }
        if (command.Ability == BattleAbility.NativeCommand && IsExpandedTargetedSpecial(native))
        {
            // The expanded host prepares and applies the contact at the same callback, after windup revalidation.
            BattleNativeSpecialContext special = PrepareNativeSpecialTarget(recipients.Single(), native.AttackType);
            if (special.Failed)
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
        if (command is { Ability: BattleAbility.GuardianForce, GuardianFormInvocation: null } && expandedEffectFactory is not null && action.NativeGuardianBoostAvailable.HasValue && nativeBattleUiRandom is not null && CanPresentExpandedGuardianSummon(native))
        {
            BeginExpandedGuardianSummon(actor, command with { NativeAction = native }, recipients);
            return;
        }
        bool randomTarget = command.AiRecipients?.NativeSelectionMask is ushort nativeMask
            ? ExpandedNativeTargetRequest(nativeMask, native).SelectsRandomTarget
            : command.AiRecipients?.RandomSide is not null ||
                command.IndependentGuardian is null && (drawSpell is not null || command.Ability is BattleAbility.Magic or BattleAbility.Item or BattleAbility.NativeCommand or BattleAbility.EnemyAbility) && (native.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleCombatant[][] hitPlan = PlanExpandedHits(recipients, native.HitCount, randomTarget);
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
        bool presentsNativeEffect = expandedEffectFactory is not null && (command.GuardianFormInvocation?.Mode == GuardianFormInvocationMode.FinisherWithoutEntry ||
            stockDraw is not null && command.NativeDraw is { Failed: false } ||
            spellPresentation && ExpandedBattleActionRules.HasNativeSpellPresentation(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.LimitBreak && CanPresentExpandedLimit(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.Item && native is { Command: 4, MagicId: 50, HitCount: 1 } && firstHitRecipientCount == 1 ||
            CanPresentExpandedEnemyEffect(actor, command, native, firstHitRecipientCount));

        bool independentSupport = IsExpandedIndependentSupport(native, command.NativePlayerReaction);
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = actor.IsPlayer && !independentSupport ? (byte)2 : (byte)0, Command = stockDraw is not null || native.Command is 248 or 253 ? native.Command : command.NativeQueuedCommand ?? native.Command, Action = native.ActionId };
        if (command.Ability == BattleAbility.GuardianForce)
        {
            // TODO(TD-009): A logical-only host has no summon presentation callback to own Boost.
            nativeActionScratch = nativeActionScratch with { GuardianFormulaE1E = true, GuardianBoostPercent = 100 };
        }
        BattleActionPresentation resolved = command with { NativeAction = miniMog ? native with { ActionId = (ushort)native.KernelIndex } : native };
        ImmutableArray<ExpandedBattleContact> contacts;
        ImmutableArray<ExpandedBattleContact> reflectedContacts = [];
        BattleActionPresentation? reflectedCommand = null;
        bool presentsReflection = false;
        (BattleActionPresentation Command, ImmutableArray<ExpandedBattleContact> Contacts)? phoenixRevival = null;
        bool phoenixPinion = command is { Ability: BattleAbility.Item, ActionId: 31, IndependentGuardian.Action: not null };
        resolvingNativeAction = resolved;
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
            BattleCombatant? reflector;
            deferringExpandedImpacts = presentsNativeEffect && !IsExpandedOriginalGraphicPresentation(actor, command, native);
            (contacts, reflector) = ResolveExpandedHitPlan(actor, resolved, action, resolved.NativeAction!, hitPlan, presentsNativeEffect);
            deferringExpandedImpacts = false;
            if (command.NativeDraw is { Failed: true })
            {
                contacts = [];
            }
            if (miniMog && !contacts.IsEmpty)
            {
                QueueNativeSpecialText(actor, target, NativeBattleGlobalText(54), BattleTextIdentity.Global(54));
            }
            (reflectedCommand, reflectedContacts, presentsReflection) = ResolveExpandedReflection(actor, resolved, action, reflector, reflected);
            CompleteNativeOrdinaryCommand(actor, resolved);
            bool continueAffinity = CompleteNativeActionInventory(actor, resolved, false);
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
            if (!independentSupport)
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
        if (presentsNativeEffect && !contacts.IsEmpty && !IsExpandedOriginalGraphicPresentation(actor, command, native))
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
        FlushExpandedEnemyReactions();
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
            if (!expandedModelRecipients!.TryGetValue(request.ActorIndex, out string? recipient))
            {
                throw new InvalidDataException("The expanded effect requested a sequence outside its bound contact model rows.");
            }
            int sequence = request.Kind == NativeBattleForeignSequenceKind.Hit ? 4 : 1;
            Log("EffectModelSequence", command.ActorId, recipient, sequence, "Native effect model sequence.", actionContext: command);
        }
        foreach (NativeBattleEffectActivation activation in step.Activations)
        {
            BattleCombatant actor = FindNativeActor(activation.PhysicalIndex);
            if (!UsesOriginalEnemySlots || actor.Id != activation.ActorId || !actor.Suspended || !actor.Hidden || !actor.Untargetable)
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
            (int ordinal, string recipient) = expandedContactBridge!.ResolveOccurrence(packet);
            PublishExpandedEffectContact(command, ordinal, recipient, packet.Primary);
            if (packet.Secondary.ActorIndex == 0)
            {
                PublishExpandedImpact(command, command.ActorId, packet.Secondary, "SecondaryEffectContact", deferredImpact: true);
            }
        }
        if (!expandedSharedContacts.IsEmpty && expandedPresentedOccurrences.Count == expandedContactBridge!.Count)
        {
            for (int index = 0; index < expandedSharedContacts.Length; index++)
            {
                ExpandedBattleContact shared = expandedSharedContacts[index];
                PublishExpandedEffectContact(command, expandedContactBridge.Count + index, shared.RecipientId, shared.Contact);
            }
            expandedSharedContacts = [];
        }
    }

    private NativeBattleEffectPresentation CreateExpandedEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts)
    {
        int nativeContactLimit = NativeBattleSlots.Count - 1;
        ImmutableArray<ExpandedBattleContact> presented = contacts.Length > nativeContactLimit ? contacts[..nativeContactLimit] : contacts;
        return expandedEffectFactory!(this, actor, action, sequence, presented, command.GuardianFormInvocation?.Mode);
    }

    private void BeginExpandedEffect(BattleCombatant actor, BattleActionPresentation command, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, NativeBattleEffectPresentation? prepared = null)
    {
        expandedEffect = prepared ?? CreateExpandedEffect(actor, command, action, sequence, contacts);
        expandedEffectCommand = command;
        bool graphicsOnly = IsExpandedOriginalGraphicPresentation(actor, command, action);
        expandedExpectedContacts = graphicsOnly ? 0 : contacts.Length;
        SetExpandedEffectContacts(contacts);
        if (graphicsOnly)
        {
            PublishExpandedImpacts(command, contacts);
        }
    }

    private void SetExpandedEffectContacts(ImmutableArray<ExpandedBattleContact> contacts)
    {
        int nativeContactLimit = NativeBattleSlots.Count - 1;
        ImmutableArray<ExpandedBattleContact> presented = contacts.Length > nativeContactLimit ? contacts[..nativeContactLimit] : contacts;
        // TODO(TD-009): Contacts beyond the presented slice still publish through the logical impact UI.
        expandedSharedContacts = contacts.Length > nativeContactLimit ? contacts[nativeContactLimit..] : [];
        expandedContactBridge = new(presented.Select(contact => (contact.RecipientId, contact.Packet)));
        expandedModelContacts = presented;
        expandedModelRecipients = presented.DistinctBy(contact => contact.Contact.ActorIndex).ToDictionary(contact => (int)contact.Contact.ActorIndex, contact => contact.RecipientId);
    }

    private void ValidateExpandedEffectContactCompletion()
    {
        if (!expandedSharedContacts.IsEmpty || expandedPresentedOccurrences.Count != expandedExpectedContacts)
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
        expandedModelRecipients = null;
        expandedSharedContacts = [];
        expandedExpectedContacts = 0;
        expandedPresentedOccurrences.Clear();
        expandedGuardianFormSkipRequested = false;
    }

    private (BattleActionPresentation? Command, ImmutableArray<ExpandedBattleContact> Contacts, bool PresentsNativeEffect) ResolveExpandedReflection(BattleCombatant actor, BattleActionPresentation source, BattleActionDefinition action, BattleCombatant? reflector, BattleNativeAction? reflected)
    {
        if (reflector is null || reflected is null)
        {
            return (null, [], false);
        }
        BattleCombatant[] opposite = ExpandedRandomTargets(reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies);
        if (opposite.Length == 0)
        {
            return (null, [], false);
        }
        BattleCombatant redirected = opposite[NextRandomByte() % opposite.Length];
        bool randomReflection = (reflected.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleCombatant[][] reflectionPlan = PlanExpandedHits(randomReflection ? opposite : [redirected], reflected.HitCount, randomReflection);
        BattleActionPresentation command = source with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, TargetId = redirected.Id, DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeSequence = 0, NativeQueuedCommand = 247, NativeAction = reflected, FreeNativeMagic = true, MagicReservationId = null };
        BattleActionDefinition reflectionAction = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? action.Name, BattleActionEffect.Native, reflected.Power, actor.IsPlayer == redirected.IsPlayer) { NativeAction = reflected };
        NativeBattleActionScratch sourceScratch = nativeActionScratch;
        resolvingNativeAction = command;
        nativeActionScratch = nativeActionScratch with { Command = 247, Action = reflected.ActionId };
        bool presentsNativeEffect = expandedEffectFactory is not null && ExpandedBattleActionRules.HasNativeSpellPresentation(reflected, reflectionPlan[0].Length);
        try
        {
            deferringExpandedImpacts = presentsNativeEffect;
            (ImmutableArray<ExpandedBattleContact> contacts, _) = ResolveExpandedHitPlan(actor, command, reflectionAction, reflected, reflectionPlan, presentsNativeEffect);
            return (command, contacts, presentsNativeEffect);
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

    private (ImmutableArray<ExpandedBattleContact> Contacts, BattleCombatant? Reflector) ResolveExpandedHitPlan(BattleCombatant actor, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native, BattleCombatant[][] hitPlan, bool presentsNativeEffect)
    {
        IGrouping<BattleCombatant, BattleCombatant>[] recipientHits = hitPlan.SelectMany(targets => targets).GroupBy(recipient => recipient).ToArray();
        foreach (IGrouping<BattleCombatant, BattleCombatant> entry in recipientHits)
        {
            entry.Key.NativeRemainingHits = checked((byte)entry.Count());
        }

        // The effect bank is action-local. It does not own any battle roster slot or action memory.
        ReadOnlySpan<byte> availableRows = hitPlan[0].Length == 1 ? [3, 1, 2, 4, 5, 6] : [1, 2, 3, 4, 5, 6];
        Dictionary<string, byte> rowsByRecipient = new(StringComparer.Ordinal);
        bool usePhysicalRows = presentsNativeEffect && (IsExpandedOriginalGraphicPresentation(actor, command, native) || IsExpandedAuthoredDeathEffectPresentation(actor, command, native));
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
                    bool hasPresentationRow = presentsNativeEffect && contacts.Count < NativeBattleSlots.Count - 1;
                    byte row = 0;
                    if (usePhysicalRows)
                    {
                        row = checked((byte)recipient.EncodeNativeSlot());
                    }
                    else if (hasPresentationRow && recipient != actor && !rowsByRecipient.TryGetValue(recipient.Id, out row))
                    {
                        row = availableRows[rowsByRecipient.Count];
                        rowsByRecipient.Add(recipient.Id, row);
                    }
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
                        packet = ResolveInstalledNativeHitWithContact(actor, recipient, command, action, native, 255, presentationRow: row);
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
            PublishExpandedImpact(command, contact.RecipientId, contact.Contact, contactKind);
            if (contact.Packet.Secondary.ActorIndex == 0)
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
