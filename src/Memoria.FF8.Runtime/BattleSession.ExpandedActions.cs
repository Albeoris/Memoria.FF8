using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleContact(string RecipientId, NativeBattleContactPacket Packet)
{
    public NativeBattleContactHalf Contact => Packet.Primary;
}

public sealed partial class BattleSession
{
    private Func<BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, NativeBattleEffectPresentation>? expandedEffectFactory;
    private NativeBattleEffectPresentation? expandedEffect;
    private NativeBattleEffectContactBridge<string>? expandedContactBridge;
    private int expandedExpectedContacts;
    private ImmutableArray<ExpandedBattleContact> expandedSharedContacts = [];
    private ImmutableArray<ExpandedBattleContact> expandedModelContacts = [];
    private readonly HashSet<string> expandedPresentedRecipients = new(StringComparer.Ordinal);
    public NativeBattleEffectPresentation? ExpandedEffect => expandedEffect;

    public bool IsExpandedEffectReplacingModel(string actorId)
    {
        if (expandedEffect is null || ActiveAction is null)
        {
            return false;
        }
        if (actorId == ActiveAction.ActorId)
        {
            return expandedEffect.ReplacesHostModel(0);
        }
        ExpandedBattleContact? target = expandedModelContacts.FirstOrDefault(contact => contact.RecipientId == actorId);
        return target is not null && expandedEffect.ReplacesHostModel(target.Contact.ActorIndex);
    }

