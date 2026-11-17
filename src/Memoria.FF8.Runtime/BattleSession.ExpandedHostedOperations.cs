using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record ExpandedHostedPrefix(PreparedHostedDispatch Dispatch, ImmutableArray<ExpandedHostedOperation> Operations, int Next, int? Current, NativeBattleEffectContactBridge<string>? Bridge);

    private void StageExpandedHostedControl(HostedDispatchControl control)
    {
        if (control is not HostedDispatchControl.Complete)
        {
            StopTurnAdvance();
        }
        expandedOperationCursor.Operations = expandedOperationCursor.Operations.Add(new ExpandedHostedDescriptor(control));
    }

    private void StageExpandedHostedModelControl(ushort command, BattleCombatant actor)
    {
        StageExpandedHostedControl(new HostedDispatchControl.Model(command, ReadPresentationModelOwner(actor.Id)));
    }

    private void StageExpandedHostedCompletion(BattleActionPresentation command, uint callback, BattleModelOwner owner, bool capturedBeforeAi)
    {
        StageExpandedHostedControl(new HostedDispatchControl.Complete(new(callback, owner, command.ActorId, capturedBeforeAi)));
    }

    private ExpandedHostedPrefix PrepareExpandedHostedPrefix(BattlePresentationProcess process,
        PreparedHostedCompletionAck? acknowledgment = null, bool acknowledgeOnly = false, ImmutableArray<HostedModelLoadAdmission> allocations = default, NativeBattleDuelSelectionReached? duelSelection = null,
        HostedRenzokukenFinisherReplacement? renzokukenFinisher = null, PreparedHostedGuardianFormChange? guardianForm = null)
    {
        var headers = ImmutableArray.CreateBuilder<HostedDispatchHeader>();
        int firstUnpublished = -1;
        for (int i = 0; i < expandedOperationCursor.Operations.Length; i++)
        {
            if (expandedOperationCursor.Operations[i] is ExpandedHostedInvocation { HeaderReference: null } invocation)
            {
                if (firstUnpublished < 0)
                {
                    firstUnpublished = i;
                }
                headers.Add(new(invocation.Header, invocation.Contacts.Select(contact => contact.Packet).ToImmutableArray(), checked((byte)(invocation.FollowingCasts.Length + 1))));
                foreach (ExpandedHostedInvocation cast in invocation.FollowingCasts)
                {
                    headers.Add(new(cast.Header, cast.Contacts.Select(contact => contact.Packet).ToImmutableArray()));
                }
            }
        }
        var before = ImmutableArray.CreateBuilder<HostedDispatchControl>();
        var after = ImmutableArray.CreateBuilder<HostedDispatchControl>();
        int? current = null;
        int next = expandedOperationCursor.Next;
        bool completionBeforeAction = false;
        if (!acknowledgeOnly)
        {
            for (; next < expandedOperationCursor.Operations.Length; next++)
            {
                switch (expandedOperationCursor.Operations[next])
                {
                    case ExpandedHostedInvocation when current.HasValue || completionBeforeAction:
                        goto PrefixReady;
                    case ExpandedHostedInvocation:
                        current = next;
                        break;
                    case ExpandedHostedDescriptor descriptor:
                        (current.HasValue ? after : before).Add(descriptor.Control);
                        if (descriptor.Control is HostedDispatchControl.RenzokukenFinisher)
                        {
                            next++;
                            goto PrefixReady;
                        }
                        completionBeforeAction |= !current.HasValue && descriptor.Control is HostedDispatchControl.Complete;
                        break;
                }
            }
        }
    PrefixReady:
        bool dispatchAppended = current is int selected && selected == firstUnpublished;
        BattleActionHeaderReference? reference = current is int index ? ((ExpandedHostedInvocation)expandedOperationCursor.Operations[index]).HeaderReference : null;
        if (current.HasValue && reference is null && !dispatchAppended)
        {
            throw new InvalidDataException("Hosted action lost its retained header before dispatch.");
        }
        HostedDispatchControls? controls = acknowledgeOnly ? null : new(before.ToImmutable(), after.ToImmutable());
        PreparedHostedDispatch dispatch = process.PrepareDispatch(headers.ToImmutable(), headerReference: reference, dispatchAppended: dispatchAppended,
            acknowledgment: acknowledgment, controls: controls, publishEventHeaders: true, allocations: allocations, duelSelection: duelSelection, renzokukenFinisher: renzokukenFinisher, guardianForm: guardianForm);
        ImmutableArray<ExpandedHostedOperation> operations = expandedOperationCursor.Operations;
        int appended = 0;
        for (int i = 0; i < expandedOperationCursor.Operations.Length; i++)
        {
            if (expandedOperationCursor.Operations[i] is ExpandedHostedInvocation { HeaderReference: null } invocation)
            {
                BattleActionHeaderReference header = dispatch.AppendedHeaders[appended];
                appended += 1 + invocation.FollowingCasts.Length;
                operations = operations.SetItem(i, invocation with
                {
                    HeaderReference = header,
                    FirstContact = dispatch.Packets.ReadHeader(header).FirstContact
                });
            }
        }
        NativeBattleEffectContactBridge<string>? bridge = current is int operation
            ? PrepareExpandedHostedContactBridge((ExpandedHostedInvocation)operations[operation], dispatch.Packets.Roster)
            : null;
        return new(dispatch, operations, next, current, bridge);
    }

    private void BindExpandedHostedPrefix(ExpandedHostedPrefix prefix, BattleModelRoster roster)
    {
        expandedOperationCursor = new(prefix.Operations, prefix.Next, prefix.Current);
        expandedInvocationProgress = null;
        if (CurrentExpandedHostedInvocation is { } action)
        {
            BindExpandedHostedContactTracking(action, roster, prefix.Bridge);
        }
    }

    private void AdvanceExpandedHostedInvocation(long? frameToken)
    {
        if (expandedHostedEvent is not { } hostedEvent)
        {
            return;
        }
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("A hosted event lost its attached process.");
        if (process.PendingRenzokukenFinisher is not null)
        {
            return;
        }
        if (expandedHostedActionToken is long token)
        {
            if ((frameToken is long current && current != token) || !process.IsActionComplete(token))
            {
                return;
            }
            if (CurrentExpandedHostedInvocation is not { PendingGunblade: false, PendingGuardian: false })
            {
                throw new InvalidDataException("Hosted action retired with unresolved preliminary contacts.");
            }
            ValidateExpandedEffectContactCompletion();
            ValidateExpandedHostedRenzokukenInitialRetirement(hostedEvent);
        }
        if (!process.IsControlPrefixRetired)
        {
            return;
        }
        if (hostedEvent.CompletionToken is long completion)
        {
            if (!process.IsCompletionQueued(completion))
            {
                throw new InvalidDataException("Hosted event lost its scheduled native completion.");
            }
            return;
        }
        if (expandedOperationCursor.Next == expandedOperationCursor.Operations.Length)
        {
            if (hostedEvent.DuelPoll is { } poll && ReferenceEquals(process.PendingDuelSelection?.Frame.Poll, poll))
            {
                return;
            }
            if (!hostedEvent.LogicallyComplete)
            {
                throw new InvalidDataException("Hosted event retired without reaching native combat completion.");
            }
            ReleaseExpandedHostedFence();
            return;
        }


        ExpandedHostedPrefix prefix = PrepareExpandedHostedPrefix(process);
        PreparedHostedDispatch next = prefix.Dispatch;
        process.ValidatePreparedDispatch(next);
        BindExpandedHostedPrefix(prefix, next.Packets.Roster);
        expandedHostedEvent = hostedEvent with { CompletionToken = next.Completion?.Request.Token, Callback = next.Completion?.Request.Callback ?? hostedEvent.Callback };
        expandedHostedActionToken = next.Action?.Invocation.Token;
        process.CommitDispatch(next);
    }

    private void ReleaseExpandedHostedFence()
    {
        diagnosticReflectedCasts?.Clear();
        diagnosticReflectionSources?.Clear();
        if (ReferenceEquals(ActiveAction, expandedHostedEvent?.ActivePresentation))
        {
            ActiveAction = null;
        }
        expandedOperationCursor = new([]);
        expandedHostedActionToken = null;
        expandedHostedEvent = null;
        expandedInvocationProgress = null;
    }
}
