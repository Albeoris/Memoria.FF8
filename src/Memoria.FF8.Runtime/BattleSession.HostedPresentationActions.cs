using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedPresentationInvocation(long Token, int DescriptorSlot, byte DescriptorSerial, int HeaderIndex, uint HeaderArgument);

    internal sealed class PreparedHostedAction(BattleSession owner, NativeBattleGraphicsState graphics, long epoch, long issued, long nextEpoch, NativeBattleActionPacketsState packets, int sourceIndex, NativeBattleGraphicsResource source, byte? modelId, bool isParty, int partyActorCount, NativeBattleGraphicsState admitted, HostedPresentationInvocation invocation)
    {
        internal BattleSession Owner { get; } = owner;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal long Epoch { get; } = epoch;
        internal long Issued { get; } = issued;
        internal long NextEpoch { get; } = nextEpoch;
        internal NativeBattleActionPacketsState Packets { get; } = packets;
        internal int SourceIndex { get; } = sourceIndex;
        internal NativeBattleGraphicsResource Source { get; } = source;
        internal byte? ModelId { get; } = modelId;
        internal bool IsParty { get; } = isParty;
        internal int PartyActorCount { get; } = partyActorCount;
        internal NativeBattleGraphicsState Admitted { get; } = admitted;
        internal HostedPresentationInvocation Invocation { get; } = invocation;
    }

    private HostedPresentationInvocation? hostedPresentationInvocation;
    private long lastHostedActionIssued;
    private long lastHostedActionCompleted;
    private long hostedPreparationEpoch;

    internal long? HostedActiveActionToken => hostedPresentationInvocation?.Token;

    internal bool IsHostedActionComplete(long token)
    {
        if (token <= 0 || token > lastHostedActionIssued)
        {
            throw new ArgumentOutOfRangeException(nameof(token), "The action token was not issued by this presentation process.");
        }
        return token <= lastHostedActionCompleted;
    }

    internal long BeginHostedAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        return PublishHostedAction(PrepareHostedAction(header, contacts));
    }

    internal PreparedHostedAction PrepareHostedAction(NativeBattleActionHeaderPreparation header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: { } roster } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not null || hostedCompletionInvocation is not null)
        {
            throw new InvalidOperationException("Hosted action admission requires an idle presentation process after entry.");
        }
        roster.Validate();
        RequireHostedActionModelBank(graphics, roster);
        if (contacts.IsDefault)
        {
            throw new InvalidDataException("Hosted action admission requires its explicit contact list.");
        }

        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromPresentationStorage(roster, Math.Max(NativeBattleActionPacketsState.ContactCapacity, contacts.Length));
        (packets, int headerIndex) = packets.Prepare(header);
        packets = packets.AppendContacts(contacts);
        (packets, _) = packets.Publish(contacts.Length);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, headerIndex, graphics.ActionGraphics);
        context.ValidatePresentationTargets(roster);

        NativeBattleGraphicsResource source = NativeActionResource(graphics, header.Actor);
        source.Validate();
        byte? modelId = graphics.Models[header.Actor].ModelId4;
        bool isParty = roster[header.Actor].IsParty;
        int partyActorCount = roster.PartyRows.Count();
        uint argument = checked(0x1D280C4u + 20u * (uint)headerIndex);
        (NativeBattleDescriptorState descriptors, int slot) = graphics.Descriptors.Enqueue(104, 128, argument);
        long token = checked(lastHostedActionIssued + 1);
        HostedPresentationInvocation invocation = new(token, slot, descriptors.Packets[slot].Serial, headerIndex, argument);
        NativeBattleGraphicsState admitted = graphics with { Descriptors = descriptors };
        return new(this, graphics, hostedPreparationEpoch, lastHostedActionIssued, checked(hostedPreparationEpoch + 1), packets, header.Actor, source, modelId, isParty, partyActorCount, admitted, invocation);
    }

    internal void ValidatePreparedHostedAction(PreparedHostedAction prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || prepared.Epoch != hostedPreparationEpoch || prepared.Issued != lastHostedActionIssued ||
            !ReferenceEquals(prepared.Graphics, NativeGraphics) || !hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: not null } ||
            hostedFrameServices is not null || hostedPresentationInvocation is not null || hostedCompletionInvocation is not null)
        {
            throw new InvalidOperationException("The prepared action no longer belongs to this idle hosted presentation process.");
        }
    }

    internal long PublishHostedAction(PreparedHostedAction prepared)
    {
        ValidatePreparedHostedAction(prepared);
        nativeActionPackets = prepared.Packets;
        presentationSourceIndex = prepared.SourceIndex;
        presentationSourceResource = prepared.Source;
        presentationSourceModelId = prepared.ModelId;
        presentationSourceIsParty = prepared.IsParty;
        presentationPartyActorCount = prepared.PartyActorCount;
        NativeGraphics = prepared.Admitted;
        hostedPresentationInvocation = prepared.Invocation;
        lastHostedActionIssued = prepared.Invocation.Token;
        hostedPreparationEpoch = prepared.NextEpoch;
        return lastHostedActionIssued;
    }

    private void ObserveHostedActionPacketRemoved(int slot, NativeBattleDescriptorPacket packet)
    {
        if (hostedPresentationInvocation is { } invocation && slot == invocation.DescriptorSlot &&
            packet.Serial == invocation.DescriptorSerial && packet.Command == 104 && packet.Argument == invocation.HeaderArgument &&
            packet.PriorityState == 255)
        {
            lastHostedActionCompleted = invocation.Token;
            hostedPresentationInvocation = null;
        }
    }
}
