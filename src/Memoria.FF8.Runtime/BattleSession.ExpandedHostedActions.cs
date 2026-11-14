using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record ExpandedHostedInvocation(BattleActionPresentation Command, NativeBattleActionHeaderPreparation Header, ImmutableArray<ExpandedBattleContact> Contacts, BattlePendingEnemyDraw? EnemyDraw);

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
        if (!preparingExpandedHostedAction)
        {
            throw new InvalidOperationException("Hosted invocation staging requires the logical preparation candidate.");
        }
        // TODO(TD-009): Adapt failure-only header ownership before enabling the attached host in App.
        if (header.Command == 0 || contacts.IsEmpty)
        {
            throw new NotSupportedException("Hosted presentation has no admission for this invocation without resolved contacts.");
        }
        expandedHostedInvocations = expandedHostedInvocations.Add(new(command, header, contacts, enemyDraw));
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

    private static NativeBattleEffectContactBridge<string> PrepareExpandedHostedContactBridge(ExpandedHostedInvocation invocation, NativeBattleGraphicsRoster roster)
    {
        return new(invocation.Contacts.Select(contact => (contact.RecipientId, contact.Packet)), roster);
    }

    private void BindExpandedHostedContactTracking(ExpandedHostedInvocation invocation, NativeBattleGraphicsRoster roster, NativeBattleEffectContactBridge<string> bridge)
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
        ValidateExpandedEffectContactCompletion();
        if (expandedHostedInvocations.Length == 1)
        {
            expandedHostedActionToken = null;
            expandedHostedInvocations = [];
            ReleaseExpandedEffect();
            CompleteActionPresentation(action, "Expanded action completed.");
            return;
        }

        NativeBattleGraphicsRoster roster = process.Graphics.PresentationRoster!;
        ValidateExpandedHostedInvocations(roster);
        ImmutableArray<ExpandedHostedInvocation> remaining = expandedHostedInvocations.RemoveAt(0);
        ExpandedHostedInvocation invocation = remaining[0];
        PreparedHostedAction next = PrepareExpandedHostedInvocation(process, invocation);
        NativeBattleEffectContactBridge<string> bridge = PrepareExpandedHostedContactBridge(invocation, roster);
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
                    AcknowledgeExpandedEffectContact(command, contact.Packet);
                    NativeBattleGraphicsRoster roster = expandedModelRoster!;
                    PublishExpandedPartyPresentedContact(roster, contact.Packet.Primary);
                    if (contact.Packet.Secondary.ActorIndex != byte.MaxValue)
                    {
                        PublishExpandedPartyPresentedContact(roster, contact.Packet.Secondary);
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
                default:
                    // TODO(TD-009): Implement parent trigger acknowledgement before App attachment.
                    throw new NotSupportedException($"Hosted parent acknowledgement does not handle {receipt.GetType().Name}.");
            }
        }
    }
}
