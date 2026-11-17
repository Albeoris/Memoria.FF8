using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleDiagnosticEffectDispatch? diagnosticGuardianDispatch;
    private BattleDiagnosticNativeContact? diagnosticGuardianContact;

    private ImmutableArray<BattleDiagnosticNativeContact> PrepareDiagnosticGuardianContacts(BattlePresentationProcess process, ExpandedHostedInvocation invocation)
    {
        if (DiagnosticResolution is null || diagnosticGuardianDispatch is not { HeaderReference: { } reference, NativeHeaderGeneration: long generation, ActorSlot: { } actorSlot } dispatch ||
            invocation.HeaderReference != reference || dispatch.ActorId != invocation.Command.ActorId ||
            dispatch.NativeCommand != invocation.Header.Command || dispatch.ActionId != invocation.Header.Action ||
            dispatch.RequestedEffect != invocation.Header.Effect || process.ReadDiagnosticCounterHeaderGeneration(reference.Ordinal) != generation)
        {
            return [];
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        NativeBattleRetainedActionHeaderSlot? rawHeader = dispatch.NativeHeaderIndex is int rawIndex ? packets.RetainedStorage.Headers[rawIndex] : null;
        ImmutableArray<NativeBattleContactPacket> pending = packets.ReadHeaderContacts(reference);
        ImmutableArray<BattleDiagnosticNativeContact>.Builder proofs = ImmutableArray.CreateBuilder<BattleDiagnosticNativeContact>(pending.Length);
        for (int index = 0; index < pending.Length; index++)
        {
            NativeBattleContactPacket contact = pending[index];
            BattleContactOccurrence occurrence = contact.Occurrence ?? throw new InvalidDataException("Guardian formula proof requires its admitted contact occurrence.");
            BattleCombatant recipient = GetActor(DiagnosticActorId(contact.Primary.Recipient));
            if (recipient.Id != invocation.Contacts[index].RecipientId)
            {
                throw new InvalidDataException("Guardian formula proof lost its admitted recipient.");
            }
            byte? originalRecipient = packets.ModelAliases.TryEncodeExact(contact.Primary.Recipient, out byte operand) ? operand : null;
            NativeBattleRawContactPacket? raw = occurrence.Ordinal < NativeBattleRetainedActionPackets.ContactCapacity ? packets.RetainedStorage.Contacts[occurrence.Ordinal] : null;
            if (raw is not null && raw.Primary.ActorIndex != originalRecipient)
            {
                raw = null;
            }
            proofs.Add(new(0x4850A0, dispatch.Serial, invocation.Command.TargetId, invocation.Command.ActorId, dispatch.NativeActorIndex, recipient.Id,
                originalRecipient, dispatch.NativeHeaderIndex, rawHeader?.Payload, rawHeader?.UnknownByteMask, rawHeader?.FirstContactIndex,
                raw is null ? null : occurrence.Ordinal, raw is null ? null : NativeBattleContactPacketCodec.Encode(raw).ToImmutableArray())
            {
                ActorSlot = actorSlot,
                RecipientSlot = recipient.Slot,
                HeaderReference = reference,
                HeaderGeneration = generation,
                Occurrence = occurrence,
                FirstContactOrdinal = invocation.FirstContact.Ordinal,
                ContactCount = pending.Length
            });
        }
        return proofs.MoveToImmutable();
    }
}
