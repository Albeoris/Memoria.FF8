using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed class PreparedHostedContactReplacement(BattleSession owner, HostedPresentationInvocation invocation, NativeBattleGraphicsState graphics, long epoch, NativeBattleActionPacketsState originalPackets, NativeBattleActionPacketsState replacementPackets, NativeBattleGraphicsState? replacementGraphics, ImmutableArray<BattleActionHeaderReference> appendedHeaders, HostedGuardianBoostRequest? guardianRequest, PreparedHostedModelRegistry? registry, NativeBattleRenzokukenWidgetStepped? renzokuken, PreparedHostedGuardianFormChange? guardianForm)
    {
        internal PreparedHostedGuardianFormChange? GuardianForm { get; } = guardianForm;
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

    internal PreparedHostedContactReplacement PrepareHostedContactReplacement(long token, ImmutableArray<NativeBattleContactPacket> replacements, NativeBattleTriggerReceipt? acknowledgeTrigger = null, ImmutableArray<HostedDispatchHeader> append = default, HostedGuardianBoostRequest? acknowledgeGuardian = null, ImmutableArray<HostedModelLoadAdmission> allocations = default, PreparedHostedGuardianFormChange? guardianForm = null, ImmutableArray<NativeBattleContactPacket> routed = default)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics ||
            hostedFrameServices is not null || hostedPresentationInvocation is not { } invocation || token != invocation.Token)
        {
            throw new InvalidOperationException("Contact replacement requires the active entry-ready hosted invocation outside graphics services.");
        }
        NativeBattleGraphicsState originalGraphics = graphics;
        if (guardianForm is not null)
        {
            ValidatePreparedHostedGuardianFormChange(guardianForm);
            graphics = guardianForm.Staged.RequireNativeGraphics();
        }
        BattleSession native = guardianForm?.Staged ?? this;
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
            if (!ReferenceEquals(originalGraphics.PendingTrigger, acknowledgeTrigger) || acknowledgeTrigger.Actor != caster || original.Length != 1)
            {
                throw new InvalidDataException("Contact replacement requires the active caster's exact pending trigger and single physical contact.");
            }
            replacementGraphics = graphics with { PendingTrigger = null };
        }
        if (!routed.IsDefaultOrEmpty)
        {
            if (acknowledgeTrigger is null || replacementGraphics is null || header.Command is not (1 or 12 or 28) ||
                replacements is not [var selected] || selected.Primary is not { Sequence: 0, PresentedAmount: 0 })
            {
                throw new InvalidDataException("Routed graphics contacts require the ordinary Gunblade's resolved zero-damage source contact.");
            }
            foreach (NativeBattleContactPacket packet in routed)
            {
                packet.Validate();
                if (packet.Occurrence is not null || packet.Secondary is not null || !roster.Contains(packet.Primary.Recipient) ||
                    packet.Primary.Recipient == selected.Primary.Recipient || packet.Primary.PresentedAmount <= 0)
                {
                    throw new InvalidDataException("A routed graphics contact requires its own allocated damage recipient.");
                }
            }
            replacementGraphics = replacementGraphics with { PendingContacts = replacementGraphics.PendingContacts.AddRange(routed) };
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
        PreparedHostedModelRegistry? admission = native.PrepareHostedModelRegistry(graphics, allocations);
        NativeBattleActionPacketsState replacement = packets.ReplaceHeaderContacts(invocation.Header, replacements);
        if (admission is { Replacement.Roster: { } planned })
        {
            replacement = replacement.WithRoster(planned, graphics.NativeBindings.CaptureAliases(graphics.ActorModels, planned));
        }
        (replacement, ImmutableArray<BattleActionHeaderReference> appended) = native.AppendHostedHeaders(graphics, replacement, append, publishEventHeaders: true, admission?.Replacement ?? hostedModelRegistry);
        return new(this, invocation, originalGraphics, hostedPreparationEpoch, packets, replacement, replacementGraphics, appended, acknowledgeGuardian, admission, renzokuken, guardianForm);
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
        if (prepared.GuardianForm is { } form)
        {
            ValidatePreparedHostedGuardianFormChange(form);
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
        if (prepared.GuardianForm is { } form)
        {
            CommitHostedGuardianFormChange(form);
        }
        PublishHostedModelRegistry(prepared.Registry);
        nativeActionPackets = prepared.ReplacementPackets;
        ObserveDiagnosticCommittedHostedHeaders(prepared.AppendedHeaders);
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
