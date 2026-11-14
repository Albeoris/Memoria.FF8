using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed class PreparedHostedContactReplacement(BattleSession owner, HostedPresentationInvocation invocation, NativeBattleGraphicsState graphics, long epoch, NativeBattleActionPacketsState originalPackets, NativeBattleActionPacketsState replacementPackets, NativeBattleGraphicsState? replacementGraphics)
    {
        internal BattleSession Owner { get; } = owner;
        internal HostedPresentationInvocation Invocation { get; } = invocation;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal long Epoch { get; } = epoch;
        internal NativeBattleActionPacketsState OriginalPackets { get; } = originalPackets;
        internal NativeBattleActionPacketsState ReplacementPackets { get; } = replacementPackets;
        internal NativeBattleGraphicsState? ReplacementGraphics { get; } = replacementGraphics;
    }

    internal PreparedHostedContactReplacement PrepareHostedContactReplacement(long token, ImmutableArray<NativeBattleContactPacket> replacements, NativeBattleTriggerReceipt? acknowledgeTrigger = null)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: { } roster } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation || token != invocation.Token)
        {
            throw new InvalidOperationException("Contact replacement requires the active entry-ready hosted invocation outside graphics services.");
        }
        RequireHostedActionModelBank(graphics, roster);
        NativeBattleActionPacketsState packets = nativeActionPackets;
        if (packets.PresentationRoster is not { } packetRoster || !packetRoster.Rows.SequenceEqual(roster.Rows) ||
            packets.ActiveHeaderIndex is int active && active != invocation.HeaderIndex)
        {
            throw new InvalidDataException("Contact replacement requires the active invocation's hosted packet bank and header.");
        }

        // The admitted header owns its full extent even before 104 first activates its cursor.
        ImmutableArray<NativeBattleContactPacket> original = packets.ReadHeaderContacts506BA0(invocation.HeaderIndex);
        NativeBattleActionHeaderSlot header = packets.Headers[invocation.HeaderIndex];
        if (replacements.IsDefault || replacements.Length != original.Length || header.FirstContactIndex is not int first)
        {
            throw new InvalidDataException("Contact replacement must retain the invocation's original contact extent.");
        }
        byte caster = header.ReadByte(0);
        NativeBattleGraphicsState? replacementGraphics = null;
        if (acknowledgeTrigger is not null)
        {
            acknowledgeTrigger.Validate(roster);
            if (!ReferenceEquals(graphics.PendingTrigger, acknowledgeTrigger) || acknowledgeTrigger.ActorIndex != caster ||
                acknowledgeTrigger is not { ContactIndex: 1, TotalContacts: 1 } || header.Command is not (1 or 12 or 28) || original.Length != 1)
            {
                throw new InvalidDataException("Contact replacement does not acknowledge this invocation's pending ordinary Gunblade receipt.");
            }
            replacementGraphics = graphics with { PendingTrigger = null };
        }
        ImmutableArray<NativeBattleContactPacket?>.Builder contacts = packets.Contacts.ToBuilder();
        for (int index = 0; index < replacements.Length; index++)
        {
            NativeBattleContactPacket packet = replacements[index];
            if (packet?.Primary is not { } primary || primary.ActorIndex != original[index].Primary.ActorIndex ||
                packet.Secondary is not { } secondary || secondary.ActorIndex != byte.MaxValue && secondary.ActorIndex != caster)
            {
                throw new InvalidDataException("Contact replacement must retain each primary recipient and bind any secondary to the header caster.");
            }
            contacts[first + index] = packet;
        }
        NativeBattleActionPacketsState replacement = packets with { Contacts = contacts.MoveToImmutable() };
        replacement.Validate();
        return new(this, invocation, graphics, hostedPreparationEpoch, packets, replacement, replacementGraphics);
    }

    internal void ValidatePreparedHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Invocation, hostedPresentationInvocation) ||
            !ReferenceEquals(prepared.Graphics, NativeGraphics) || !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) ||
            prepared.Epoch != hostedPreparationEpoch || !hostedBattlePresentation || NativeGraphics is not { EntryReady: true, PresentationRoster: not null } ||
            hostedFrameServices is not null)
        {
            throw new InvalidOperationException("The prepared contact replacement no longer belongs to this active hosted invocation.");
        }
    }

    internal void PublishHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        ValidatePreparedHostedContactReplacement(prepared);
        CommitHostedContactReplacement(prepared);
    }

    internal void CommitHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        nativeActionPackets = prepared.ReplacementPackets;
        if (prepared.ReplacementGraphics is { } graphics)
        {
            NativeGraphics = graphics;
        }
    }
}
