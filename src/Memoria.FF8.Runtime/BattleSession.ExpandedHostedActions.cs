using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private enum ExpandedHostedContactDisposition
    {
        RequiredImpacts,
        ContextOnly
    }

    private abstract record ExpandedHostedOperation;
    private sealed record ExpandedHostedDescriptor(HostedDispatchControl Control) : ExpandedHostedOperation;

    private sealed record ExpandedHostedInvocation(BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header, ImmutableArray<ExpandedBattleContact> Contacts, BattlePendingEnemyDraw? EnemyDraw, ExpandedHostedContactDisposition ContactDisposition) : ExpandedHostedOperation
    {
        internal ExpandedAiInvocationReservation? Reservation { get; init; }
        internal bool PendingGunblade { get; init; }
        internal bool PendingGuardian { get; init; }
        internal BattleActionHeaderReference? HeaderReference { get; init; }
        internal BattleContactCursor FirstContact { get; init; }
    }

    private sealed record ExpandedHostedEvent(BattleActionPresentation Command, BattleActionPresentation ActivePresentation, uint Callback, long? CompletionToken, bool LogicallyComplete = false);

    private bool preparingExpandedHostedAction;
    private ImmutableArray<ExpandedHostedOperation> expandedHostedOperations = [];
    private int expandedHostedNextOperation;
    private int? expandedHostedCurrentOperation;
    private long? expandedHostedActionToken;
    private ExpandedHostedEvent? expandedHostedEvent;
    private bool OwnsExpandedHostedInvocation => expandedPresentationProcess is not null || preparingExpandedHostedAction;
    private ExpandedHostedInvocation? CurrentExpandedHostedInvocation => expandedHostedCurrentOperation is int current ? (ExpandedHostedInvocation)expandedHostedOperations[current] : null;
    private bool HasStagedExpandedHostedOperations => !expandedHostedOperations.IsEmpty;

    private void ReplaceCurrentExpandedHostedInvocation(ExpandedHostedInvocation expected, ExpandedHostedInvocation replacement, NativeBattleEffectContactBridge<string>? bridge)
    {
        if (!ReferenceEquals(CurrentExpandedHostedInvocation, expected) ||
            !ReferenceEquals(expected.Command, replacement.Command) || expected.HeaderReference != replacement.HeaderReference ||
            expected.FirstContact != replacement.FirstContact || expected.Contacts.Length != replacement.Contacts.Length)
        {
            throw new InvalidDataException("Hosted contact replacement requires the exact current invocation and its admitted extent.");
        }
        expandedHostedOperations = expandedHostedOperations.SetItem(expandedHostedCurrentOperation!.Value, replacement);
        expandedContactBridge = bridge;
        expandedModelContacts = replacement.Contacts;
        expandedEffectEnemyDraw = replacement.EnemyDraw;
    }

    private static void RequireOrdinaryExpandedHostedCommand(BattleActionPresentation command)
    {
        // TODO(TD-009): Finish nonordinary invocation ownership before enabling the attached host in App.
        if (command.Ability == BattleAbility.LimitBreak || command.Ability == BattleAbility.GuardianForce && command.NativeQueuedCommand != 254 || command.GuardianFormAction is not null ||
            command.GuardianFormInvocation is not null || command.IndependentGuardian is not null || command.NativeLimitCallback is not null)
        {
            throw new NotSupportedException("Hosted presentation has no admission for this nonordinary invocation.");
        }
    }

    private void StageExpandedHostedInvocation(BattleActionPresentation command, NativeBattleActionHeaderPreparation header, ImmutableArray<ExpandedBattleContact> contacts, BattlePendingEnemyDraw? enemyDraw)
    {
        StageExpandedHostedInvocationCore(command, header, contacts, enemyDraw, pendingGunblade: false);
    }

    private void StageExpandedHostedInvocationCore(BattleActionPresentation command, NativeBattleActionHeaderPreparation header, ImmutableArray<ExpandedBattleContact> contacts, BattlePendingEnemyDraw? enemyDraw, bool pendingGunblade, bool pendingGuardian = false)
    {
        if (!preparingExpandedHostedAction)
        {
            throw new InvalidOperationException("Hosted invocation staging requires the logical preparation candidate.");
        }
        ExpandedHostedContactDisposition disposition = ReadExpandedHostedContactDisposition(command, header);
        if (contacts.IsDefault || contacts.IsEmpty && disposition != ExpandedHostedContactDisposition.ContextOnly && header is not { Command: 0, Sequence: 10, Action: 9, Effect: 0 })
        {
            throw new NotSupportedException("Hosted presentation requires an explicit contact list, its original failed control header, or a native graphics header.");
        }
        if ((contacts.IsEmpty || disposition == ExpandedHostedContactDisposition.ContextOnly) && enemyDraw is not null)
        {
            throw new InvalidDataException("A hosted event cannot defer pending Draw without an expected impact.");
        }
        if (pendingGunblade && (contacts.Length != 1 || header.Command is not (1 or 12 or 28)))
        {
            throw new InvalidDataException("Hosted ordinary Gunblade requires one preliminary contact and its original command.");
        }
        if (pendingGuardian && (pendingGunblade || contacts.IsEmpty || header.Command != 254))
        {
            throw new InvalidDataException("Hosted Guardian Force requires preliminary contacts and its original command254 header.");
        }
        ExpandedHostedInvocation invocation = new(command, header, contacts, enemyDraw, disposition) { PendingGunblade = pendingGunblade, PendingGuardian = pendingGuardian };
        for (int index = 0; index < expandedHostedOperations.Length; index++)
        {
            if (expandedHostedOperations[index] is ExpandedHostedInvocation reserved && ReferenceEquals(reserved.Command, command) && reserved.Header == header && reserved.Contacts.IsDefault)
            {
                expandedHostedOperations = expandedHostedOperations.SetItem(index, invocation with { Reservation = reserved.Reservation });
                return;
            }
        }
        if (!producingExpandedAiCallback)
        {
            expandedHostedOperations = expandedHostedOperations.Add(invocation);
            return;
        }
        throw new InvalidDataException("The AI command has no reserved position in its hosted event.");
    }

    private void ReserveExpandedAiInvocation(BattleActionPresentation command, NativeBattleActionHeaderPreparation? header)
    {
        if (preparingExpandedHostedAction && header is not null)
        {
            // The parent header precedes any commands emitted by an immediate stage4 counter.
            producingExpandedAiInvocation = new(header.Source, command);
            expandedHostedOperations = expandedHostedOperations.Add(new ExpandedHostedInvocation(command, header, default, null, ReadExpandedHostedContactDisposition(command, header)) { Reservation = producingExpandedAiInvocation });
        }
    }

    private bool IsStagedExpandedAiInvocation(ExpandedAiInvocationReservation reservation)
    {
        return expandedHostedOperations.OfType<ExpandedHostedInvocation>().Any(invocation => ReferenceEquals(invocation.Reservation, reservation) &&
            invocation.Header.Source == reservation.Source && ReferenceEquals(invocation.Command, reservation.Command) && !invocation.Contacts.IsDefault);
    }

    private static ExpandedHostedContactDisposition ReadExpandedHostedContactDisposition(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        if (command.NativeGraphic is { } graphic && header is { Command: 252, Action: 1 } && header.Effect == graphic.MagicId)
        {
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        if (command.NativeGraphic is not null || header.Command == 252)
        {
            throw new InvalidDataException("Hosted graphics contact context requires its exact native command252 header.");
        }
        return ExpandedHostedContactDisposition.RequiredImpacts;
    }

    private BattlePendingEnemyDraw? TakePendingHostedEnemyDraw()
    {
        BattlePendingEnemyDraw? draw = pendingEnemyDraw;
        pendingEnemyDraw = null;
        return draw;
    }

    private void ValidateExpandedHostedInvocations(BattleModelRoster roster)
    {
        if (!roster.Owners.SequenceEqual(ReadBattlePresentationRoster().Owners))
        {
            throw new InvalidDataException("Hosted invocation roster differs from the logical allocations.");
        }
        foreach (ExpandedHostedInvocation invocation in expandedHostedOperations.OfType<ExpandedHostedInvocation>())
        {
            BattleCombatant actor = GetActor(invocation.Command.ActorId);
            _ = GetActor(invocation.Command.TargetId);
            if (invocation.Header.Source != RequirePresentationModelOwner(roster, actor) || invocation.Contacts.IsDefault)
            {
                throw new InvalidDataException("Hosted invocation header does not identify its canonical caster.");
            }
            ValidateExpandedEffectContacts(actor, invocation.Contacts, roster);
        }
    }

    private HostedDispatchCompletion ReadExpandedHostedCompletion(BattleModelRoster roster, BattleActionPresentation command, uint callback)
    {
        return new(callback, RequirePresentationModelOwner(roster, GetActor(command.ActorId)), command.ActorId);
    }

    private static NativeBattleEffectContactBridge<string>? PrepareExpandedHostedContactBridge(ExpandedHostedInvocation invocation, BattleModelRoster roster)
    {
        return invocation.Contacts.IsEmpty ? null : new(invocation.Contacts.Select(contact => (contact.RecipientId, contact.Packet)), roster, invocation.FirstContact);
    }

    private void BindExpandedHostedContactTracking(ExpandedHostedInvocation invocation, BattleModelRoster roster, NativeBattleEffectContactBridge<string>? bridge)
    {
        expandedContactBridge = bridge;
        expandedModelContacts = invocation.Contacts;
        expandedModelRoster = roster;
        expandedEffectCommand = invocation.Command;
        expandedEffectEnemyDraw = invocation.EnemyDraw;
        expandedExpectedContacts = invocation.ContactDisposition == ExpandedHostedContactDisposition.RequiredImpacts ? invocation.Contacts.Length : 0;
        expandedPresentedOccurrences.Clear();
    }

    private void ApplyExpandedHostedFrameReceipts(NativeBattleModelLifecycleServices services, long? actionToken)
    {
        foreach (NativeBattlePresentationReceipt receipt in services.ReadOrderedReceipts())
        {
            switch (receipt)
            {
                case NativeBattlePresentationContactReceived contact:
                    if (actionToken is null || actionToken != expandedHostedActionToken || expandedEffectCommand is not { } command)
                    {
                        throw new InvalidDataException("Hosted contact arrived without its active logical action.");
                    }
                    if (expandedContactBridge is null)
                    {
                        throw new InvalidDataException("A hosted control invocation published an unexpected contact.");
                    }
                    if (CurrentExpandedHostedInvocation is { PendingGunblade: true } or { PendingGuardian: true })
                    {
                        throw new InvalidDataException("Hosted action published a placeholder before recomputation.");
                    }
                    if (CurrentExpandedHostedInvocation is { ContactDisposition: ExpandedHostedContactDisposition.ContextOnly })
                    {
                        if (contact.Packet.Occurrence is null)
                        {
                            throw new InvalidDataException("Hosted graphics context requires its exact contact occurrence.");
                        }
                        (int ordinal, _) = expandedContactBridge.ResolveOccurrence(contact.Packet);
                        if (!expandedPresentedOccurrences.Add(ordinal))
                        {
                            throw new InvalidDataException("Hosted graphics repeated a contact context occurrence.");
                        }
                        break;
                    }
                    AcknowledgeExpandedEffectContact(command, contact.Packet);
                    BattleModelRoster roster = expandedModelRoster!;
                    PublishExpandedReadyContact(roster, contact.Packet.Primary, command.ActorId);
                    if (contact.Packet.Secondary is { } secondary)
                    {
                        PublishExpandedReadyContact(roster, secondary, command.ActorId);
                    }
                    break;
                case NativeBattlePresentationSoundCommitted sound:
                    events.Add(new BattleEvent(Frame, sound.Sequence is null ? "NativeModuleSound" : "NativeSequenceSound", sound.ActorId, null, 0, string.Empty) { NativeModuleSound = sound.Sound, NativeSound = sound.Sequence });
                    break;
                case NativeBattlePresentationStopAllSoundsRequested stopAll:
                    events.Add(new BattleEvent(Frame, "NativeSoundStopAll", null, null, 0, stopAll.Message) { NativeStopAllSounds = true });
                    break;
                case NativeBattlePresentationMusicCommandCommitted music:
                    events.Add(new BattleEvent(Frame, "NativeMusicCommand", null, null, 0, $"Native501B60 consumed singleton command{music.Command.Command}.") { NativeMusicCommand = music.Command });
                    break;
                case NativeBattleModelTextPublished text:
                    events.Add(new BattleEvent(Frame, "NativeText", text.ActorId, null, text.Value, text.Text) { NativeMessageMode = 3, NativeTextIdentity = text.TextIdentity });
                    break;
                case NativeBattleModelTurnAdvanceStopRequested stop:
                    if (RequirePresentationModelOwner(RequireExpandedLifecycleRoster(), GetActor(stop.ActorId)) != stop.Owner)
                    {
                        throw new InvalidDataException("Hosted turn-advance stop does not identify its canonical model owner.");
                    }
                    StopTurnAdvance();
                    break;
                case NativeBattlePresentationDescriptorText descriptorText:
                    events.Add(descriptorText.Message with { Frame = Frame });
                    break;
                case NativeBattleModelParticipantReleased released:
                    AcknowledgeExpandedModelRelease(RequireExpandedLifecycleRoster(), released);
                    break;
                case NativeBattleModelParticipantActivated activated:
                    BattleCombatant actor = RequireExpandedLifecycleActor(RequireExpandedLifecycleRoster(), activated);
                    actor.NativeCombatantFlags &= ~0xC2u;
                    ActivateNativeParticipant(actor);
                    break;
                case NativeBattleModelParticipantSuspended suspended:
                    AcknowledgeExpandedModelSuspension(RequireExpandedLifecycleRoster(), suspended);
                    break;
                case NativeBattleModelTriggerRegistered trigger:
                    AcknowledgeExpandedGunbladeRegistration(trigger, actionToken);
                    break;
                case NativeBattlePresentationCompletionRequested completion:
                    CompleteExpandedHostedEvent(completion, actionToken);
                    break;
                case NativeBattleTerminalEntered entered:
                    AcknowledgeHostedTerminalEntry(entered);
                    break;
                case NativeBattleTerminalFinished finished:
                    AcknowledgeHostedTerminalFinish(finished);
                    break;
                default:
                    // TODO(TD-009): Finish the remaining hosted lifecycle acknowledgements before App attachment.
                    throw new NotSupportedException($"Hosted parent acknowledgement does not handle {receipt.GetType().Name}.");
            }
        }
        AdvanceExpandedHostedInvocation(actionToken);
    }
}
