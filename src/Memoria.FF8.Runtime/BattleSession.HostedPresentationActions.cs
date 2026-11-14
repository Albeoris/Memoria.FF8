using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record HostedPresentationInvocation(long Token, int DescriptorSlot, byte DescriptorSerial, int HeaderIndex, uint HeaderArgument);

    private HostedPresentationInvocation? hostedPresentationInvocation;
    private long lastHostedActionIssued;
    private long lastHostedActionCompleted;

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
        ArgumentNullException.ThrowIfNull(header);
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: { } roster } graphics || hostedFrameServices is not null || hostedPresentationInvocation is not null)
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

        nativeActionPackets = packets;
        presentationSourceIndex = header.Actor;
        presentationSourceResource = source;
        presentationSourceModelId = modelId;
        presentationSourceIsParty = isParty;
        presentationPartyActorCount = partyActorCount;
        NativeGraphics = admitted;
        hostedPresentationInvocation = invocation;
        lastHostedActionIssued = token;
        return token;
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
