using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleActionOutcomeEntry(BattlePresentationProcess.HostedPresentationInvocation Invocation, long HeaderGeneration, BattleModelOwner Source) : NativeBattlePresentationReceipt
{
    internal NativeBattlePhysicalPartyStatuses OriginalStatuses { get; init; } = NativeBattlePhysicalPartyStatuses.Unknown();
    internal NativeBattlePhysicalPartyStatuses BeforeStatuses { get; init; } = NativeBattlePhysicalPartyStatuses.Unknown();
    internal NativeBattlePhysicalPartyStatuses AfterStatuses { get; init; } = NativeBattlePhysicalPartyStatuses.Unknown();

    internal void Validate(BattlePresentationProcess.HostedPresentationInvocation? current, NativeBattleDescriptorPacket packet, long? generation, NativeBattleActionHeaderSlot header, ImmutableArray<NativeBattleContactPacket> contacts)
    {
        if (Invocation is null || !ReferenceEquals(Invocation, current) ||
            Invocation.Token <= 0 || Invocation.DescriptorSlot < 0 || !Invocation.DiagnosticOutcomeEntryIssued || Invocation.DiagnosticOutcomeEntryAcknowledged ||
            packet.Command != 104 || packet.Serial != Invocation.DescriptorSerial || packet.Header != Invocation.Header || packet.PriorityState == 255 ||
            Source is null || header.Source != Source ||
            header.ContactCount is not > 0 || contacts.IsDefaultOrEmpty || contacts.Length != header.ContactCount ||
            contacts.Any(contact => contact.Primary.PresentedAmount != 0 || contact.Secondary is not null))
        {
            throw new InvalidDataException("Native47DF60 action process entry lost its current invocation, descriptor, header, or zero-amount contacts.");
        }
        bool matchesOwner = Invocation.Provenance switch
        {
            BattlePresentationInvocationProvenance.ExternalDiagnostic => HeaderGeneration > 0 && generation == HeaderGeneration,
            BattlePresentationInvocationProvenance.Gameplay => HeaderGeneration == 0 && header is { Command: 8, Action: 356, Sequence: 18, Effect: 77, Source: BattleEnemyActorModel } &&
                contacts is [{ Primary: { Recipient: var recipient, Sequence: 0, Flags2: 1, HitFlags: 0, CoverSource: null } }] && recipient == Source,
            _ => false
        };
        if (!matchesOwner)
        {
            throw new InvalidDataException("Native47DF60 action process entry lost its original action owner.");
        }
        if (OriginalStatuses is null || BeforeStatuses is null || AfterStatuses is null)
        {
            throw new InvalidDataException("Native47DF60 action process entry requires its complete physical status owners.");
        }
        OriginalStatuses.Validate();
        BeforeStatuses.Validate();
        AfterStatuses.Validate();
        if (!BeforeStatuses.ClearHighBits().SameValues(AfterStatuses))
        {
            throw new InvalidDataException("Native47DF60 action process entry changed its physical battle-status masked write.");
        }
    }
}

public sealed partial class BattlePresentationProcess
{
    private NativeBattlePhysicalPartyStatuses physicalPartyBattleStatuses = NativeBattlePhysicalPartyStatuses.Unknown();

    internal NativeBattlePhysicalPartyStatuses ReadPhysicalPartyBattleStatuses()
    {
        NativeBattleModelLifecycleServices facts = hostedFrameServices ?? (services ?? throw new InvalidOperationException("Physical battle statuses require the current logical services.")).CaptureModels(HostedPlannedModelRoster, hostedModelRegistry);
        NativeBattlePhysicalPartyStatuses result = physicalPartyBattleStatuses;
        result.Validate();
        for (byte index = 0; index < 3; index++)
        {
            if (FindOriginalActor(NativeBattleModelSlot.Decode(index)) is { } owner)
            {
                if (!facts.Facts.TryGetValue(owner, out NativeBattleModelActorFacts? actor) || !actor.IsParty || actor.RawBattleStatuses is not uint value)
                {
                    throw new NotSupportedException("Physical party battle statuses require the current occupied actor's raw DWORD owner.");
                }
                result = result.Observe(index, value);
            }
        }
        return result;
    }

