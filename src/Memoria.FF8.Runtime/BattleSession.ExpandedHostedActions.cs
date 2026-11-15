using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private enum ExpandedHostedContactDisposition
    {
        RequiredImpacts,
        ContextOnly
    }

    private sealed record ExpandedHostedInvocation(BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header, ImmutableArray<ExpandedBattleContact> Contacts, BattlePendingEnemyDraw? EnemyDraw, ExpandedHostedContactDisposition ContactDisposition)
    {
        internal bool PendingGunblade { get; init; }
        internal BattleActionHeaderReference? HeaderReference { get; init; }
        internal BattleContactCursor FirstContact { get; init; }
    }

    private sealed record ExpandedHostedEvent(BattleActionPresentation Command, BattleActionPresentation ActivePresentation, uint Callback, long? CompletionToken);

    private bool preparingExpandedHostedAction;
    private ImmutableArray<ExpandedHostedInvocation> expandedHostedInvocations = [];
    private long? expandedHostedActionToken;
    private ExpandedHostedEvent? expandedHostedEvent;
    private bool OwnsExpandedHostedInvocation => expandedPresentationProcess is not null || preparingExpandedHostedAction;

    private static void RequireOrdinaryExpandedHostedCommand(BattleActionPresentation command)
    {
        // TODO(TD-009): Finish nonordinary invocation ownership before enabling the attached host in App.
        if (command.Ability is BattleAbility.GuardianForce or BattleAbility.LimitBreak || command.GuardianFormAction is not null ||
            command.GuardianFormInvocation is not null || command.IndependentGuardian is not null || command.NativeLimitCallback is not null)
        {
            throw new NotSupportedException("Hosted presentation has no admission for this nonordinary invocation.");
        }
    }

    private void StageExpandedHostedInvocation(BattleActionPresentation command, NativeBattleActionHeaderPreparation header, ImmutableArray<ExpandedBattleContact> contacts, BattlePendingEnemyDraw? enemyDraw)
    {
        StageExpandedHostedInvocationCore(command, header, contacts, enemyDraw, pendingGunblade: false);
    }

    private void StageExpandedHostedInvocationCore(BattleActionPresentation command, NativeBattleActionHeaderPreparation header, ImmutableArray<ExpandedBattleContact> contacts, BattlePendingEnemyDraw? enemyDraw, bool pendingGunblade)
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
        expandedHostedInvocations = expandedHostedInvocations.Add(new(command, header, contacts, enemyDraw, disposition) { PendingGunblade = pendingGunblade });
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
        foreach (ExpandedHostedInvocation invocation in expandedHostedInvocations)
        {
            BattleCombatant actor = GetActor(invocation.Command.ActorId);
            _ = GetActor(invocation.Command.TargetId);
            if (invocation.Header.Source != RequirePresentationModelOwner(roster, actor))
            {
                throw new InvalidDataException("Hosted invocation header does not identify its canonical caster.");
            }
            ValidateExpandedEffectContacts(actor, invocation.Contacts, roster);
        }
    }

    private PreparedHostedDispatch PrepareExpandedHostedEventSegment(ExpandedBattlePresentationProcess process, BattleModelRoster roster, BattleActionPresentation command, uint callback, PreparedHostedCompletionAck? acknowledgment = null)
    {
        ImmutableArray<HostedDispatchHeader> headers = expandedHostedInvocations.Select(invocation => new HostedDispatchHeader(invocation.Header, invocation.Contacts.Select(contact => contact.Packet).ToImmutableArray())).ToImmutableArray();
        HostedDispatchCompletion? completion = headers.Length == 1 ? ReadExpandedHostedCompletion(roster, command, callback) : null;
        PreparedHostedDispatch prepared = process.PrepareDispatch(headers, dispatchAppended: true, completion: completion, acknowledgment: acknowledgment);
        expandedHostedInvocations = expandedHostedInvocations.Select((invocation, index) =>
        {
            BattleActionHeaderReference header = prepared.AppendedHeaders[index];
            BattleContactCursor firstContact = prepared.Packets.ReadHeader(header).FirstContact;
            return invocation with { HeaderReference = header, FirstContact = firstContact };
        }).ToImmutableArray();
        return prepared;
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

    private void AdvanceExpandedHostedInvocation(long? token)
    {
        if (expandedHostedEvent is not { } hostedEvent || token is null || token != expandedHostedActionToken)
        {
            return;
        }
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("A hosted action lost its attached process.");
        if (!process.IsActionComplete(token.Value))
        {
            return;
        }
        if (expandedHostedInvocations[0].PendingGunblade)
        {
            throw new InvalidDataException("Hosted Gunblade completed with an unresolved placeholder.");
        }
        ValidateExpandedEffectContactCompletion();
        if (expandedHostedInvocations.Length == 1)
        {
            if (hostedEvent.CompletionToken is not long completionToken || !process.IsCompletionQueued(completionToken))
            {
                throw new InvalidDataException("The final hosted action retired without its owned native completion.");
            }
            return;
        }

        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        ValidateExpandedHostedInvocations(roster);
        ImmutableArray<ExpandedHostedInvocation> remaining = expandedHostedInvocations.RemoveAt(0);
        ExpandedHostedInvocation invocation = remaining[0];
        HostedDispatchCompletion? completion = remaining.Length == 1 ? ReadExpandedHostedCompletion(roster, hostedEvent.Command, hostedEvent.Callback) : null;
        BattleActionHeaderReference reference = invocation.HeaderReference ?? throw new InvalidDataException("Hosted invocation lost its prepared header index.");
        PreparedHostedDispatch next = process.PrepareDispatch([], headerReference: reference, completion: completion);
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(invocation, roster);
        ExpandedHostedEvent nextEvent = hostedEvent with { CompletionToken = next.Completion?.Request.Token };
        process.ValidatePreparedDispatch(next);
        expandedHostedInvocations = remaining;
        BindExpandedHostedContactTracking(invocation, roster, bridge);
        expandedHostedEvent = nextEvent;
        expandedHostedActionToken = next.Action!.Invocation.Token;
        process.CommitDispatch(next);
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
                    if (expandedHostedInvocations[0].PendingGunblade)
                    {
                        throw new InvalidDataException("Hosted Gunblade published its placeholder before trigger recomputation.");
                    }
                    if (expandedHostedInvocations[0].ContactDisposition == ExpandedHostedContactDisposition.ContextOnly)
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
