using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record ExpandedHostedInvocation(BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header, ImmutableArray<ExpandedBattleContact> Contacts, BattlePendingEnemyDraw? EnemyDraw)
    {
        internal bool PendingGunblade { get; init; }
        internal bool CommittedGunblade { get; init; }
    }

    private bool preparingExpandedHostedAction;
    private ImmutableArray<ExpandedHostedInvocation> expandedHostedInvocations = [];
    private long? expandedHostedActionToken;
    private bool OwnsExpandedHostedInvocation => expandedPresentationProcess is not null || preparingExpandedHostedAction;

    private static void RequireOrdinaryExpandedHostedCommand(BattleActionPresentation command)
    {
        // TODO(TD-009): Finish nonordinary invocation ownership before enabling the attached host in App.
        if (command.Ability is BattleAbility.GuardianForce or BattleAbility.LimitBreak || command.GuardianFormAction is not null ||
            command.GuardianFormInvocation is not null || command.IndependentGuardian is not null || command.NativeLimitCallback is not null || command.NativeGraphic is not null)
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
        // TODO(TD-009): Ordinary zero-target and graphics-only invocations have separate admission contracts.
        if (contacts.IsDefault || contacts.IsEmpty && header is not { Command: 0, Sequence: 10, Action: 9, Effect: 0 })
        {
            throw new NotSupportedException("Hosted presentation requires an explicit contact list or its original failed control header.");
        }
        if (contacts.IsEmpty && enemyDraw is not null)
        {
            throw new InvalidDataException("A hosted control invocation cannot consume pending Draw without a contact.");
        }
        if (pendingGunblade && (contacts.Length != 1 || header.Command is not (1 or 12 or 28)))
        {
            throw new InvalidDataException("Hosted ordinary Gunblade requires one preliminary contact and its original command.");
        }
        expandedHostedInvocations = expandedHostedInvocations.Add(new(command, header, contacts, enemyDraw) { PendingGunblade = pendingGunblade });
    }

    private BattlePendingEnemyDraw? TakePendingHostedEnemyDraw()
    {
        BattlePendingEnemyDraw? draw = pendingEnemyDraw;
        pendingEnemyDraw = null;
        return draw;
    }

    private void ValidateExpandedHostedInvocations(NativeBattleGraphicsRoster roster)
    {
        if (!roster.Rows.SequenceEqual(ReadExpandedPresentationRoster().Rows))
        {
            throw new InvalidDataException("Hosted invocation roster differs from the logical allocations.");
        }
        foreach (ExpandedHostedInvocation invocation in expandedHostedInvocations)
        {
            BattleCombatant actor = GetActor(invocation.Command.ActorId);
            _ = GetActor(invocation.Command.TargetId);
            if (invocation.Header.Actor != RequireExpandedPresentationRow(roster, actor))
            {
                throw new InvalidDataException("Hosted invocation header does not identify its canonical caster.");
            }
            ValidateExpandedEffectContacts(actor, invocation.Contacts, roster);
        }
    }

    private static PreparedHostedAction PrepareExpandedHostedInvocation(ExpandedBattlePresentationProcess process, ExpandedHostedInvocation invocation)
    {
        return process.PrepareAction(invocation.Header, invocation.Contacts.Select(contact => contact.Packet).ToImmutableArray());
    }

    private static NativeBattleEffectContactBridge<string>? PrepareExpandedHostedContactBridge(ExpandedHostedInvocation invocation, NativeBattleGraphicsRoster roster)
    {
        return invocation.Contacts.IsEmpty ? null : new(invocation.Contacts.Select(contact => (contact.RecipientId, contact.Packet)), roster);
    }

    private void BindExpandedHostedContactTracking(ExpandedHostedInvocation invocation, NativeBattleGraphicsRoster roster, NativeBattleEffectContactBridge<string>? bridge)
    {
        expandedContactBridge = bridge;
        expandedModelContacts = invocation.Contacts;
        expandedModelRoster = roster;
        expandedEffectCommand = invocation.Command;
        expandedEffectEnemyDraw = invocation.EnemyDraw;
        expandedExpectedContacts = invocation.Contacts.Length;
        expandedPresentedOccurrences.Clear();
    }

    private void AdvanceExpandedHostedInvocation(BattleActionPresentation action, long token)
    {
        ExpandedBattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("A hosted action lost its attached process.");
        if (!process.IsActionComplete(token))
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
            if (expandedHostedInvocations[0].CommittedGunblade)
            {
                FlushExpandedEnemyReactions();
            }
            expandedHostedActionToken = null;
            expandedHostedInvocations = [];
            ReleaseExpandedEffect();
            CompleteExpandedHostedEventGauge(action);
            CompleteActionPresentation(action, "Expanded action completed.");
            return;
        }

        NativeBattleGraphicsRoster roster = process.Graphics.PresentationRoster!;
        ValidateExpandedHostedInvocations(roster);
        ImmutableArray<ExpandedHostedInvocation> remaining = expandedHostedInvocations.RemoveAt(0);
        ExpandedHostedInvocation invocation = remaining[0];
        PreparedHostedAction next = PrepareExpandedHostedInvocation(process, invocation);
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(invocation, roster);
        process.ValidatePreparedAction(next);
        expandedHostedInvocations = remaining;
        BindExpandedHostedContactTracking(invocation, roster, bridge);
        expandedHostedActionToken = process.PublishAction(next);
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
                    AcknowledgeExpandedEffectContact(command, contact.Packet);
                    NativeBattleGraphicsRoster roster = expandedModelRoster!;
                    PublishExpandedReadyContact(roster, contact.Packet.Primary, command.ActorId);
                    if (contact.Packet.Secondary.ActorIndex != byte.MaxValue)
                    {
                        PublishExpandedReadyContact(roster, contact.Packet.Secondary, command.ActorId);
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
                case NativeBattleModelParticipantReleased released:
                    AcknowledgeExpandedModelRelease(RequireExpandedLifecycleRoster(), released);
                    break;
                case NativeBattleModelParticipantSuspended suspended:
                    AcknowledgeExpandedModelSuspension(RequireExpandedLifecycleRoster(), suspended);
                    break;
                case NativeBattleModelTriggerRegistered trigger:
                    AcknowledgeExpandedGunbladeRegistration(trigger, actionToken);
                    break;
                default:
                    // TODO(TD-009): Finish the remaining hosted lifecycle acknowledgements before App attachment.
                    throw new NotSupportedException($"Hosted parent acknowledgement does not handle {receipt.GetType().Name}.");
            }
        }
    }
}
