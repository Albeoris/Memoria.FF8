using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed class PreparedHostedContactReplacement(BattleSession owner, HostedPresentationInvocation invocation, NativeBattleGraphicsState graphics, long epoch, NativeBattleActionPacketsState originalPackets, NativeBattleActionPacketsState replacementPackets, NativeBattleGraphicsState? replacementGraphics, ImmutableArray<BattleActionHeaderReference> appendedHeaders, HostedGuardianBoostRequest? guardianRequest, PreparedHostedModelRegistry? registry, NativeBattleRenzokukenWidgetStepped? renzokuken)
    {
        internal BattleSession Owner { get; } = owner;
        internal HostedPresentationInvocation Invocation { get; } = invocation;
        internal NativeBattleGraphicsState Graphics { get; } = graphics;
        internal long Epoch { get; } = epoch;
        internal NativeBattleActionPacketsState OriginalPackets { get; } = originalPackets;
        internal NativeBattleActionPacketsState ReplacementPackets { get; } = replacementPackets;
        internal NativeBattleGraphicsState? ReplacementGraphics { get; } = replacementGraphics;
        internal ImmutableArray<BattleActionHeaderReference> AppendedHeaders { get; } = appendedHeaders;
        internal HostedGuardianBoostRequest? GuardianRequest { get; } = guardianRequest;
        internal PreparedHostedModelRegistry? Registry { get; } = registry;
        internal NativeBattleRenzokukenWidgetStepped? Renzokuken { get; } = renzokuken;
    }

    internal PreparedHostedContactReplacement PrepareHostedContactReplacement(long token, ImmutableArray<NativeBattleContactPacket> replacements, NativeBattleTriggerReceipt? acknowledgeTrigger = null, ImmutableArray<HostedDispatchHeader> append = default, HostedGuardianBoostRequest? acknowledgeGuardian = null, ImmutableArray<HostedModelLoadAdmission> allocations = default)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation || token != invocation.Token)
        {
            throw new InvalidOperationException("Contact replacement requires the active entry-ready hosted invocation outside graphics services.");
        }
        BattleModelRoster roster = HostedPlannedModelRoster;
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
        NativeBattleRenzokukenWidgetStepped? renzokuken = null;
        if (acknowledgeTrigger is not null)
        {
            acknowledgeTrigger.Validate(roster);
            if (header.Command == 5)
            {
                renzokuken = hostedRenzokukenPendingStep;
                if (renzokuken is null || !ReferenceEquals(renzokuken.Pending, acknowledgeTrigger) ||
                    renzokuken.Frame.ActionToken != token || renzokuken.Frame.Owner.Actor != caster || renzokuken.Frame.Owner.MagicId != header.Effect ||
                    renzokuken.Widget.Input.ContactIndex != acknowledgeTrigger.ContactIndex || renzokuken.Frame.Contacts + 1 != acknowledgeTrigger.ContactIndex ||
                    acknowledgeTrigger.TotalContacts != renzokuken.Widget.Block.Timeline.InputThresholds.Length)
                {
                    throw new InvalidDataException("Contact replacement requires the exact pending Renzokuken widget step.");
                }
            }
            else if (acknowledgeTrigger is not { ContactIndex: 1, TotalContacts: 1 } || header.Command is not (1 or 12 or 28))
            {
                throw new InvalidDataException("Contact replacement does not acknowledge this invocation's pending ordinary Gunblade receipt.");
            }
            if (!ReferenceEquals(graphics.PendingTrigger, acknowledgeTrigger) || acknowledgeTrigger.Actor != caster || original.Length != 1)
            {
                throw new InvalidDataException("Contact replacement requires the active caster's exact pending trigger and single physical contact.");
            }
            replacementGraphics = graphics with { PendingTrigger = null };
        }
        if (acknowledgeGuardian is not null)
        {
            if (acknowledgeTrigger is not null || acknowledgeGuardian.Token != token)
            {
                throw new InvalidDataException("Hosted contact replacement requires one matching native recompute request.");
            }
            ValidateHostedGuardianBoostRequest(acknowledgeGuardian);
        }
        if (append.IsDefault)
        {
            append = [];
        }
        if (!append.IsEmpty && (!packets.HasPublishedHeader(invocation.Header) || packets.ContactCount < header.FirstContact.Advance(original.Length).Ordinal))
        {
            throw new InvalidOperationException("Hosted contact append requires live packet counts beyond the current header and its contacts.");
        }
        PreparedHostedModelRegistry? admission = PrepareHostedModelRegistry(graphics, allocations);
        NativeBattleActionPacketsState replacement = packets.ReplaceHeaderContacts(invocation.Header, replacements);
        if (admission is { Replacement.Roster: { } planned })
        {
            replacement = replacement.WithRoster(planned, graphics.NativeBindings.CaptureAliases(graphics.ActorModels, planned));
        }
        (replacement, ImmutableArray<BattleActionHeaderReference> appended) = AppendHostedHeaders(graphics, replacement, append, publishEventHeaders: true, admission?.Replacement ?? hostedModelRegistry);
        return new(this, invocation, graphics, hostedPreparationEpoch, packets, replacement, replacementGraphics, appended, acknowledgeGuardian, admission, renzokuken);
    }

    internal void ValidatePreparedHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(prepared.Owner, this) || !ReferenceEquals(prepared.Invocation, hostedPresentationInvocation) ||
            !ReferenceEquals(prepared.Graphics, NativeGraphics) || !ReferenceEquals(prepared.OriginalPackets, nativeActionPackets) ||
            prepared.Epoch != hostedPreparationEpoch || prepared.Registry is { } registry && !ReferenceEquals(registry.Original, hostedModelRegistry) || !hostedBattlePresentation || NativeGraphics is not { EntryReady: true } ||
            hostedFrameServices is not null || prepared.Renzokuken is { } step && !ReferenceEquals(step, hostedRenzokukenPendingStep))
        {
            throw new InvalidOperationException("The prepared contact replacement no longer belongs to this active hosted invocation.");
        }
        if (prepared.GuardianRequest is { } guardian)
        {
            ValidateHostedGuardianBoostRequest(guardian);
        }
    }

    internal void PublishHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        ValidatePreparedHostedContactReplacement(prepared);
        CommitHostedContactReplacement(prepared);
    }

    internal void CommitHostedContactReplacement(PreparedHostedContactReplacement prepared)
    {
        PublishHostedModelRegistry(prepared.Registry);
        nativeActionPackets = prepared.ReplacementPackets;
        if (prepared.ReplacementGraphics is { } graphics)
        {
            NativeGraphics = graphics;
        }
        if (prepared.GuardianRequest is { } guardian)
        {
            lastHostedGuardianBoostAcknowledged = guardian.Token;
            pendingHostedGuardianBoost = null;
        }
        if (prepared.Renzokuken is { } renzokuken)
        {
            hostedRenzokukenPendingStep = null;
            hostedRenzokukenPublishedStep = renzokuken;
        }
    }
}