    private NativeBattleActionOutcomeEntry PrepareActionOutcomeEntry(NativeBattleGraphicsState graphics)
    {
        HostedPresentationInvocation current = hostedPresentationInvocation ?? throw new InvalidDataException("Native47DF60 action process entry requires its active invocation.");
        if (current.DiagnosticOutcomeEntryIssued || current.DiagnosticOutcomeEntryAcknowledged ||
            (uint)current.DescriptorSlot >= (uint)graphics.Descriptors.Packets.Length)
        {
            throw new InvalidDataException("Native47DF60 action process entry requires its unconsumed current invocation.");
        }
        HostedPresentationInvocation issued = current with { DiagnosticOutcomeEntryIssued = true };
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(current.Header);
        long generation = 0;
        if (current.Provenance == BattlePresentationInvocationProvenance.ExternalDiagnostic)
        {
            generation = ReadDiagnosticCounterHeaderGeneration(current.Header.Ordinal) ?? throw new InvalidDataException("Native47DF60 diagnostic process entry requires its actual header generation.");
        }
        else
        {
            RequireAuthoredOutcomeSource(graphics, current, header);
        }
        NativeBattlePhysicalPartyStatuses before = ReadPhysicalPartyBattleStatuses();
        NativeBattleActionOutcomeEntry receipt = new(issued, generation, header.Source)
        {
            OriginalStatuses = physicalPartyBattleStatuses,
            BeforeStatuses = before,
            AfterStatuses = before.ClearHighBits()
        };
        receipt.Validate(issued, graphics.Descriptors.Packets[current.DescriptorSlot], generation, header, nativeActionPackets.ReadHeaderContacts(current.Header));
        return receipt;
    }

    internal void ValidateActionOutcomeEntry(NativeBattleActionOutcomeEntry receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        HostedPresentationInvocation current = hostedPresentationInvocation ?? throw new InvalidDataException("Native47DF60 action process entry has no current invocation.");
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if ((uint)current.DescriptorSlot >= (uint)graphics.Descriptors.Packets.Length)
        {
            throw new InvalidDataException("Native47DF60 action process entry left its current descriptor owner.");
        }
        NativeBattleActionHeaderSlot header = nativeActionPackets.ReadHeader(current.Header);
        if (current.Provenance == BattlePresentationInvocationProvenance.Gameplay)
        {
            RequireAuthoredOutcomeSource(graphics, current, header);
        }
        receipt.Validate(current, graphics.Descriptors.Packets[current.DescriptorSlot], ReadDiagnosticCounterHeaderGeneration(current.Header.Ordinal), header, nativeActionPackets.ReadHeaderContacts(current.Header));
        if (!ReferenceEquals(receipt.OriginalStatuses, physicalPartyBattleStatuses) || !receipt.BeforeStatuses.SameValues(ReadPhysicalPartyBattleStatuses()))
        {
            throw new InvalidDataException("Native47DF60 action process entry lost its current physical status owner.");
        }
    }

    internal void AcknowledgeActionOutcomeEntry(NativeBattleActionOutcomeEntry receipt)
    {
        ValidateActionOutcomeEntry(receipt);
        physicalPartyBattleStatuses = receipt.AfterStatuses;
        hostedPresentationInvocation = receipt.Invocation with { DiagnosticOutcomeEntryAcknowledged = true };
    }

    private void RequireAuthoredOutcomeSource(NativeBattleGraphicsState graphics, HostedPresentationInvocation invocation, NativeBattleActionHeaderSlot header)
    {
        if (invocation.Provenance != BattlePresentationInvocationProvenance.Gameplay || nativeActionPackets.ActiveHeader != invocation.Header ||
            graphics.OriginalEffect is not { Binding.Effect: 77, Parameters: { } effect } || effect.Header != invocation.Header || effect.Source != header.Source ||
            graphics.ActorModels.Get(header.Source).ModelId4 != 142)
        {
            throw new InvalidDataException("Native47DF60 action process entry requires the active original cinematic source.");
        }
    }
}
