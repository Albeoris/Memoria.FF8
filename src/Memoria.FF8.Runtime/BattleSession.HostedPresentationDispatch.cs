using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedDispatchHeader(NativeBattleActionHeaderPreparation Header, ImmutableArray<NativeBattleContactPacket> Contacts);
    internal sealed record HostedDispatchCompletion(uint Callback, int Row, string ActorId);
    internal sealed record HostedDispatchAction(HostedPresentationInvocation Invocation, int SourceIndex, NativeBattleGraphicsResource Source, byte? ModelId, bool IsParty, int PartyActorCount);

    internal sealed class PreparedHostedDispatch(BattleSession owner, NativeBattleGraphicsState graphics, NativeBattleActionPacketsState originalPackets, long epoch, long nextEpoch, long issuedAction, long issuedCompletion, NativeBattleGraphicsState admitted, NativeBattleActionPacketsState packets, HostedDispatchAction? action, HostedCompletionInvocation? completion, PreparedHostedCompletionAck? acknowledgment, ImmutableArray<int> appendedIndices)
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
        internal ImmutableArray<int> AppendedIndices { get; } = appendedIndices;
    }

    internal PreparedHostedDispatch PrepareHostedDispatch(ImmutableArray<HostedDispatchHeader> append, int? headerIndex = null, bool dispatchAppended = false, HostedDispatchCompletion? completion = null, PreparedHostedCompletionAck? acknowledgment = null, bool freshActionPackets = false)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: { } roster } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not null || hostedCompletionInvocation is not null && acknowledgment is null)
        {
            throw new InvalidOperationException("Hosted dispatch requires an idle presentation process after entry.");
        }
        if (append.IsDefault || freshActionPackets && acknowledgment is not null || dispatchAppended && (append.IsEmpty || headerIndex is not null) ||
            headerIndex is null && !dispatchAppended && completion is null && acknowledgment is null)
        {
            throw new InvalidDataException("Hosted dispatch requires explicit headers and a dispatch or acknowledgement.");
        }
        roster.Validate();
        RequireHostedActionModelBank(graphics, roster);
        if (acknowledgment is not null)
        {
            ValidatePreparedHostedCompletionAck(acknowledgment);
        }

        NativeBattleActionPacketsState original = nativeActionPackets;
        NativeBattleActionPacketsState packets = acknowledgment?.AcknowledgedPackets ?? original;
        if (freshActionPackets || !append.IsEmpty && packets.PresentationRoster is null)
        {
            packets = NativeBattleActionPacketsState.FromPresentationStorage(roster);
        }
        if (packets.PresentationRoster is { } bank && !bank.Rows.SequenceEqual(roster.Rows))
        {
            throw new InvalidDataException("Hosted packet storage differs from its graphics roster.");
        }
        int contactCapacity = packets.ContactCount;
        foreach (HostedDispatchHeader write in append)
        {
            if (write.Contacts.IsDefault)
            {
                throw new InvalidDataException("Hosted action admission requires its explicit contact list.");
            }
            contactCapacity = checked(contactCapacity + write.Contacts.Length);
        }
        if (!append.IsEmpty)
        {
            packets = packets.GrowPresentationContacts(contactCapacity);
        }
        ImmutableArray<int>.Builder appended = ImmutableArray.CreateBuilder<int>(append.Length);
        foreach (HostedDispatchHeader write in append)
        {
            (packets, int index) = packets.Prepare(write.Header);
            packets = packets.AppendContacts(write.Contacts);
            (packets, _) = packets.Publish(write.Contacts.Length);
            packets = packets.WritePlayerRepeatMode(index, 1);
            _ = ReadHostedDispatchSource(graphics, roster, packets, index);
            appended.Add(index);
        }
        if (dispatchAppended)
        {
            headerIndex = appended[0];
        }

        NativeBattleDescriptorState descriptors = graphics.Descriptors;
        HostedDispatchAction? action = null;
        if (headerIndex is int selected)
        {
            (byte row, NativeBattleGraphicsResource source) = ReadHostedDispatchSource(graphics, roster, packets, selected);
            uint argument = checked(0x1D280C4u + 20u * (uint)selected);
            (descriptors, int slot) = descriptors.Enqueue(104, 128, argument);
            HostedPresentationInvocation invocation = new(checked(lastHostedActionIssued + 1), slot, descriptors.Packets[slot].Serial, selected, argument);
            action = new(invocation, row, source, graphics.ActorModels.Get(row).ModelId4, roster[row].IsParty, roster.PartyRows.Count());
        }
        HostedCompletionInvocation? nextCompletion = null;
        if (completion is not null)
        {
            if (!IsHostedCompletionCallback(completion.Callback))
            {
                throw new ArgumentOutOfRangeException(nameof(completion), "The hosted callback is not a native combat completion.");
            }
            if (!roster.ContainsRow(completion.Row) || !StringComparer.Ordinal.Equals(roster[completion.Row].ActorId, completion.ActorId))
            {
                throw new InvalidDataException("Hosted completion requires its canonical bound actor.");
            }
            (descriptors, int slot) = descriptors.Enqueue(10, 128, completion.Callback);
            nextCompletion = new(new(checked(lastHostedCompletionIssued + 1), completion.Callback, completion.Row, completion.ActorId), slot, descriptors.Packets[slot].Serial);
        }
        NativeBattleGraphicsState admitted = ReferenceEquals(graphics.Descriptors, descriptors) ? graphics : graphics with { Descriptors = descriptors };
        return new(this, graphics, original, hostedPreparationEpoch, checked(hostedPreparationEpoch + 1), lastHostedActionIssued, lastHostedCompletionIssued, admitted, packets, action, nextCompletion, acknowledgment, appended.MoveToImmutable());
    }

    private (byte Row, NativeBattleGraphicsResource Source) ReadHostedDispatchSource(NativeBattleGraphicsState graphics, BattleModelRoster roster, NativeBattleActionPacketsState packets, int headerIndex)
    {
        if (packets.PresentationRoster is null || (uint)headerIndex >= NativeBattleActionPacketsState.HeaderCapacity ||
            packets.Headers[headerIndex].PresentationContactCount is null)
        {
            throw new InvalidDataException("Hosted dispatch requires its retained typed header.");
        }
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, headerIndex, graphics.ActionGraphics);
        context.ValidatePresentationTargets(roster);
        byte row = packets.Headers[headerIndex].ReadByte(0);
        NativeBattleGraphicsResource source = NativeActionResource(graphics, row);
        source.Validate();
        return (row, source);
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
            presentationSourceOwner = action.SourceIndex;
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
