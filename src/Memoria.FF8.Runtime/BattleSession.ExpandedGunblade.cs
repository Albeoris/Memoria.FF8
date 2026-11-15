namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal ushort? HostedTriggerOptions => definition.LimitProgress?.Options978;

    private bool CanTriggerExpandedGunblade(ExpandedBattlePresentationProcess process)
    {
        return Outcome == BattleOutcome.Active && GunbladeMode == BattleGunbladeMode.Manual &&
            expandedHostedActionToken is long token && process.ActiveActionToken == token &&
            CurrentExpandedHostedInvocation is { PendingGunblade: true } invocation &&
            process.TriggerWidget is { SourceMagicId: null } widget && widget.Actor == invocation.Header.Source &&
            CanPressNativeTriggerWidget(widget, process.TriggerOptions);
    }

    internal void PressHostedGunblade(long token, BattleModelOwner actor, NativeBattleTriggerWidget widget)
    {
        if (!hostedBattlePresentation || hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation ||
            invocation.Token != token || NativeGraphics is not { } graphics ||
            actor is not BattlePartyModelOwner || !graphics.ActorModels.Contains(actor) || graphics.PendingTrigger is not null ||
            !ReferenceEquals(graphics.TriggerWidget, widget) || widget.Actor != actor || widget.SourceMagicId is not null ||
            !CanPressNativeTriggerWidget(widget, definition.LimitProgress?.Options978))
        {
            throw new InvalidOperationException("Gunblade input requires its active hosted widget.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(invocation.Header);
        if (header.Source != actor || header.Command is not (1 or 12 or 28))
        {
            throw new InvalidDataException("Gunblade input does not belong to the active hosted header.");
        }
        NativeGraphics = graphics with { TriggerWidget = widget with { PendingTrigger = true } };
    }

    private void AcknowledgeExpandedGunbladeRegistration(NativeBattleModelTriggerRegistered receipt, long? actionToken)
    {
        ExpandedBattlePresentationProcess process = expandedPresentationProcess!;
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
        nativeGuardianBoost = null;
    }

    private void DispatchExpandedGunbladeTrigger(ExpandedBattlePresentationProcess process)
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
            invocation.Header.Command is not (1 or 12 or 28) || expandedExpectedContacts != 1 || expandedPresentedOccurrences.Count != 0 ||
            expandedModelRoster?.Contains(receipt.Actor) != true || !ReferenceEquals(expandedEffectCommand, invocation.Command) ||
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

        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.pendingEnemyDraw ??= invocation.EnemyDraw;
        candidate.preparingExpandedHostedAction = true;
        candidate.producingExpandedAiCallback = true;
        candidate.producingExpandedAiInvocation = new(invocation.Header.Source, invocation.Command);
        NativeBattleContactPacket packet = candidate.ResolveExpandedGunbladeContact(invocation, previous, receipt);
        if (candidate.HasStagedExpandedHostedOperations || candidate.encounterCounterCommands.Count != 0)
        {
            // TODO(TD-009): Combine deferred contact replacement with publication of its nested suffix after the queued descriptors.
            throw new NotSupportedException("Hosted Gunblade recompute emitted nested commands that require an atomic event extension.");
        }
        ExpandedHostedInvocation resolved = invocation with
        {
            Contacts = [new(previous.RecipientId, packet)], EnemyDraw = candidate.TakePendingHostedEnemyDraw(), PendingGunblade = false
        };
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(resolved, roster);
        PreparedHostedContactReplacement replacement = process.PrepareGunbladeContact(token, packet, receipt);
        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = Restore(before, customHandlers);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        process.ValidateGunbladeContact(replacement);
        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        ReplaceCurrentExpandedHostedInvocation(invocation, resolved, bridge);
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
