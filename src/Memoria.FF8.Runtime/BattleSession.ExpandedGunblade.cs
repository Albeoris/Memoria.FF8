namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{

    private bool CanTriggerExpandedGunblade(BattlePresentationProcess process)
    {
        if (expandedHostedEvent?.Renzokuken is not null)
        {
            return CanTriggerExpandedHostedRenzokuken(process);
        }
        return Outcome == BattleOutcome.Active && GunbladeMode == BattleGunbladeMode.Manual &&
            expandedHostedActionToken is long token && process.ActiveActionToken == token &&
            CurrentExpandedHostedInvocation is { PendingGunblade: true } invocation &&
            process.TriggerWidget is { SourceMagicId: null } widget && widget.Actor == invocation.Header.Source &&
            BattlePresentationProcess.CanPressNativeTriggerWidget(widget, process.TriggerOptions);
    }

    private void AcknowledgeExpandedGunbladeRegistration(NativeBattleModelTriggerRegistered receipt, long? actionToken)
    {
        BattlePresentationProcess process = expandedPresentationProcess!;
        if (receipt.Widget.SourceMagicId is not null && expandedHostedEvent?.Renzokuken is not null)
        {
            AcknowledgeExpandedHostedRenzokukenRegistration(process, receipt, actionToken);
            return;
        }
        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        if (actionToken is null || expandedHostedActionToken != actionToken || process.ActiveActionToken != actionToken ||
            CurrentExpandedHostedInvocation is not { PendingGunblade: true } invocation ||
            receipt.Owner != invocation.Header.Source || !roster.Contains(receipt.Owner) || receipt.Owner is not BattlePartyModelOwner ||
            receipt.ActorId != RequireCanonicalGraphicsActorId(receipt.Owner) || receipt.ActorId != invocation.Command.ActorId ||
            receipt.Widget is not { SourceMagicId: null } widget || widget.Actor != receipt.Owner ||
            widget.Block.Timeline.InputThresholds.Length != 1 || process.TriggerWidget is not { } current ||
            current.Actor != widget.Actor || !ReferenceEquals(current.Block, widget.Block) ||
            current.SourceSequence != widget.SourceSequence || current.SourceAbOffset != widget.SourceAbOffset || current.SourceMagicId != widget.SourceMagicId)
        {
            // TODO(TD-009): Hosted limit widgets require their own logical registration owner.
            throw new NotSupportedException("Hosted trigger registration requires the current ordinary Gunblade invocation.");
        }
    }

    private void DispatchExpandedGunbladeTrigger(BattlePresentationProcess process)
    {
        if (process.PendingTrigger is not { } receipt)
        {
            return;
        }
        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        receipt.Validate(roster);
        if (expandedHostedActionToken is not long token || process.ActiveActionToken != token ||
            CurrentExpandedHostedInvocation is not { PendingGunblade: true, Contacts: [var previous] } invocation ||
            receipt is not { ContactIndex: 1, TotalContacts: 1 } || receipt.Actor != invocation.Header.Source ||
            invocation.Header.Command is not (1 or 12 or 28) || expandedInvocationProgress?.ExpectedContacts != 1 || expandedInvocationProgress?.Occurrences.Count != 0 ||
            expandedInvocationProgress?.Roster?.Contains(receipt.Actor) != true || !ReferenceEquals(expandedInvocationProgress?.Command, invocation.Command) ||
            nativeReactionOrder is null)
        {
            throw new InvalidDataException("Hosted Gunblade receipt does not replace its unacknowledged ordinary placeholder.");
        }
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleCombatant recipient = GetActor(previous.RecipientId);
        if (RequirePresentationModelOwner(roster, actor) != receipt.Actor ||
            RequirePresentationModelOwner(roster, recipient) != previous.Packet.Primary.Recipient || recipient.NativeRemainingHits != 0)
        {
            throw new InvalidDataException("Hosted Gunblade placeholder lost its original caster or recipient.");
        }

        PublishExpandedHostedRecompute(process, roster, token, invocation, candidate =>
        {
            candidate.pendingEnemyDraw ??= invocation.EnemyDraw;
            NativeBattleContactPacket packet = candidate.ResolveExpandedGunbladeContact(invocation, previous, receipt);
            return invocation with { Contacts = [new(previous.RecipientId, packet)], EnemyDraw = candidate.TakePendingHostedEnemyDraw(), PendingGunblade = false };
        }, trigger: receipt);
    }

    private NativeBattleContactPacket ResolveExpandedGunbladeContact(ExpandedHostedInvocation invocation, ExpandedBattleContact previous, NativeBattleTriggerReceipt receipt)
    {
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleCombatant recipient = GetActor(previous.RecipientId);
        (BattleNativeAction native, short timing) = PrepareNativeTriggerHit(actor, recipient, invocation.Header.Command, receipt);
        BattleActionPresentation contact = invocation.Command with { NativeAction = native, GunbladeTriggerTiming = timing };
        BattleActionDefinition action = new(contact.Ability, contact.ActionId, native.Name ?? "Gunblade", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        resolvingNativeAction = contact;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        try
        {
            return ResolveInstalledNativeHitWithContact(actor, recipient, contact, action, native);
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = false;
            resolvingNativeAction = null;
        }
    }
}
