using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticReflection(uint ProducerAddress, long SourceDispatchSerial, int SourceHeaderIndex, long SourceHeaderGeneration, int ReflectedHeaderIndex, long ReflectedHeaderGeneration, string ActorId, byte? ActorIndex, string ReflectorId, byte? ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte? RecipientIndex, byte SourceCommand, ushort SourceActionId, ushort SourceEffect, byte SourceAttackType, byte SourceAttackFlags, ushort ReflectedActionId, ushort ReflectedEffect, byte ReflectedTargetFlags, ushort? ReflectedTargetMask)
{
    public BattleRosterSlot? ActorSlot { get; init; }
    public BattleRosterSlot? ReflectorSlot { get; init; }
    public BattleRosterSlot? RecipientSlot { get; init; }
    public BattleDiagnosticReflectionTargetProvenance? TargetProvenance { get; init; }
}
public sealed record BattleDiagnosticReflectionTargetProvenance(int HeaderIndex, long HeaderGeneration, int FirstContactIndex, ImmutableArray<byte?> ContactRecipients)
{
    public ImmutableArray<string> ContactActorIds { get; init; } = [];
    public ImmutableArray<BattleRosterSlot?> ContactSlots { get; init; } = [];
}
public sealed record BattleDiagnosticReflectionRejection(int? SourceHeaderIndex, long? ExpectedSourceGeneration, long? ActualSourceGeneration, long? SourceDispatchSerial, int PendingHeaderCount, int ObservedHeaderCount, BattleDiagnosticReflection? Candidate);

public static class BattleDiagnosticReflectionLineage
{
    public static bool Matches(BattleDiagnosticEffectDispatch child, BattleDiagnosticEffectDispatch source)
    {
        if (child.Reflection is not { } proof || source.Reflection is not null || proof.ProducerAddress != 0x487DF0 || proof.SourceDispatchSerial != source.Serial ||
            source.Serial >= child.Serial || source.Callback > child.Callback || string.IsNullOrWhiteSpace(proof.ActorId) || proof.SourceEffect == 0 || proof.ReflectedEffect == 0 ||
            proof.ActorIndex is >= NativeBattleSlots.Count || proof.ReflectorIndex is >= NativeBattleSlots.Count || proof.RecipientIndex is >= NativeBattleSlots.Count ||
            proof.SourceHeaderGeneration <= 0 || proof.ReflectedHeaderGeneration <= proof.SourceHeaderGeneration)
        {
            return false;
        }
        if (source.HeaderReference?.Ordinal != proof.SourceHeaderIndex || source.NativeHeaderGeneration != proof.SourceHeaderGeneration ||
            child.HeaderReference?.Ordinal != proof.ReflectedHeaderIndex || child.NativeHeaderGeneration != proof.ReflectedHeaderGeneration ||
            source.NativeActorIndex != proof.ActorIndex || child.NativeActorIndex != proof.ActorIndex || source.ActorId != proof.ActorId || child.ActorId != proof.ActorId ||
            proof.ActorSlot is not { } actor || source.ActorSlot != actor || child.ActorSlot != actor || proof.ReflectorSlot is not { } reflector ||
            proof.RecipientSlot is not { } recipient || reflector.Side == recipient.Side || !MatchesOriginalSide(proof.ActorIndex, actor.Side) ||
            !MatchesOriginalSide(proof.ReflectorIndex, reflector.Side) || !MatchesOriginalSide(proof.RecipientIndex, recipient.Side) ||
            string.IsNullOrWhiteSpace(proof.ReflectorId) || string.IsNullOrWhiteSpace(proof.RecipientId))
        {
            return false;
        }
        if (proof.SourceCommand == 247 || source.NativeCommand != proof.SourceCommand || source.ActionId != proof.SourceActionId || source.RequestedEffect != proof.SourceEffect ||
            source.ExecutedEffect != proof.SourceEffect || !source.ContactActorIds.Contains(proof.ReflectorId) ||
            source.NativeTargetMask is ushort sourceMask && (proof.ReflectorIndex is not byte reflectorIndex || (sourceMask & (1 << reflectorIndex)) == 0) ||
            (proof.ReflectorStatuses1 & 0x80) == 0 || (proof.SourceAttackFlags & 0x10) == 0 || proof.SourceAttackType is not (2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33))
        {
            return false;
        }
        ushort? selectedMask = proof.RecipientIndex is byte index ? ReadReflectionMask(index, proof.ReflectedTargetFlags) : null;
        return child.Ability == (actor.Side == BattleSide.Allies ? BattleAbility.Magic : BattleAbility.EnemyAbility) &&
            child.NativeCommand == 247 && proof.ReflectedActionId == unchecked((byte)proof.SourceActionId) && child.ActionId == proof.ReflectedActionId &&
            child.RequestedEffect == proof.ReflectedEffect && child.ExecutedEffect == proof.ReflectedEffect && proof.ReflectedTargetMask == selectedMask &&
            MatchesTargets(child, proof, recipient.Side);
    }

