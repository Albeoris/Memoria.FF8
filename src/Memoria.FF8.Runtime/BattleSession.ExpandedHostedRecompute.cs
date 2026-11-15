using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void PublishExpandedHostedRecompute(ExpandedBattlePresentationProcess process, BattleModelRoster roster, long token, ExpandedHostedInvocation invocation, Func<BattleSession, ExpandedHostedInvocation> resolve, NativeBattleTriggerReceipt? trigger = null, HostedGuardianBoostRequest? guardian = null)
    {
        if (expandedHostedEvent is not { LogicallyComplete: false } hostedEvent || expandedOperationCursor.Current is not int current || !ReferenceEquals(CurrentExpandedHostedInvocation, invocation) || invocation.HeaderReference is null)
        {
            throw new InvalidDataException("Hosted recompute requires its current admitted invocation and logical event.");
        }

        ImmutableArray<ExpandedHostedOperation> originalOperations = expandedOperationCursor.Operations;
        int next = expandedOperationCursor.Next;
        ExpandedInvocationProgress progress = expandedInvocationProgress ?? throw new InvalidDataException("Hosted recompute has no published contact owner.");
        int acknowledged = progress.Occurrences.Count;
        BattleSnapshot before = Capture();
        IBattleStatusEffectHandler[] customHandlers = handlers.Values.Where(handler => handler is not ScaffoldBattleEffectHandler).ToArray();
        HostedRenzokukenCandidateProof? renzokuken = hostedEvent.Renzokuken is not null && invocation.Header.Command == 5 && trigger is not null
            ? new(before, hostedEvent, trigger, null)
            : null;
        BattleSession candidate = CreatePreparedHostedActionCandidate(before, customHandlers, renzokuken);
        if (renzokuken is not null)
        {
            candidate.expandedHostedEvent = hostedEvent;
        }
        candidate.producingExpandedAiCallback = true;
        ExpandedAiInvocationReservation reservation = new(invocation.Header.Source, invocation.Command);
        candidate.producingExpandedAiInvocation = reservation;
        // Deferred counters must wait for the resolved source, even though the child already owns its preliminary packets.
        candidate.expandedOperationCursor.Operations = [invocation with { Contacts = default, Reservation = reservation }];
        ExpandedHostedInvocation resolved = resolve(candidate);
        if (!ReferenceEquals(resolved.Command, invocation.Command) || resolved.Header != invocation.Header ||
            resolved.HeaderReference != invocation.HeaderReference || resolved.FirstContact != invocation.FirstContact ||
            resolved.Contacts.IsDefault || resolved.Contacts.Length != invocation.Contacts.Length ||
            resolved.PendingGunblade || resolved.PendingGuardian)
        {
            throw new InvalidDataException("Hosted recompute must resolve the complete existing invocation without changing its owners.");
        }
        candidate.expandedOperationCursor.Operations = candidate.expandedOperationCursor.Operations.SetItem(0, resolved with { Reservation = reservation });
        candidate.FlushEncounterCounterCommands();
        if (candidate.encounterCounterCommands.Count != 0)
        {
            throw new InvalidDataException("Hosted recompute did not produce all of its captured counters.");
        }

        ImmutableArray<HostedModelLoadAdmission> allocations = candidate.PrepareExpandedModelLoadAdmissions(process);
        BattleModelRoster planned = allocations.IsEmpty ? process.PlannedModelRoster : new(process.PlannedModelRoster.Owners.Concat(allocations.Select(plan => plan.Load.Owner)));
        candidate.ValidateExpandedHostedInvocations(planned);
        ImmutableArray<ExpandedHostedOperation> suffix = candidate.expandedOperationCursor.Operations.RemoveAt(0);
        ImmutableArray<HostedDispatchHeader> headers = suffix.OfType<ExpandedHostedInvocation>().Select(write => new HostedDispatchHeader(write.Header, write.Contacts.Select(contact => contact.Packet).ToImmutableArray())).ToImmutableArray();
        PreparedHostedGuardianFormChange? form = PrepareStagedHostedGuardianForm(process, candidate);
        PreparedHostedContactReplacement replacement = process.PrepareRecomputedContacts(token, resolved.Contacts.Select(contact => contact.Packet).ToImmutableArray(), headers, trigger, guardian, allocations, form);
        NativeBattleEffectContactBridge<string>? bridge = PrepareExpandedHostedContactBridge(resolved, roster);
        ImmutableArray<ExpandedHostedOperation> updated = BindExpandedRecomputeSuffix(originalOperations.SetItem(current, resolved), suffix, replacement, replacement.ReplacementPackets.Roster);

        PreparedActionPublication publication = new(this, candidate, RestoreSnapshot(before, customHandlers, renzokuken));
        if (expandedOperationCursor.Operations != originalOperations || expandedOperationCursor.Current != current || expandedOperationCursor.Next != next ||
            !ReferenceEquals(expandedHostedEvent, hostedEvent) || expandedHostedActionToken != token ||
            !ReferenceEquals(expandedInvocationProgress, progress) || progress.Occurrences.Count != (renzokuken is null ? 0 : acknowledged))
        {
            throw new InvalidOperationException("The hosted recompute source changed before publication.");
        }
        process.ValidatePreparedRecomputedContacts(replacement);

        publication.Publish();
        expandedOperationCursor.Operations = updated;
        progress.Bridge = bridge;
        progress.EnemyDraw = resolved.EnemyDraw;
        if (renzokuken is not null)
        {
            progress.Occurrences.Clear();
        }
        process.CommitRecomputedContacts(replacement);
        PublishHostedGuardianFormAudio(form);
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
                case ExpandedHostedDescriptor { Control: HostedDispatchControl.LoadEnemy or HostedDispatchControl.AllocationStatistics or HostedDispatchControl.ActivateOriginal or HostedDispatchControl.AllocationGate }:
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
