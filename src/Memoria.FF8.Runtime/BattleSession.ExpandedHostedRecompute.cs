using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishExpandedHostedRecompute(ExpandedBattlePresentationProcess process, BattleModelRoster roster, long token, ExpandedHostedInvocation invocation, Func<BattleSession, ExpandedHostedInvocation> resolve, NativeBattleTriggerReceipt? trigger = null, HostedGuardianBoostRequest? guardian = null)
    {
        if (expandedHostedEvent is not { LogicallyComplete: false } hostedEvent || expandedHostedCurrentOperation is not int current || !ReferenceEquals(CurrentExpandedHostedInvocation, invocation) || invocation.HeaderReference is null)
        {
            throw new InvalidDataException("Hosted recompute requires its current admitted invocation and logical event.");
        }

        ImmutableArray<ExpandedHostedOperation> originalOperations = expandedHostedOperations;
        int next = expandedHostedNextOperation;
        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        BattleSession candidate = Restore(before, customHandlers);
        candidate.expandedPartyPresentation = expandedPartyPresentation;
        candidate.nativeAffinityAction = nativeAffinityAction;
        candidate.preparingExpandedHostedAction = true;
        candidate.producingExpandedAiCallback = true;
        ExpandedAiInvocationReservation reservation = new(invocation.Header.Source, invocation.Command);
        candidate.producingExpandedAiInvocation = reservation;
        // Deferred counters must wait for the resolved source, even though the child already owns its preliminary packets.
        candidate.expandedHostedOperations = [invocation with { Contacts = default, Reservation = reservation }];
        ExpandedHostedInvocation resolved = resolve(candidate);
        if (!ReferenceEquals(resolved.Command, invocation.Command) || resolved.Header != invocation.Header ||
            resolved.HeaderReference != invocation.HeaderReference || resolved.FirstContact != invocation.FirstContact ||
            resolved.Contacts.IsDefault || resolved.Contacts.Length != invocation.Contacts.Length ||
            resolved.PendingGunblade || resolved.PendingGuardian)
        {
            throw new InvalidDataException("Hosted recompute must resolve the complete existing invocation without changing its owners.");
        }
        candidate.expandedHostedOperations = candidate.expandedHostedOperations.SetItem(0, resolved with { Reservation = reservation });
        candidate.FlushEncounterCounterCommands();
        if (candidate.encounterCounterCommands.Count != 0)
        {
            throw new InvalidDataException("Hosted recompute did not produce all of its captured counters.");
        }

        candidate.ValidateExpandedHostedInvocations(roster);
        ImmutableArray<ExpandedHostedOperation> suffix = candidate.expandedHostedOperations.RemoveAt(0);
        ImmutableArray<HostedDispatchHeader> headers = suffix.OfType<ExpandedHostedInvocation>().Select(write => new HostedDispatchHeader(write.Header, write.Contacts.Select(contact => contact.Packet).ToImmutableArray())).ToImmutableArray();
        PreparedHostedContactReplacement replacement = process.PrepareRecomputedContacts(token, resolved.Contacts.Select(contact => contact.Packet).ToImmutableArray(), headers, trigger, guardian);
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(resolved, roster);
        ImmutableArray<ExpandedHostedOperation> updated = BindExpandedRecomputeSuffix(originalOperations.SetItem(current, resolved), suffix, replacement, roster);

        BattleSnapshot prepared = candidate.CaptureState();
        BattleSession preflight = Restore(before, customHandlers);
        preflight.expandedPartyPresentation = expandedPartyPresentation;
        preflight.CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        preflight.ValidatePreparedExpandedPresentation(candidate);
        if (expandedHostedOperations != originalOperations || expandedHostedCurrentOperation != current || expandedHostedNextOperation != next ||
            !ReferenceEquals(expandedHostedEvent, hostedEvent) || expandedHostedActionToken != token || expandedPresentedOccurrences.Count != 0)
        {
            throw new InvalidOperationException("The hosted recompute source changed before publication.");
        }
        process.ValidatePreparedRecomputedContacts(replacement);

        CopyCandidateState(candidate, prepared, preparedExpandedAction: true);
        expandedHostedOperations = updated;
        expandedContactBridge = bridge;
        expandedModelContacts = resolved.Contacts;
        expandedEffectEnemyDraw = resolved.EnemyDraw;
        process.CommitRecomputedContacts(replacement);
    }

    private static ImmutableArray<ExpandedHostedOperation> BindExpandedRecomputeSuffix(ImmutableArray<ExpandedHostedOperation> operations, ImmutableArray<ExpandedHostedOperation> suffix, PreparedHostedContactReplacement prepared, BattleModelRoster roster)
    {
        var result = operations.ToBuilder();
        int appended = 0;
        foreach (ExpandedHostedOperation operation in suffix)
        {
            switch (operation)
            {
                case ExpandedHostedInvocation { HeaderReference: null } invocation when appended < prepared.AppendedHeaders.Length:
                    BattleActionHeaderReference reference = prepared.AppendedHeaders[appended++];
                    result.Add(invocation with { HeaderReference = reference, FirstContact = prepared.ReplacementPackets.ReadHeader(reference).FirstContact });
                    break;
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.Model model } when model.Command is 105 or 106 or 113 && roster.Contains(model.Owner):
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.Barrier or HostedDispatchControl.ResetCounts or HostedDispatchControl.Message }:
                    result.Add(operation);
                    break;
                default:
                    throw new InvalidDataException("Hosted recompute produced a suffix without its original operation and owner.");
            }
        }
        if (appended != prepared.AppendedHeaders.Length)
        {
            throw new InvalidDataException("Hosted recompute produced a different number of headers and suffix actions.");
        }
        return result.ToImmutable();
    }
}