    internal static ushort ReadReflectionMask(byte index, byte flags)
    {
        return checked((ushort)((1 << index) | ((flags & 1) != 0 ? 0x4000 : 0) | ((flags & 2) != 0 ? 0x2000 : 0)));
    }

    private static bool MatchesOriginalSide(byte? operand, BattleSide side)
    {
        return operand is not byte value || value < NativeBattleSlots.Count && NativeBattleSlots.Decode(value).Side == side;
    }

    private static bool MatchesTargets(BattleDiagnosticEffectDispatch child, BattleDiagnosticReflection proof, BattleSide recipientSide)
    {
        if (proof.TargetProvenance is not { } targets || targets.HeaderIndex != proof.ReflectedHeaderIndex || targets.HeaderGeneration != proof.ReflectedHeaderGeneration ||
            targets.FirstContactIndex < 0 || targets.ContactActorIds.IsDefaultOrEmpty || !child.ContactActorIds.SequenceEqual(targets.ContactActorIds) ||
            targets.ContactSlots.IsDefault || targets.ContactRecipients.IsDefault || targets.ContactSlots.Length != targets.ContactActorIds.Length ||
            targets.ContactRecipients.Length != targets.ContactActorIds.Length ||
            targets.ContactSlots.Any(slot => slot is null || slot.Value.Side != recipientSide))
        {
            return false;
        }
        ushort contacts = 0;
        bool allOriginal = true;
        foreach (byte? value in targets.ContactRecipients)
        {
            if (value is not byte recipient)
            {
                allOriginal = false;
                continue;
            }
            if (recipient >= NativeBattleSlots.Count || NativeBattleSlots.Decode(recipient).Side != recipientSide)
            {
                return false;
            }
            contacts |= checked((ushort)(1 << recipient));
        }
        return child.NativeTargetMask is null || allOriginal && child.NativeTargetMask.Value == contacts;
    }
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticReflectedCast(BattleActionPresentation Source, BattleActorReference Reflector, BattleRosterSlot ReflectorSlot, byte? ReflectorIndex, uint ReflectorStatuses1, BattleActorReference Recipient, BattleRosterSlot RecipientSlot, byte? RecipientIndex, BattleNativeAction Original, BattleNativeAction Reflected);
    private bool trackDiagnosticReflections;
    private Dictionary<BattleActionPresentation, DiagnosticReflectedCast>? diagnosticReflectedCasts;
    private Dictionary<BattleActionPresentation, BattleDiagnosticEffectDispatch>? diagnosticReflectionSources;

    private static int? OriginalDiagnosticHeaderIndex(BattleActionHeaderReference? header)
    {
        return header is { } reference && reference.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? reference.Ordinal : null;
    }

    private void RecordDiagnosticReflection(BattleActionPresentation source, BattleActionPresentation reflected, BattleCombatant reflector, BattleCombatant recipient, BattleNativeAction original)
    {
        if (!trackDiagnosticReflections)
        {
            return;
        }
        diagnosticReflectedCasts ??= new(ReferenceEqualityComparer.Instance);
        diagnosticReflectedCasts.Add(reflected, new(source, TargetActorReference(reflector), reflector.Slot ?? throw new InvalidDataException("Reflection proof requires the occupied reflector."),
            ReadDiagnosticOriginalOperand(reflector), reflector.NativeStatus1, TargetActorReference(recipient), recipient.Slot ?? throw new InvalidDataException("Reflection proof requires the occupied recipient."),
            ReadDiagnosticOriginalOperand(recipient), original,
            reflected.NativeAction ?? throw new InvalidOperationException("A prepared reflection requires its installed action.")));
    }

    private void PublishDiagnosticReflectionCandidates(BattleSession candidate)
    {
        if (candidate.diagnosticReflectedCasts is not { } staged)
        {
            return;
        }
        diagnosticReflectedCasts ??= new(ReferenceEqualityComparer.Instance);
        foreach ((BattleActionPresentation command, DiagnosticReflectedCast proof) in staged)
        {
            diagnosticReflectedCasts.Add(command, proof);
        }
    }

    private static ExpandedHostedInvocation? ReadDiagnosticBoundCast(ExpandedHostedInvocation? current, BattleActionHeaderReference? entered)
    {
        if (current?.HeaderReference is not { } first || entered is not { } reference)
        {
            return null;
        }
        int offset = reference.Ordinal - first.Ordinal;
        if (offset == 0)
        {
            return current;
        }
        return offset > 0 && offset <= current.FollowingCasts.Length ? current.FollowingCasts[offset - 1] : null;
    }

    private static ImmutableArray<string> ReadDiagnosticContactActorIds(NativeBattleActionPacketsState packets, BattleActionHeaderReference reference)
    {
        return packets.ReadHeaderContacts(reference).Select(contact => DiagnosticActorId(contact.Primary.Recipient)).ToImmutableArray();
    }

