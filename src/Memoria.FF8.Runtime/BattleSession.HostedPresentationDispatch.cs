using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedDispatchHeader(NativeBattleActionHeaderPreparation Header, ImmutableArray<NativeBattleContactPacket> Contacts);
    internal sealed record HostedDispatchCompletion(uint Callback, BattleModelOwner Owner, string ActorId);
    internal sealed record HostedDispatchAction(HostedPresentationInvocation Invocation, BattleModelOwner SourceOwner, NativeBattleGraphicsResource Source, byte? ModelId, bool IsParty, int PartyActorCount);

    internal sealed class PreparedHostedDispatch(BattleSession owner, NativeBattleGraphicsState graphics, NativeBattleActionPacketsState originalPackets, long epoch, long nextEpoch, long issuedAction, long issuedCompletion, NativeBattleGraphicsState admitted, NativeBattleActionPacketsState packets, HostedDispatchAction? action, HostedCompletionInvocation? completion, PreparedHostedCompletionAck? acknowledgment, ImmutableArray<BattleActionHeaderReference> appendedHeaders)
    {
        internal BattleSession Owner { get; } = owner;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal NativeBattleActionPacketsState OriginalPackets { get; } = originalPackets;
        internal long Epoch { get; } = epoch;
        internal long NextEpoch { get; } = nextEpoch;
        internal long IssuedAction { get; } = issuedAction;
        internal long IssuedCompletion { get; } = issuedCompletion;
        internal NativeBattleGraphicsState Admitted { get; } = admitted;
        internal NativeBattleActionPacketsState Packets { get; } = packets;
        internal HostedDispatchAction? Action { get; } = action;
        internal HostedCompletionInvocation? Completion { get; } = completion;
        internal PreparedHostedCompletionAck? Acknowledgment { get; } = acknowledgment;
        internal ImmutableArray<BattleActionHeaderReference> AppendedHeaders { get; } = appendedHeaders;
    }

    internal PreparedHostedDispatch PrepareHostedDispatch(ImmutableArray<HostedDispatchHeader> append, BattleActionHeaderReference? headerReference = null, bool dispatchAppended = false, HostedDispatchCompletion? completion = null, PreparedHostedCompletionAck? acknowledgment = null, bool freshActionPackets = false)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not null || hostedCompletionInvocation is not null && acknowledgment is null)
        {
            throw new InvalidOperationException("Hosted dispatch requires an idle presentation process after entry.");
        }
        if (append.IsDefault || freshActionPackets && acknowledgment is not null || dispatchAppended && (append.IsEmpty || headerReference is not null) ||
            headerReference is null && !dispatchAppended && completion is null && acknowledgment is null)
        {
            throw new InvalidDataException("Hosted dispatch requires explicit headers and a dispatch or acknowledgement.");
        }
        BattleModelRoster roster = graphics.ActorModels.Roster;
        if (acknowledgment is not null)
        {
            ValidatePreparedHostedCompletionAck(acknowledgment);
        }

        NativeBattleActionPacketsState original = nativeActionPackets;
        NativeBattleActionPacketsState packets = acknowledgment?.AcknowledgedPackets ?? original;
        if (!packets.Roster.Owners.SequenceEqual(roster.Owners))
        {
            throw new InvalidDataException("Hosted packet storage differs from its graphics roster.");
        }
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        packets = freshActionPackets
            ? NativeBattleActionPacketsState.Create(roster, packets.ExportRetained(), aliases)
            : packets.WithRoster(roster, aliases);
        ImmutableArray<BattleActionHeaderReference>.Builder appended = ImmutableArray.CreateBuilder<BattleActionHeaderReference>(append.Length);
        foreach (HostedDispatchHeader write in append)
        {
            if (write.Contacts.IsDefault)
            {
                throw new InvalidDataException("Hosted action admission requires its explicit contact list.");
            }
            (packets, BattleActionHeaderReference reference) = packets.Prepare(write.Header);
            packets = packets.AppendContacts(write.Contacts);
            (packets, _) = packets.Publish(write.Contacts.Length);
            packets = packets.WritePlayerRepeatMode(reference, 1);
            _ = ReadHostedDispatchSource(graphics, packets, reference);
            appended.Add(reference);
        }
        if (dispatchAppended)
        {
            headerReference = appended[0];
        }

        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        HostedDispatchAction? action = null;
        if (headerReference is BattleActionHeaderReference selected)
        {
            (BattleModelOwner owner, NativeBattleGraphicsResource source) = ReadHostedDispatchSource(graphics, packets, selected);
            (descriptors, int slot) = descriptors.EnqueueAction(128, selected);
            HostedPresentationInvocation invocation = new(checked(lastHostedActionIssued + 1), slot, descriptors.Packets[slot].Serial, selected);
            action = new(invocation, owner, source, graphics.ActorModels.Get(owner).ModelId4, owner.Side == BattleSide.Allies, roster.Party.Count());
        }
        HostedCompletionInvocation? nextCompletion = null;
        if (completion is not null)
        {
            if (!IsHostedCompletionCallback(completion.Callback))
            {
                throw new ArgumentOutOfRangeException(nameof(completion), "The hosted callback is not a native combat completion.");
            }
            if (!roster.Contains(completion.Owner) || !StringComparer.Ordinal.Equals(RequireCanonicalGraphicsActorId(completion.Owner), completion.ActorId))
            {
                throw new InvalidDataException("Hosted completion requires its canonical bound actor.");
            }
            (descriptors, int slot) = descriptors.Enqueue(10, 128, completion.Callback);
            nextCompletion = new(new(checked(lastHostedCompletionIssued + 1), completion.Callback, completion.Owner, completion.ActorId), slot, descriptors.Packets[slot].Serial);
        }
        NativeBattleGraphicsState admitted = ReferenceEquals(graphics.Descriptors, descriptors) ? graphics : graphics with { Descriptors = descriptors };
        return new(this, graphics, original, hostedPreparationEpoch, checked(hostedPreparationEpoch + 1), lastHostedActionIssued, lastHostedCompletionIssued, admitted, packets, action, nextCompletion, acknowledgment, appended.MoveToImmutable());
    }

    private (BattleModelOwner Owner, NativeBattleGraphicsResource Source) ReadHostedDispatchSource(NativeBattleGraphicsState graphics, NativeBattleActionPacketsState packets, BattleActionHeaderReference reference)
    {
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, reference, graphics.ActionGraphics);
        NativeBattleGraphicsResource source = NativeActionResource(graphics, context.Actor);
        source.Validate();
        return (context.Actor, source);
    }

    internal void ValidatePreparedHostedDispatch(PreparedHostedDispatch prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Graphics, NativeGraphics) ||
            !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) || prepared.Epoch != hostedPreparationEpoch ||
            prepared.IssuedAction != lastHostedActionIssued || prepared.IssuedCompletion != lastHostedCompletionIssued ||
            !hostedBattlePresentation || hostedFrameServices is not null || hostedPresentationInvocation is not null ||
            hostedCompletionInvocation is not null && prepared.Acknowledgment is null)
        {
            throw new InvalidOperationException("The prepared dispatch no longer belongs to this idle hosted process.");
        }
        if (prepared.Acknowledgment is { } ack)
        {
            ValidatePreparedHostedCompletionAck(ack);
        }
    }

    internal void CommitHostedDispatch(PreparedHostedDispatch prepared)
    {
        nativeActionPackets = prepared.Packets;
        NativeGraphics = prepared.Admitted;
        if (prepared.Action is { } action)
        {
            presentationSourceOwner = action.SourceOwner;
            presentationSourceResource = action.Source;
            presentationSourceModelId = action.ModelId;
            presentationSourceIsParty = action.IsParty;
            presentationPartyActorCount = action.PartyActorCount;
            hostedPresentationInvocation = action.Invocation;
            lastHostedActionIssued = action.Invocation.Token;
        }
        if (prepared.Acknowledgment is { } ack)
        {
            lastHostedCompletionAcknowledged = ack.Invocation.Request.Token;
        }
        hostedCompletionInvocation = prepared.Completion;
        if (prepared.Completion is { } completion)
        {
            lastHostedCompletionIssued = completion.Request.Token;
        }
        hostedPreparationEpoch = prepared.NextEpoch;
    }
}
