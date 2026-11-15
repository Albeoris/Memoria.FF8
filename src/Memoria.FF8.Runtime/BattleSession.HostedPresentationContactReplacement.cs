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
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation || token != invocation.Token)
        {
            throw new InvalidOperationException("Contact replacement requires the active entry-ready hosted invocation outside graphics services.");
        }
        BattleModelRoster roster = graphics.ActorModels.Roster;
        NativeBattleActionPacketsState packets = nativeActionPackets;
        if (!packets.Roster.Owners.SequenceEqual(roster.Owners) ||
            packets.ActiveHeader is BattleActionHeaderReference active && active != invocation.Header)
        {
            throw new InvalidDataException("Contact replacement requires the active invocation's hosted packet bank and header.");
        }

        // The admitted header owns its full extent even before 104 first activates its cursor.
        ImmutableArray<NativeBattleContactPacket> original = packets.ReadHeaderContacts(invocation.Header);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(invocation.Header);
        if (replacements.IsDefault || replacements.Length != original.Length)
        {
            throw new InvalidDataException("Contact replacement must retain the invocation's original contact extent.");
        }
        BattleModelOwner caster = header.Source;
        NativeBattleGraphicsState? replacementGraphics = null;
        if (acknowledgeTrigger is not null)
        {
            acknowledgeTrigger.Validate(roster);
            if (!ReferenceEquals(graphics.PendingTrigger, acknowledgeTrigger) || acknowledgeTrigger.Actor != caster ||
                acknowledgeTrigger is not { ContactIndex: 1, TotalContacts: 1 } || header.Command is not (1 or 12 or 28) || original.Length != 1)
            {
                throw new InvalidDataException("Contact replacement does not acknowledge this invocation's pending ordinary Gunblade receipt.");
            }
            replacementGraphics = graphics with { PendingTrigger = null };
        }
        NativeBattleActionPacketsState replacement = packets.ReplaceHeaderContacts(invocation.Header, replacements);
        return new(this, invocation, graphics, hostedPreparationEpoch, packets, replacement, replacementGraphics);
    }

    internal void ValidatePreparedHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Invocation, hostedPresentationInvocation) ||
            !ReferenceEquals(prepared.Graphics, NativeGraphics) || !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) ||
            prepared.Epoch != hostedPreparationEpoch || !hostedBattlePresentation || NativeGraphics is not { EntryReady: true } ||
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