    private BattleDiagnosticEffectDispatch CompleteDiagnosticReflection(BattlePresentationProcess process, BattlePresentationEffectObservation observation, ExpandedHostedInvocation? cast, BattleDiagnosticEffectDispatch dispatch)
    {
        if (dispatch.NativeCommand != 247)
        {
            if (cast is not null && diagnosticReflectedCasts?.Values.Any(proof => ReferenceEquals(proof.Source, cast.Command)) == true)
            {
                diagnosticReflectionSources ??= new(ReferenceEqualityComparer.Instance);
                diagnosticReflectionSources[cast.Command] = dispatch;
            }
            return dispatch;
        }
        if (cast is null || diagnosticReflectedCasts is not { } staged || !staged.Remove(cast.Command, out DiagnosticReflectedCast? pending))
        {
            return Reject("PENDING_HEADER_ABSENT");
        }
        if (diagnosticReflectionSources is not { } sources || !sources.TryGetValue(pending.Source, out BattleDiagnosticEffectDispatch? source) ||
            source.HeaderReference is not { } sourceReference || source.NativeHeaderGeneration is not long sourceGeneration)
        {
            return Reject("SOURCE_DISPATCH_MISSING");
        }
        if (!staged.Values.Any(proof => ReferenceEquals(proof.Source, pending.Source)))
        {
            sources.Remove(pending.Source);
        }
        long? liveSourceGeneration = process.ReadDiagnosticCounterHeaderGeneration(sourceReference.Ordinal);
        if (liveSourceGeneration != sourceGeneration)
        {
            return Reject("SOURCE_HEADER_GENERATION_STALE", source, liveSourceGeneration);
        }
        if (dispatch.HeaderReference is not { } reference || dispatch.NativeHeaderGeneration is not long headerGeneration ||
            dispatch.ActorId is not { } actorId || observation.Header is not { } header ||
            source.NativeCommand is not byte sourceCommand || source.ActionId is not int sourceAction)
        {
            return Reject("CHILD_HEADER_GENERATION_MISSING", source, liveSourceGeneration);
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        ImmutableArray<NativeBattleContactPacket> contacts = packets.ReadHeaderContacts(reference);
        ImmutableArray<string> recipients = dispatch.ContactActorIds;
        ImmutableArray<BattleRosterSlot?> contactSlots = recipients.Select(id => id == pending.Recipient.ActorId ? pending.RecipientSlot : GetActor(id).Slot).ToImmutableArray();
        ImmutableArray<byte?> originalRecipients = contacts.Select(contact => packets.ModelAliases.TryEncodeExact(contact.Primary.Recipient, out byte operand) ? (byte?)operand : null).ToImmutableArray();
        byte targetFlags = pending.Reflected.TargetFlags.GetValueOrDefault();
        ushort? selected = pending.RecipientIndex is byte originalRecipient ? BattleDiagnosticReflectionLineage.ReadReflectionMask(originalRecipient, targetFlags) : null;
        BattleDiagnosticReflection evidence = new(0x487DF0, source.Serial, sourceReference.Ordinal, sourceGeneration, reference.Ordinal, headerGeneration, actorId,
            dispatch.NativeActorIndex, pending.Reflector.ActorId, pending.ReflectorIndex, pending.ReflectorStatuses1, pending.Recipient.ActorId, pending.RecipientIndex,
            sourceCommand, checked((ushort)sourceAction), pending.Original.MagicId, pending.Original.AttackType, pending.Original.AttackFlags,
            pending.Reflected.ActionId, pending.Reflected.MagicId, targetFlags, selected)
        {
            ActorSlot = dispatch.ActorSlot,
            ReflectorSlot = pending.ReflectorSlot,
            RecipientSlot = pending.RecipientSlot,
            TargetProvenance = new(reference.Ordinal, headerGeneration, header.FirstContact.Ordinal, originalRecipients)
            {
                ContactActorIds = recipients,
                ContactSlots = contactSlots
            }
        };
        BattleDiagnosticEffectDispatch linked = dispatch with { Reflection = evidence, ContactActorIds = recipients };
        return BattleDiagnosticReflectionLineage.Matches(linked, source) ? linked : Reject("LINEAGE_FIELDS_REJECTED", source, liveSourceGeneration, evidence);

        BattleDiagnosticEffectDispatch Reject(string reason, BattleDiagnosticEffectDispatch? sourceDispatch = null, long? activeGeneration = null, BattleDiagnosticReflection? evidence = null)
        {
            return dispatch with
            {
                ReflectionRejectionReason = reason,
                ReflectionRejection = new(sourceDispatch?.HeaderReference?.Ordinal, sourceDispatch?.NativeHeaderGeneration, activeGeneration, sourceDispatch?.Serial,
                    diagnosticReflectedCasts?.Count ?? 0, diagnosticReflectionSources?.Count ?? 0, evidence)
            };
        }
    }
}
