namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal ushort? HostedTriggerOptions => definition.LimitProgress?.Options978;

    private bool CanTriggerExpandedGunblade(ExpandedBattlePresentationProcess process)
    {
        return Outcome == BattleOutcome.Active && GunbladeMode == BattleGunbladeMode.Manual &&
            expandedHostedActionToken is long token && process.ActiveActionToken == token &&
            expandedHostedInvocations is [{ PendingGunblade: true } invocation] &&
            process.TriggerWidget is { SourceMagicId: null } widget && widget.Actor == invocation.Header.Actor &&
            CanPressNativeTriggerWidget(widget, process.TriggerOptions);
    }

    internal void PressHostedGunblade(long token, byte actor, NativeBattleTriggerWidget widget)
    {
        if (!hostedBattlePresentation || hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation ||
            invocation.Token != token || NativeGraphics is not { PresentationRoster: { } roster } graphics ||
            !roster.ContainsRow(actor) || !roster[actor].IsParty || graphics.PendingTrigger is not null ||
            !ReferenceEquals(graphics.TriggerWidget, widget) || widget.Actor != actor || widget.SourceMagicId is not null ||
            !CanPressNativeTriggerWidget(widget, definition.LimitProgress?.Options978))
        {
            throw new InvalidOperationException("Gunblade input requires its active hosted widget.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[invocation.HeaderIndex];
        if (header.Source != actor || header.Command is not (1 or 12 or 28))
        {
            throw new InvalidDataException("Gunblade input does not belong to the active hosted header.");
        }
        NativeGraphics = graphics with { TriggerWidget = widget with { PendingTrigger = true } };
    }

    private void AcknowledgeExpandedGunbladeRegistration(NativeBattleModelTriggerRegistered receipt, long? actionToken)
    {
        ExpandedBattlePresentationProcess process = expandedPresentationProcess!;
        BattleModelRoster roster = process.Graphics.PresentationRoster!;
        if (actionToken is null || expandedHostedActionToken != actionToken || process.ActiveActionToken != actionToken ||
            expandedHostedInvocations is not [{ PendingGunblade: true } invocation] ||
            receipt.Row != invocation.Header.Actor || !roster.ContainsRow(receipt.Row) || !roster[receipt.Row].IsParty ||
            receipt.ActorId != roster[receipt.Row].ActorId || receipt.ActorId != invocation.Command.ActorId ||
            receipt.Widget is not { SourceMagicId: null } widget || widget.Actor != receipt.Row ||
            widget.Block.Timeline.InputThresholds.Length != 1 || process.TriggerWidget is not { } current ||
            current.Actor != widget.Actor || !ReferenceEquals(current.Block, widget.Block) ||
            current.SourceSequence != widget.SourceSequence || current.SourceAbOffset != widget.SourceAbOffset || current.SourceMagicId != widget.SourceMagicId)
        {
            // TODO(TD-009): Hosted limit widgets require their own logical registration owner.
            throw new NotSupportedException("Hosted trigger registration requires the current ordinary Gunblade invocation.");
        }
        nativeGuardianBoost = null;
    }

    private void DispatchExpandedGunbladeTrigger(ExpandedBattlePresentationProcess process)
    {
        if (process.PendingTrigger is not { } receipt)
        {
            return;
        }
        BattleModelRoster roster = process.Graphics.PresentationRoster!;
        receipt.Validate(roster);
        if (expandedHostedActionToken is not long token || process.ActiveActionToken != token ||
            expandedHostedInvocations is not [{ PendingGunblade: true, Contacts: [var previous] } invocation] ||
            receipt is not { ContactIndex: 1, TotalContacts: 1 } || receipt.Actor != invocation.Header.Actor ||
            invocation.Header.Command is not (1 or 12 or 28) || expandedExpectedContacts != 1 || expandedPresentedOccurrences.Count != 0 ||
            !ReferenceEquals(expandedModelRoster, roster) || !ReferenceEquals(expandedEffectCommand, invocation.Command) ||
            nativeReactionOrder is null)
        {
            throw new InvalidDataException("Hosted Gunblade receipt does not replace its unacknowledged ordinary placeholder.");
        }
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleCombatant recipient = GetActor(previous.RecipientId);
        if (RequireExpandedPresentationRow(roster, actor) != receipt.Actor ||
            RequireExpandedPresentationRow(roster, recipient) != previous.Packet.Primary.Recipient || recipient.NativeRemainingHits != 0)
        {
            throw new InvalidDataException("Hosted Gunblade placeholder lost its original caster or recipient.");
        }

        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.pendingEnemyDraw ??= invocation.EnemyDraw;
        NativeBattleContactPacket packet = candidate.ResolveExpandedGunbladeContact(invocation, previous, receipt);
        ExpandedHostedInvocation resolved = invocation with
        {
            Contacts = [new(previous.RecipientId, packet)], EnemyDraw = candidate.TakePendingHostedEnemyDraw(), PendingGunblade = false
        };
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(resolved, roster);
        var invocations = expandedHostedInvocations.SetItem(0, resolved);
        PreparedHostedContactReplacement replacement = process.PrepareGunbladeContact(token, packet, receipt);
        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = Restore(before, customHandlers);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        process.ValidateGunbladeContact(replacement);
        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        expandedHostedInvocations = invocations;
        expandedContactBridge = bridge;
        expandedModelContacts = resolved.Contacts;
        expandedEffectEnemyDraw = resolved.EnemyDraw;
        process.CommitGunbladeContact(replacement);
    }

    private NativeBattleContactPacket ResolveExpandedGunbladeContact(ExpandedHostedInvocation invocation, ExpandedBattleContact previous, NativeBattleTriggerReceipt receipt)
    {
        BattleCombatant actor = GetActor(invocation.Command.ActorId);
        BattleCombatant recipient = GetActor(previous.RecipientId);
        (BattleNativeAction native, short timing) = PrepareNativeTriggerHit(actor, recipient, invocation.Header.Command, receipt, hosted: true);
        BattleActionPresentation contact = invocation.Command with { NativeAction = native, GunbladeTriggerTiming = timing };
        BattleActionDefinition action = new(contact.Ability, contact.ActionId, native.Name ?? "Gunblade", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        resolvingNativeAction = contact;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        try
        {
            return ResolveInstalledNativeHitWithContact(actor, recipient, contact, action, native, 255, presentationRow: previous.Packet.Primary.Recipient);
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = false;
            resolvingNativeAction = null;
        }
    }
}