    public void AttachExpandedEffectFactory(Func<BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, NativeBattleEffectPresentation> factory)
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
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.AiRecipients) || !CanExecuteMagicReservation(actor, command) || ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.AiRecipients, command.NativeAction).Length == 0)
        {
            RefundCancelledNativeItem(command);
            actor.IsBusy = false;
            return;
        }
        if (command.Ability == BattleAbility.Item)
        {
            command = PrepareIndependentGuardianItem(actor, command);
        }
        ActiveAction = NativeCommandPresentation(command) with
        {
            RemainingFrames = NativeActionWindupFrames(actor, command.NativeSequence),
            NativeSequenceFrame = NativeControlTrack(actor, command.NativeSequence) is null ? null : 0
        };
        string name = command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log("Windup", actor.Id, command.TargetId, 0, $"{actor.Name}: {name}.");
    }

    private void AdvanceExpandedAction(BattleActionPresentation command)
    {
        if (expandedEffect is { } effect)
        {
            NativeBattleEffectPresentationStep step = effect.AdvanceCurrent();
            foreach (NativeBattleModuleSound sound in step.Sounds)
            {
                PublishNativeModuleSound(sound, command.ActorId);
            }
            foreach (NativeBattleContactPacket packet in step.Contacts)
            {
                string recipient = expandedContactBridge!.Resolve(packet);
                PublishExpandedEffectContact(command, recipient, packet.Primary);
                if (packet.Secondary.ActorIndex == 0)
                {
                    PublishExpandedImpact(command, command.ActorId, packet.Secondary, "SecondaryEffectContact");
                }
            }
            if (!expandedSharedContacts.IsEmpty && expandedPresentedRecipients.Count == expandedContactBridge!.Count)
            {
                // Quake publishes its entire contact list on one emitter callback.
                foreach (ExpandedBattleContact shared in expandedSharedContacts)
                {
                    PublishExpandedEffectContact(command, shared.RecipientId, shared.Contact);
                }
                expandedSharedContacts = [];
            }
            if (effect.Complete)
            {
                if (expandedSharedContacts.Length != 0 || expandedPresentedRecipients.Count != expandedExpectedContacts)
                {
                    throw new InvalidDataException("The expanded effect completed before publishing all prepared contacts.");
                }
                expandedEffect = null;
                expandedContactBridge = null;
                expandedModelContacts = [];
                expandedExpectedContacts = 0;
                expandedPresentedRecipients.Clear();
                CompleteActionPresentation(command, "Expanded effect completed.");
            }
            return;
        }
        BattleCombatant actor = GetActor(command.ActorId);
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
        BattleCombatant target = GetActor(command.TargetId);
        BattleCombatant[] recipients = ExpandedActionTargets(actor, command.TargetId, command.Ability, command.ActionId, command.AiRecipients, command.NativeAction);
        if (!CanExecuteExpandedAction(actor, command.NativeAiCallbackStage, command.AiRecipients) || !CanExecuteMagicReservation(actor, new(command.ActorId, command.TargetId, command.Ability, command.ActionId) { MagicReservationId = command.MagicReservationId }) || recipients.Length == 0 ||
            (command.Ability == BattleAbility.Magic || command.NativeAction?.Command == 2) && (NativeSavedStatusBits(actor) & 0x10) != 0)
        {
            RefundCancelledNativeItem(command);
            CompleteActionPresentation(command, "Expanded action cancelled.");
            return;
        }
        if (command.Ability == BattleAbility.Attack && actor.NativeAttack is null)
        {
            ResolveAction(actor, target, command);
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }
        BattleActionDefinition? drawSpell = null;
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
                CompleteActionPresentation(command, "Draw completed.");
                return;
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
        BattleActionDefinition action = drawSpell ?? ExpandedActionDefinition(actor, command);
        if (command.IndependentGuardian is { Action: { } child } independent)
        {
            action = action with { Name = child.Name ?? action.Name, Level = independent.Level, NativeAction = child };
        }
        BattleNativeAction native = action.NativeAction!;
        if (command.Ability == BattleAbility.GuardianForce && (native.TargetFlags.GetValueOrDefault() & 2) != 0)
        {
            BattleCombatant recipient = recipients[NextRandomByte() % recipients.Length];
            recipients = [recipient];
            command = command with { TargetId = recipient.Id };
        }
        bool randomTarget = command.AiRecipients?.RandomSide is not null ||
            command.IndependentGuardian is null && (drawSpell is not null || command.Ability is BattleAbility.Magic or BattleAbility.Item or BattleAbility.NativeCommand or BattleAbility.EnemyAbility) && (native.TargetFlags.GetValueOrDefault() & 2) != 0;
        BattleCombatant[][] hitPlan = PlanExpandedHits(recipients, native.HitCount, randomTarget);
        bool hasReflector = hitPlan.Any(targets => targets.Any(recipient => NativeActionReflects(recipient, native)));
        bool spellPresentation = command.Ability == BattleAbility.Magic || drawSpell is not null;
        BattleNativeAction? reflected = null;
        if (hasReflector)
        {
            // 487DF0 reads a magic record for the first reflected contact.
            reflected = definition.NativeActions.SingleOrDefault(action => action.Command == 247 && action.ActionId == unchecked((byte)native.ActionId) && action.CrisisLevel is null);
            if (reflected is null || !ExpandedBattleActionRules.SupportsContacts(reflected))
            {
                RefundCancelledNativeItem(command);
                CompleteActionPresentation(command, "The expanded host has no supported reflected spell record.");
                return;
            }
        }

        int firstHitRecipientCount = hitPlan[0].Length;
        int nativeContactLimit = NativeBattleSlots.Count - 1;
        // TODO(TD-009): Reflected commands need a presentation bridge for the source and redirected contacts.
        bool presentsNativeEffect = !hasReflector && expandedEffectFactory is not null && (spellPresentation && ExpandedBattleActionRules.HasNativeSpellPresentation(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.LimitBreak && CanPresentExpandedLimit(native, firstHitRecipientCount) ||
            command.Ability == BattleAbility.Item && native is { Command: 4, MagicId: 50, HitCount: 1 } && firstHitRecipientCount == 1 ||
            CanPresentExpandedEnemyEffect(actor, command, native, firstHitRecipientCount));
        bool exceedsPresentationBank = presentsNativeEffect && firstHitRecipientCount > nativeContactLimit;

        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false, EventLane = actor.IsPlayer ? (byte)2 : (byte)0, Command = command.NativeQueuedCommand ?? native.Command, Action = native.ActionId };
        if (command.Ability == BattleAbility.GuardianForce)
        {
            // TODO(TD-009): Expanded GF impact uses the unboosted formula without the native summon presentation.
            nativeActionScratch = nativeActionScratch with { GuardianFormulaE1E = true, GuardianBoostPercent = 100 };
        }
        BattleActionPresentation resolved = command with { NativeAction = native };
        ImmutableArray<ExpandedBattleContact> contacts;
        ImmutableArray<ExpandedBattleContact> reflectedContacts = [];
        BattleActionPresentation? reflectedCommand = null;
        resolvingNativeAction = resolved;
        BeginNativeReactionBatch();
        nativeActionCommitDepth++;
        try
        {
            BattleCombatant? reflector;
            (contacts, reflector) = ResolveExpandedHitPlan(actor, resolved, action, native, hitPlan, presentsNativeEffect);
            if (reflector is not null && reflected is not null)
            {
                BattleCombatant[] opposite = ExpandedRandomTargets(reflector.IsPlayer ? BattleSide.Enemies : BattleSide.Allies);
                if (opposite.Length != 0)
                {
                    BattleCombatant redirected = opposite[NextRandomByte() % opposite.Length];
                    bool randomReflection = (reflected.TargetFlags.GetValueOrDefault() & 2) != 0;
                    BattleCombatant[][] reflectionPlan = PlanExpandedHits(randomReflection ? opposite : [redirected], reflected.HitCount, randomReflection);
                    reflectedCommand = resolved with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, TargetId = redirected.Id, DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeQueuedCommand = 247, NativeAction = reflected, FreeNativeMagic = true, MagicReservationId = null };
                    BattleActionDefinition reflectionAction = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? action.Name, BattleActionEffect.Native, reflected.Power, actor.IsPlayer == redirected.IsPlayer) { NativeAction = reflected };
                    NativeBattleActionScratch sourceScratch = nativeActionScratch;
                    resolvingNativeAction = reflectedCommand;
                    nativeActionScratch = nativeActionScratch with { Command = 247, Action = reflected.ActionId };
                    try
                    {
                        (reflectedContacts, _) = ResolveExpandedHitPlan(actor, reflectedCommand, reflectionAction, reflected, reflectionPlan, presentsNativeEffect: false);
                    }
                    finally
                    {
                        nativeActionScratch = nativeActionScratch with { Command = sourceScratch.Command, Action = sourceScratch.Action };
                        resolvingNativeAction = resolved;
                    }
                }
            }
            CompleteNativeActionInventory(actor, resolved, false);
            if (command.Ability == BattleAbility.GuardianForce)
            {
                nativeAffinityAction = native;
                CompleteGuardianForceAffinity(actor, resolved);
            }
            CompleteNativeActionStatuses(actor);
        }
        finally
        {
            nativeActionCommitDepth--;
            resolvingNativeAction = null;
        }
        FlushExpandedEnemyReactions();
        if (command.Ability == BattleAbility.Attack)
        {
            CompleteActionPresentation(command, "Attack completed.");
            return;
        }
        if (!presentsNativeEffect)
        {
            // TODO(TD-009): Actions without a native presentation bridge use the host impact UI.
            PublishExpandedImpacts(resolved, contacts);
            if (reflectedCommand is not null)
            {
                PublishExpandedImpacts(reflectedCommand, reflectedContacts);
            }
            CompleteActionPresentation(command, "Action completed.");
            return;
        }
        ImmutableArray<ExpandedBattleContact> presented = exceedsPresentationBank ? contacts[..nativeContactLimit] : contacts;
        expandedExpectedContacts = contacts.Length;
        expandedSharedContacts = exceedsPresentationBank ? contacts[nativeContactLimit..] : [];
        expandedContactBridge = new(presented.Select(contact => (contact.RecipientId, contact.Packet)));
        BattleNativeAction presentationAction = drawSpell is null ? native : native with { Command = 6 };
        byte presentationSequence = drawSpell is null ? command.NativeSequence ?? 1 : NativeBattleActionGraphics.ResolvePlayerSequence(9, 0, native.ActionId, definition.NativeCommandSequences);
        expandedEffect = expandedEffectFactory!(actor, presentationAction, presentationSequence, presented);
        expandedModelContacts = presented;
        ActiveAction = command with { Phase = BattleActionPhase.Recovery, RemainingFrames = 1, MagicReservationId = null };
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
        int nextRow = hitPlan[0].Length == 1 ? NativeBattleSlots.AllyCount : 1;
        ImmutableArray<ExpandedBattleContact>.Builder contacts = ImmutableArray.CreateBuilder<ExpandedBattleContact>(hitPlan.Sum(targets => targets.Length));
        BattleCombatant? firstReflector = null;
        try
        {
            foreach (BattleCombatant[] targets in hitPlan)
            {
                foreach (BattleCombatant recipient in targets)
                {
                    // Native48E830 stops the remaining pass when a previously selected target is KO.
                    bool deathSelfContact = recipient == actor && recipient.Id == command.AiRecipients?.DeathSelfTargetId && command.NativeAiCallbackStage == 3;
                    if (!recipient.IsActive || (NativeSavedStatusBits(recipient) & 1) != 0 && !deathSelfContact && (!recipient.IsPlayer || !IsNativeRevive(action)))
                    {
                        return (contacts.ToImmutable(), firstReflector);
                    }
                    bool hasPresentationRow = presentsNativeEffect && contacts.Count < NativeBattleSlots.Count - 1;
                    byte row = hasPresentationRow && recipient != actor ? checked((byte)nextRow++) : (byte)0;
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

    private void PublishExpandedImpacts(BattleActionPresentation command, ImmutableArray<ExpandedBattleContact> contacts)
    {
        foreach (ExpandedBattleContact contact in contacts)
        {
            PublishExpandedImpact(command, contact.RecipientId, contact.Contact, "EffectContact");
            if (contact.Packet.Secondary.ActorIndex == 0)
            {
                PublishExpandedImpact(command, command.ActorId, contact.Packet.Secondary, "SecondaryEffectContact");
            }
        }
    }

    private BattleActionDefinition ExpandedActionDefinition(BattleCombatant actor, BattleActionPresentation command)
    {
        return command.Ability switch
        {
            BattleAbility.Attack => new(BattleAbility.Attack, 0, actor.NativeAttack!.Name ?? "Attack", BattleActionEffect.Native, actor.NativeAttack.Power, false) { NativeAction = actor.NativeAttack },
            BattleAbility.EnemyAbility when command.AiRecipients is not null => new(BattleAbility.EnemyAbility, command.ActionId, command.NativeAction!.Name ?? $"Enemy ability {command.ActionId}", BattleActionEffect.Native, command.NativeAction.Power, false) { NativeAction = command.NativeAction },
            BattleAbility.NativeCommand when command.NativeAction is { } ordinary => ExpandedOrdinaryCommandDefinition(ordinary),
            BattleAbility.LimitBreak when command.NativeAction is { } limit => ExpandedLimitDefinition(actor, limit),
            _ => FindAction(command.Ability, command.ActionId)
        };
    }

    private BattleCombatant[] ExpandedActionTargets(BattleCombatant actor, string targetId, BattleAbility ability, int actionId, BattleAiRecipients? aiRecipients, BattleNativeAction? native = null)
    {
        if (aiRecipients is not null)
        {
            if (aiRecipients.RandomSide is null && (native?.TargetFlags.GetValueOrDefault() & 2) == 2)
            {
                return Roster(GetActor(targetId).Side).Where(IsExpandedAiTarget).ToArray();
            }
            return CurrentExpandedAiTargets(aiRecipients);
        }
        BattleCombatant target = GetActor(targetId);
        if (ability is BattleAbility.Attack or BattleAbility.Draw)
        {
            return target.IsActive && target.IsAlive && !target.Untargetable ? [target] : [];
        }
        BattleActionDefinition action = ability switch
        {
            BattleAbility.LimitBreak when native is not null => ExpandedLimitDefinition(actor, native),
            BattleAbility.NativeCommand when native is not null => ExpandedOrdinaryCommandDefinition(native),
            _ => FindAction(ability, actionId)
        };
        if (ability == BattleAbility.NativeCommand && native is not null && IsNativeLevelCommand(native) &&
            !CanRebindExpandedEnemyLevel(target, ProjectNativeLevel(native, target.Level)))
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

    private void PublishExpandedEffectContact(BattleActionPresentation command, string recipient, NativeBattleContactHalf contact)
    {
        if (!expandedPresentedRecipients.Add(recipient))
        {
            throw new InvalidDataException("The expanded effect repeated a published recipient.");
        }
        PublishExpandedImpact(command, recipient, contact, "EffectContact");
    }

    private void PublishExpandedImpact(BattleActionPresentation command, string recipient, NativeBattleContactHalf contact, string eventKind)
    {
        string actionName = command.NativeAction?.Name ?? ActionName(command.Ability, command.ActionId);
        Log(eventKind, command.ActorId, recipient, contact.PresentedAmount, actionName);
        if (contact.PresentedAmount > 0)
        {
            bool healing = (contact.HitFlags3 & 1) != 0;
            Log(healing ? "EffectHealing" : "EffectDamage", command.ActorId, recipient, contact.PresentedAmount, actionName);
        }
    }
}
