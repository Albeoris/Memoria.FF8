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
            invocation.Header.Source != request.Caster || invocation.Contacts.Length != expandedInvocationProgress?.ExpectedContacts ||
            expandedInvocationProgress?.Occurrences.Count != 0 || expandedInvocationProgress?.Roster?.Contains(request.Caster) != true ||
            !ReferenceEquals(expandedInvocationProgress?.Command, invocation.Command) || nativeReactionOrder is null)
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

        PublishExpandedHostedRecompute(process, roster, token, invocation, candidate => invocation with
        {
            Contacts = candidate.ResolveExpandedGuardianContacts(invocation, request.Percent),
            PendingGuardian = false
        }, guardian: request);
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
