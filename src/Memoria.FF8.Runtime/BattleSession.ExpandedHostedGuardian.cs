using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DispatchExpandedGuardianBoost(ExpandedBattlePresentationProcess process)
    {
        if (process.PendingGuardianBoost is not { } request)
        {
            return;
        }
        BattleModelRoster roster = process.Graphics.ActorModels.Roster;
        if (expandedHostedActionToken is not long token || token != request.Token || process.ActiveActionToken != token ||
            CurrentExpandedHostedInvocation is not { PendingGuardian: true } invocation ||
            invocation.Header is not { Command: 254 } || invocation.HeaderReference != request.Header ||
            invocation.Header.Source != request.Caster || invocation.Contacts.Length != expandedExpectedContacts ||
            expandedPresentedOccurrences.Count != 0 || expandedModelRoster?.Contains(request.Caster) != true ||
            !ReferenceEquals(expandedEffectCommand, invocation.Command) || nativeReactionOrder is null)
        {
            throw new InvalidDataException("Hosted Boost requires its active Guardian Force invocation and unacknowledged preliminary contacts.");
        }
        BattleCombatant caster = GetActor(invocation.Command.ActorId);
        if (RequirePresentationModelOwner(roster, caster) != request.Caster)
        {
            throw new InvalidDataException("Hosted Boost lost its original caster.");
        }
        foreach (ExpandedBattleContact contact in invocation.Contacts)
        {
            if (RequirePresentationModelOwner(roster, GetActor(contact.RecipientId)) != contact.Packet.Primary.Recipient)
            {
                throw new InvalidDataException("Hosted Boost lost an admitted recipient.");
            }
        }

        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.nativeAffinityAction = nativeAffinityAction;
        candidate.preparingExpandedHostedAction = true;
        candidate.producingExpandedAiCallback = true;
        ImmutableArray<ExpandedBattleContact> contacts = candidate.ResolveExpandedGuardianContacts(invocation, request.Percent);
        if (candidate.HasStagedExpandedHostedOperations)
        {
            // TODO(TD-009): Append nested stage4 graphics after the already queued action and completion descriptors.
            throw new NotSupportedException("Hosted Guardian Force recompute emitted a nested callback that requires the live event stream.");
        }
        ExpandedHostedInvocation resolved = invocation with { Contacts = contacts, PendingGuardian = false };
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(resolved, roster);
        PreparedHostedGuardianContacts replacement = process.PrepareGuardianContacts(request, contacts.Select(contact => contact.Packet).ToImmutableArray());
        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = Restore(before, customHandlers);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);

        process.ValidatePreparedGuardianContacts(replacement);
        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        ReplaceCurrentExpandedHostedInvocation(invocation, resolved, bridge);
        process.CommitGuardianContacts(replacement);
    }

    private ImmutableArray<ExpandedBattleContact> ResolveExpandedGuardianContacts(ExpandedHostedInvocation invocation, byte percent)
    {
        ushort actionId = invocation.Header.Action;
        BattleNativeAction native = FindInstalledNativeAction(254, actionId);
        ValidateNativeActionContract(native);
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, actionId - 64);
        BattleActionPresentation command = invocation.Command with { NativeAction = native };
        BattleCombatant caster = GetActor(command.ActorId);

        //4850A0 reads the admitted packets in order. An occurrence keeps its recipient even if prior contacts change eligibility.
        BattleCombatant[] recipients = invocation.Contacts.Select(contact => GetActor(contact.RecipientId)).ToArray();
        nativeActionScratch = nativeActionScratch with { Command = 254, Action = actionId, GuardianFormulaE1E = true, DeferredEffectsE00 = false, GuardianBoostPercent = percent };
        resolvingNativeAction = command;
        deferringExpandedImpacts = true;
        nativeActionCommitDepth++;
        try
        {
            var contacts = ImmutableArray.CreateBuilder<ExpandedBattleContact>(recipients.Length);
            foreach (BattleCombatant recipient in recipients)
            {
                NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(caster, recipient, command, action, native);
                contacts.Add(new(recipient.Id, packet));
            }
            return contacts.MoveToImmutable();
        }
        finally
        {
            nativeActionCommitDepth--;
            deferringExpandedImpacts = false;
            resolvingNativeAction = null;
        }
    }
}
