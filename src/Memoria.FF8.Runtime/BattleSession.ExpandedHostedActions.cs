using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal enum ExpandedHostedContactDisposition
    {
        RequiredImpacts,
        ContextOnly
    }

    internal abstract record ExpandedHostedOperation;
    private sealed record ExpandedHostedDescriptor(HostedDispatchControl Control) : ExpandedHostedOperation;

    internal sealed record ExpandedHostedInvocation(BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header, ImmutableArray<ExpandedBattleContact> Contacts, BattlePendingEnemyDraw? EnemyDraw, ExpandedHostedContactDisposition ContactDisposition) : ExpandedHostedOperation
    {
        internal ExpandedAiInvocationReservation? Reservation { get; init; }
        internal ImmutableArray<ExpandedHostedInvocation> FollowingCasts { get; init; } = [];
        internal bool PendingGunblade { get; init; }
        internal bool PendingGuardian { get; init; }
        internal BattleActionHeaderReference? HeaderReference { get; init; }
        internal BattleContactCursor FirstContact { get; init; }
        internal ImmutableArray<NativeBattleContactPacket> RoutedGraphicsContacts { get; init; } = [];
        internal int ContactCount => Contacts.Length + FollowingCasts.Sum(cast => cast.Contacts.Length);
        internal int RequiredContactCount => ContactDisposition == ExpandedHostedContactDisposition.RequiredImpacts
            ? Contacts.Length + FollowingCasts.Where(cast => cast.ContactDisposition == ExpandedHostedContactDisposition.RequiredImpacts).Sum(cast => cast.Contacts.Length)
            : 0;
        internal IEnumerable<ExpandedBattleContact> AllContacts => Contacts.Concat(FollowingCasts.SelectMany(cast => cast.Contacts));
    }

    internal sealed record ExpandedHostedEvent(BattleActionPresentation Command, BattleActionPresentation ActivePresentation, uint Callback, long? CompletionToken, bool LogicallyComplete = false, BattleModelOwner? CompletionOwner = null)
    {
        internal HostedDuelPoll? DuelPoll { get; init; }
        internal HostedRenzokukenOwner? Renzokuken { get; init; }
    }

    internal sealed class ExpandedInvocationProgress(ExpandedHostedInvocation invocation, BattleModelRoster roster, NativeBattleEffectContactBridge<string>? bridge)
    {
        internal BattleActionPresentation Command => invocation.Command;
        internal int ExpectedContacts { get; } = invocation.RequiredContactCount;
        internal BattleModelRoster Roster { get; } = roster;
        internal NativeBattleEffectContactBridge<string>? Bridge { get; set; } = bridge;
        internal HashSet<int> Occurrences { get; } = [];
        internal int PresentedRoutedContacts { get; set; }
        internal BattlePendingEnemyDraw? EnemyDraw = invocation.EnemyDraw;
    }

    private ExpandedInvocationProgress? expandedInvocationProgress;
    private bool preparingExpandedHostedAction;
    internal record struct ExpandedOperationCursor(ImmutableArray<ExpandedHostedOperation> Operations, int Next = 0, int? Current = null);

    private ExpandedOperationCursor expandedOperationCursor = new([]);
    private long? expandedHostedActionToken;
    private ExpandedHostedEvent? expandedHostedEvent;
    private bool OwnsExpandedHostedInvocation => expandedPresentationProcess is not null || preparingExpandedHostedAction;
    private ExpandedHostedInvocation? CurrentExpandedHostedInvocation => expandedOperationCursor.Current is int current ? (ExpandedHostedInvocation)expandedOperationCursor.Operations[current] : null;
    private bool HasStagedExpandedHostedOperations => !expandedOperationCursor.Operations.IsEmpty;

    private void RequireOrdinaryExpandedHostedCommand(BattleActionPresentation command)
    {
        // TODO(TD-009): Bind the remaining original invocation processes to hosted action ownership.
        bool shot = IsShotCallback(command.NativeLimitCallback);
        bool ordinaryLimit = command is { Ability: BattleAbility.LimitBreak, NativeLimitCallback: null, NativeAction: { } native } &&
            command.NativeQueuedCommand == native.Command && command.ActionId == (native.Command << 16 | native.ActionId) &&
            GetActor(command.ActorId).NativeLimit?.Command == native.Command &&
            (command.NativeSlots is { } slots ? CanCastSlots(native, slots) : SupportsExpandedLimit(native));
        if (command.GuardianFormAction is not null)
        {
            ValidateGuardianFormPresentation(command);
            return;
        }
        if (command.Ability == BattleAbility.LimitBreak && !shot && !ordinaryLimit || command.Ability == BattleAbility.GuardianForce && command.NativeQueuedCommand != 254 ||
            command.GuardianFormInvocation is not null || command.NativeLimitCallback is not null && !shot)
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
        if (IsHostedCinematicContext(command, header) && contacts.Any(contact => contact.Packet is not
            { Primary: { PresentedAmount: 0, Flags2: 1, HitFlags: 0, Sequence: 0, CoverSource: null }, Secondary: null }))
        {
            throw new InvalidDataException("A native context-only cinematic produced a contact that requires publication.");
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
        for (int index = 0; index < expandedOperationCursor.Operations.Length; index++)
        {
            if (expandedOperationCursor.Operations[index] is ExpandedHostedInvocation reserved && ReferenceEquals(reserved.Command, command) && reserved.Header == header && reserved.Contacts.IsDefault)
            {
                expandedOperationCursor.Operations = expandedOperationCursor.Operations.SetItem(index, invocation with { Reservation = reserved.Reservation });
                return;
            }
        }
        if (!producingExpandedAiCallback)
        {
            expandedOperationCursor.Operations = expandedOperationCursor.Operations.Add(invocation);
            return;
        }
        throw new InvalidDataException("The AI command has no reserved position in its hosted event.");
    }

    private void ReserveExpandedAiInvocation(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        if (preparingExpandedHostedAction)
        {
            // The parent header precedes any commands emitted by an immediate stage4 counter.
            producingExpandedAiInvocation = new(header.Source, command);
            expandedOperationCursor.Operations = expandedOperationCursor.Operations.Add(new ExpandedHostedInvocation(command, header, default, null, ReadExpandedHostedContactDisposition(command, header)) { Reservation = producingExpandedAiInvocation });
        }
    }

    private bool IsStagedExpandedAiInvocation(ExpandedAiInvocationReservation reservation)
    {
        return expandedOperationCursor.Operations.OfType<ExpandedHostedInvocation>().Any(invocation => Matches(invocation) || invocation.FollowingCasts.Any(Matches));

        bool Matches(ExpandedHostedInvocation invocation)
        {
            return ReferenceEquals(invocation.Reservation, reservation) && invocation.Header.Source == reservation.Source &&
                ReferenceEquals(invocation.Command, reservation.Command) && !invocation.Contacts.IsDefault;
        }
    }

    private ExpandedHostedContactDisposition ReadExpandedHostedContactDisposition(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        if (command.NativeAction is { PresentationOnly: true })
        {
            if (header is not { Command: 0, Action: 9, Effect: 0, Source: BattleEnemyActorModel source } ||
                definition.EncounterMod is not { } mod || header.Sequence != mod.CastingSequence || source.Actor.ActorId != command.ActorId ||
                command.ActorId != mod.OriginalActorId || command.TargetId != command.ActorId || command.NativeAiCallbackStage != 1 || encounterPolicies?.PendingSummon is null)
            {
                throw new InvalidDataException("Hosted summon requires its admitted original command 0 casting control.");
            }
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        if (command.NativeQueuedCommand == 11 && header is { Command: 11, Action: 65531 } ||
            command.NativeQueuedCommand == 241 && header is { Command: 241, Action: 65530 })
        {
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        if (command is { NativeQueuedCommand: 14, NativeLimitCallback: NativeLimitCallback.ShotOpening } && header.Command == 14)
        {
            // Shot's opening launches the module and publishes readiness without applying the header contacts.
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        if (command.NativeGraphic is { } graphic && header is { Command: 252, Action: 1 } && header.Effect == graphic.MagicId)
        {
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        if (command.NativeGraphic is not null || header.Command == 252)
        {
            throw new InvalidDataException("Hosted graphics contact context requires its exact native command252 header.");
        }
        if (IsHostedCinematicContext(command, header))
        {
            byte expectedAttackFlags = header.Effect == NativeBattleEffect298Profile.Effect ? (byte)0 : (byte)3;
            if (command.NativeAction is not { AttackType: 0, Power: 0, HitCount: 1, DamagePresentation: 0, Element: 0, StatusAttack: 0, Statuses0: 0, Statuses1: 0 } native ||
                native.AttackFlags != expectedAttackFlags ||
                native.Command != header.Command || native.ActionId != header.Action || native.MagicId != header.Effect)
            {
                throw new InvalidDataException("A native context-only cinematic requires its original zero-impact action.");
            }
            return ExpandedHostedContactDisposition.ContextOnly;
        }
        return ExpandedHostedContactDisposition.RequiredImpacts;
    }

    private bool IsHostedCinematicContext(BattleActionPresentation command, NativeBattleActionHeaderPreparation header)
    {
        if (header.Effect == NativeBattleEffect101Profile.Effect)
        {
            // Trauma's counter uses the dormant Droma packet for model setup. Its recipient sequence has no contact event.
            if (header is not { Command: 8, Action: 290, Sequence: 13, Source: BattleEnemyActorModel source } || definition.EncounterId != 431 ||
                command is not { Ability: BattleAbility.EnemyAbility, NativeAiCallbackStage: 2, NativeAction: { KernelSection: 3, KernelIndex: 290 } } ||
                source.Actor.ActorId != command.ActorId || GetActor(command.ActorId) is not { ModelId: 109, SceneSlot: 1 } ||
                command.TargetRequest.Selection is not NativeBattleTargetRequestAdapter.DeferredOperands { Slots: [var slot], ReservedPositions.IsEmpty: true } ||
                ResolveLastNativeAllocation() is not { ModelId: 110, SceneSlot: 0 or 2, IsActive: true, Hidden: true, Suspended: true, Untargetable: true } droma ||
                ReadOriginalTargetOccupant(slot) != TargetActorReference(droma))
            {
                throw new InvalidDataException("Hosted Trauma counter requires its original dormant Droma recipient.");
            }
            return true;
        }
        (ushort Action, byte Sequence, int Model, bool Death)? transition = header.Effect switch
        {
            // NORG's death dialogue marks its side models unavailable without consuming the neutral self contact.
            0 when header is { Command: 8, Action: 289 } => ((ushort)289, (byte)19, 77, true),
            0 when header is { Command: 8, Action: 74, Sequence: 12 } && command.NativeAiCallbackStage is 1 or 3 && GetActor(command.ActorId).ModelId == 105 => ((ushort)74, (byte)12, 105, false),
            0 when header is { Command: 8, Action: 74, Sequence: 13 } && command.NativeAiCallbackStage == 1 && GetActor(command.ActorId).ModelId == 105 => ((ushort)74, (byte)13, 105, false),
            NativeBattleMorph172Profile.Effect => ((ushort)53, (byte)13, 29, false),
            // Trauma's death sequence keeps its neutral self contact for setup and never calls 506690 for it.
            NativeBattleEffect290Profile.Effect => ((ushort)291, (byte)14, 109, true),
            NativeBattleEffect298Profile.Effect => ((ushort)119, (byte)14, 102, true),
            NativeBattleEffect308Profile.Effect => ((ushort)286, (byte)14, 105, true),
            NativeBattleEffect314Profile.Effect => ((ushort)191, (byte)16, 103, true),
            NativeBattleEffect316Profile.Effect => ((ushort)203, (byte)15, 130, true),
            276 => ((ushort)145, (byte)19, 92, true),
            _ => null
        };
        if (transition is { } expected)
        {
            var (action, sequence, model, death) = expected;
            if (header is not { Command: 8, Source: BattleEnemyActorModel source } || header.Action != action || header.Sequence != sequence ||
                command is not { Ability: BattleAbility.EnemyAbility, NativeAction: { KernelSection: 3 } native } || native.KernelIndex != action ||
                command.ActorId != command.TargetId || source.Actor.ActorId != command.ActorId || GetActor(command.ActorId).ModelId != model ||
                command.TargetRequest.Selection is not BattleTargetSelection.Explicit { Actors: [BattleEnemyActorReference target] } || target != source.Actor ||
                death && command.NativeAiCallbackStage != 3)
            {
                throw new InvalidDataException("Hosted actor transition requires its original self-targeted enemy action context.");
            }
            return true;
        }
        // The dedicated 69 and 227 controllers retire a presentation context without a 506690 contact consumer.
        return command.NativeGraphic is null && command.NativeLimitCallback != NativeLimitCallback.ShotOpening &&
            (header.Effect is NativeBattleEffect69Profile.Effect or NativeBattleEffect227Profile.Effect ||
                NativeBattleCinematicProfile.IsSupported(header.Effect) &&
                NativeBattleCinematicProfile.ForEffect(header.Effect).ContactPublication == NativeBattleCinematicContactPublication.ContextOnly);
    }

    private BattlePendingEnemyDraw? TakePendingHostedEnemyDraw()
    {
        BattlePendingEnemyDraw? draw = pendingEnemyDraw;
        pendingEnemyDraw = null;
        return draw;
    }

    private void ValidateExpandedHostedInvocations(BattleModelRoster roster)
    {
        if (ReadBattlePresentationRoster().Owners.Any(owner => !roster.Contains(owner)))
        {
            throw new InvalidDataException("Hosted invocation roster differs from the logical allocations.");
        }
        foreach (ExpandedHostedInvocation invocation in expandedOperationCursor.Operations.Skip(expandedOperationCursor.Current ?? expandedOperationCursor.Next).OfType<ExpandedHostedInvocation>())
        {
            ValidateCast(invocation);
            foreach (ExpandedHostedInvocation cast in invocation.FollowingCasts)
            {
                ValidateCast(cast);
            }
        }

        void ValidateCast(ExpandedHostedInvocation cast)
        {
            BattleCombatant actor = GetActor(cast.Command.ActorId);
            _ = GetActor(cast.Command.TargetId);
            if (cast.Header.Caster != CreatePresentationModelOwner(actor) || !roster.Contains(cast.Header.Source) || cast.Contacts.IsDefault)
            {
                throw new InvalidDataException("Hosted invocation header does not identify its canonical caster and graphics source.");
            }
            ValidateExpandedEffectContacts(cast.Header.Caster, cast.Contacts, roster);
        }
    }


    private static NativeBattleEffectContactBridge<string>? PrepareExpandedHostedContactBridge(ExpandedHostedInvocation invocation, BattleModelRoster roster)
    {
        return invocation.Contacts.IsEmpty ? null : new(invocation.AllContacts.Select(contact => (contact.RecipientId, contact.Packet)), roster, invocation.FirstContact);
    }

    private void BindExpandedHostedContactTracking(ExpandedHostedInvocation invocation, BattleModelRoster roster, NativeBattleEffectContactBridge<string>? bridge)
    {
        expandedInvocationProgress = new(invocation, roster, bridge);
    }

    private void ApplyExpandedHostedFrameReceipts(NativeBattleModelLifecycleServices services, long? actionToken)
    {
        foreach (NativeBattlePresentationReceipt receipt in services.ReadOrderedReceipts())
        {
            switch (receipt)
            {
                case NativeBattlePresentationContactReceived { Invocation: { Provenance: BattlePresentationInvocationProvenance.ExternalDiagnostic } invocation } external:
                    if (actionToken != invocation.Token || external.Renzokuken is not null || external.Packet.Primary.Amount6 != 0 || external.Packet.Secondary is not null)
                    {
                        throw new InvalidDataException("External presentation contact lost its diagnostic invocation ownership.");
                    }
                    break;
                case NativeBattlePresentationContactReceived contact:
                    if (actionToken is null || actionToken != expandedHostedActionToken || expandedInvocationProgress is not { } progress)
                    {
                        throw new InvalidDataException("Hosted contact arrived without its active logical action.");
                    }
                    if (CurrentExpandedHostedInvocation is { } active && progress.PresentedRoutedContacts < active.RoutedGraphicsContacts.Length &&
                        ReferenceEquals(active.RoutedGraphicsContacts[progress.PresentedRoutedContacts], contact.Packet))
                    {
                        progress.PresentedRoutedContacts++;
                        NativeBattleContactHalf routed = contact.Packet.Primary;
                        PublishExpandedImpact(progress.Command, RequireCanonicalGraphicsActorId(routed.Recipient), routed, "EffectContact", deferredImpact: true);
                        PublishExpandedReadyContact(progress.Roster, routed, progress.Command.ActorId);
                        break;
                    }
                    if (progress.Bridge is not { } contactBridge)
                    {
                        throw new InvalidDataException("A hosted control invocation published an unexpected contact.");
                    }
                    ValidateExpandedHostedRenzokukenContact(contact, actionToken.Value);
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
                        (int ordinal, _) = contactBridge.ResolveOccurrence(contact.Packet);
                        if (!progress.Occurrences.Add(ordinal))
                        {
                            throw new InvalidDataException("Hosted graphics repeated a contact context occurrence.");
                        }
                        break;
                    }
                    BattleActionPresentation command = progress.Command;
                    NativeBattleContactHalf? secondary = ReadExpandedSecondaryContact(command, contact.Packet);
                    (int impact, string recipient) = contactBridge.ResolveOccurrence(contact.Packet);
                    if (impact >= progress.ExpectedContacts)
                    {
                        throw new InvalidDataException("Hosted graphics published a contact outside its action header traversal.");
                    }
                    PublishExpandedEffectContact(progress, impact, recipient, contact.Packet.Primary);
                    PublishExpandedReadyContact(progress.Roster, contact.Packet.Primary, command.ActorId);
                    if (secondary is not null)
                    {
                        PublishExpandedImpact(command, command.ActorId, secondary, "SecondaryEffectContact", deferredImpact: true);
                        PublishExpandedReadyContact(progress.Roster, secondary, command.ActorId);
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
                case NativeBattlePresentationFileAudioRequested audio:
                    events.Add(new BattleEvent(Frame, "NativeFileAudio", null, null, 0, string.Empty) { NativeFileAudio = audio.Request });
                    break;
                case NativeBattleModelTextPublished text:
                    events.Add(new BattleEvent(Frame, "NativeText", text.ActorId, null, text.Value, text.Text) { NativeMessageMode = 3, NativeTextIdentity = text.TextIdentity });
                    break;
                case NativeBattleModelTurnAdvanceStopRequested stop:
                    _ = RequireEmittingHostedActor(services, stop);
                    StopTurnAdvance();
                    break;
                case NativeBattleShotWidgetUpdated shotWidget:
                    ApplyNativeShotWidgetUpdate(shotWidget);
                    break;
                case NativeBattlePresentationHostEvent presentationEvent:
                    events.Add(presentationEvent.Event);
                    break;
                case NativeBattlePresentationDescriptorText descriptorText:
                    events.Add(descriptorText.Message with { Frame = Frame });
                    break;
                case NativeBattleModelParticipantReleased released:
                    ApplyExpandedModelRelease(RequireEmittingHostedActor(services, released));
                    break;
                case NativeBattleModelParticipantActivated activated:
                    AcknowledgeHostedNativeActivation(services, activated);
                    break;
                case NativeBattleModelParticipantSuspended suspended:
                    ApplyExpandedModelSuspension(RequireEmittingHostedActor(services, suspended), suspended);
                    break;
                case NativeBattleAllocationStatisticsRequested statistics:
                    ApplyHostedAllocationStatistics(services, statistics);
                    break;
                case NativeBattleAllocationGateReached:
                    CompleteNativeAllocationGate();
                    break;
                case NativeBattlePartyReplacementReached replacement:
                    ApplyPartyReplacement(replacement.Descriptor);
                    break;
                case NativeBattleModelTriggerRegistered trigger:
                    AcknowledgeExpandedGunbladeRegistration(trigger, actionToken);
                    break;
                case NativeBattleGuardianFormLanded landing:
                    _ = RequireEmittingHostedActor(services, landing);
                    ApplyExpandedHostedGuardianFormLanding(expandedPresentationProcess!, landing);
                    break;
                case NativeBattleShotReadinessReached shot:
                    ApplyExpandedHostedShotReadiness(services, shot, actionToken);
                    break;
                case NativeBattleDuelReadinessReached duelReadiness:
                    ApplyExpandedHostedDuelReadiness(services, duelReadiness);
                    break;
                case NativeBattleDuelSelectionReached duelSelection:
                    ApplyExpandedHostedDuelSelection(services, duelSelection, actionToken);
                    break;
                case NativeBattleRenzokukenFinisherReached finisher:
                    ApplyExpandedHostedRenzokukenFinisher(services, finisher, actionToken);
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
        BattlePresentationProcess process = expandedPresentationProcess!;
        AdvanceHostedEncounterAllocation(process);
        if (HasPendingEncounterRetirements)
        {
            _ = AdvanceEncounterAllocationRetirements(process.Graphics, process);
        }
    }
}
