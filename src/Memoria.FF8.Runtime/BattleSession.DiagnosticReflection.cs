using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticReflection(uint ProducerAddress, long SourceDispatchSerial, int SourceHeaderIndex, long SourceHeaderGeneration, int ReflectedHeaderIndex, long ReflectedHeaderGeneration, string ActorId, byte ActorIndex, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, byte SourceCommand, ushort SourceActionId, ushort SourceEffect, byte SourceAttackType, byte SourceAttackFlags, ushort ReflectedActionId, ushort ReflectedEffect, byte ReflectedTargetFlags, ushort ReflectedTargetMask)
{
    public BattleDiagnosticReflectionTargetProvenance? TargetProvenance { get; init; }
}
public sealed record BattleDiagnosticReflectionTargetProvenance(int HeaderIndex, long HeaderGeneration, int FirstContactIndex, ImmutableArray<byte> ContactRecipients);
public sealed record BattleDiagnosticReflectionRejection(int? SourceHeaderIndex, long? ExpectedSourceGeneration, long? ActualSourceGeneration, long? SourceDispatchSerial, int PendingHeaderCount, int ObservedHeaderCount, BattleDiagnosticReflection? Candidate);

public static class BattleDiagnosticReflectionLineage
{
    public static bool Matches(BattleDiagnosticEffectDispatch child, BattleDiagnosticEffectDispatch source)
    {
        if (child.Reflection is not { } proof || source.Reflection is not null || proof.ProducerAddress != 0x487DF0 || proof.SourceDispatchSerial != source.Serial || source.Serial >= child.Serial || source.Callback > child.Callback || string.IsNullOrWhiteSpace(proof.ActorId) || proof.SourceEffect == 0 || proof.ReflectedEffect == 0 || proof.ActorIndex >= 7 || proof.ReflectorIndex >= 7 || proof.RecipientIndex >= 7 || proof.SourceHeaderIndex is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || proof.ReflectedHeaderIndex is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || proof.SourceHeaderGeneration <= 0 || proof.ReflectedHeaderGeneration <= proof.SourceHeaderGeneration)
        {
            return false;
        }
        if (source.NativeHeaderIndex != proof.SourceHeaderIndex || source.NativeHeaderGeneration != proof.SourceHeaderGeneration || child.NativeHeaderIndex != proof.ReflectedHeaderIndex || child.NativeHeaderGeneration != proof.ReflectedHeaderGeneration || source.NativeActorIndex != proof.ActorIndex || child.NativeActorIndex != proof.ActorIndex || source.ActorId != proof.ActorId || child.ActorId != proof.ActorId || string.IsNullOrWhiteSpace(proof.ReflectorId) || string.IsNullOrWhiteSpace(proof.RecipientId))
        {
            return false;
        }
        if (proof.SourceCommand == 247 || source.NativeCommand != proof.SourceCommand || source.ActionId != proof.SourceActionId || source.RequestedEffect != proof.SourceEffect || source.ExecutedEffect != proof.SourceEffect || source.NativeTargetMask is not ushort sourceMask || (sourceMask & (1 << proof.ReflectorIndex)) == 0 || (proof.ReflectorStatuses1 & 0x80) == 0 || (proof.SourceAttackFlags & 0x10) == 0 || proof.SourceAttackType is not (2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33))
        {
            return false;
        }
        ushort mask = (ushort)((1 << proof.RecipientIndex) | ((proof.ReflectedTargetFlags & 1) != 0 ? 0x4000 : 0) | ((proof.ReflectedTargetFlags & 2) != 0 ? 0x2000 : 0));
        return (proof.ReflectorIndex < 3) != (proof.RecipientIndex < 3) && child.Ability == (proof.ActorIndex < 3 ? BattleAbility.Magic : BattleAbility.EnemyAbility) && child.NativeCommand == 247 && proof.ReflectedActionId == unchecked((byte)proof.SourceActionId) && child.ActionId == proof.ReflectedActionId && child.RequestedEffect == proof.ReflectedEffect && child.ExecutedEffect == proof.ReflectedEffect && proof.ReflectedTargetMask == mask && MatchesTargets(child, proof, mask);
    }

    private static bool MatchesTargets(BattleDiagnosticEffectDispatch child, BattleDiagnosticReflection proof, ushort selectedMask)
    {
        if (proof.TargetProvenance is not { } targets)
        {
            return child.NativeTargetMask == (selectedMask & 0x7F);
        }
        if (targets.HeaderIndex != proof.ReflectedHeaderIndex || targets.HeaderGeneration != proof.ReflectedHeaderGeneration || targets.FirstContactIndex is < 0 or >= NativeBattleRetainedActionPackets.ContactCapacity || targets.ContactRecipients.IsDefaultOrEmpty || targets.ContactRecipients.Length > NativeBattleRetainedActionPackets.ContactCapacity - targets.FirstContactIndex)
        {
            return false;
        }
        ushort contacts = 0;
        foreach (byte recipient in targets.ContactRecipients)
        {
            if (recipient >= 7 || (recipient < 3) == (proof.ReflectorIndex < 3))
            {
                return false;
            }
            contacts |= checked((ushort)(1 << recipient));
        }
        return child.NativeTargetMask == contacts;
    }
}

public sealed partial class BattleSession
{
    private sealed record DiagnosticReflectedCast(BattleActionPresentation Source, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, BattleNativeAction Original, BattleNativeAction Reflected);
    private bool trackDiagnosticReflections;
    private Dictionary<BattleActionPresentation, DiagnosticReflectedCast>? diagnosticReflectedCasts;
    private Dictionary<BattleActionPresentation, BattleDiagnosticEffectDispatch>? diagnosticReflectionSources;

    private static int? OriginalDiagnosticHeaderIndex(BattleActionHeaderReference? header)
    {
        return header is { } reference && reference.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? reference.Ordinal : null;
    }

    private void RecordDiagnosticReflection(BattleActionPresentation source, BattleActionPresentation reflected, BattleCombatant reflector, BattleCombatant recipient, BattleNativeAction original)
    {
        if (!trackDiagnosticReflections || reflector.NativeActorIndex is not int reflectorIndex || recipient.NativeActorIndex is not int recipientIndex)
        {
            return;
        }
        diagnosticReflectedCasts ??= new(ReferenceEqualityComparer.Instance);
        diagnosticReflectedCasts.Add(reflected, new(source, reflector.Id, checked((byte)reflectorIndex), reflector.NativeStatus1, recipient.Id, checked((byte)recipientIndex), original,
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
            source.NativeHeaderIndex is not int sourceHeader || source.NativeHeaderGeneration is not long sourceGeneration)
        {
            return Reject("SOURCE_DISPATCH_MISSING");
        }
        if (!staged.Values.Any(proof => ReferenceEquals(proof.Source, pending.Source)))
        {
            sources.Remove(pending.Source);
        }
        long? liveSourceGeneration = process.ReadDiagnosticCounterHeaderGeneration(sourceHeader);
        if (liveSourceGeneration != sourceGeneration)
        {
            return Reject("SOURCE_HEADER_GENERATION_STALE", source, liveSourceGeneration);
        }
        if (dispatch.NativeHeaderIndex is not int headerIndex || dispatch.NativeHeaderGeneration is not long headerGeneration ||
            dispatch.NativeActorIndex is not byte actorIndex || dispatch.ActorId is not { } actorId || observation.Header is not { } header ||
            source.NativeCommand is not byte sourceCommand || source.ActionId is not int sourceAction || header.FirstContact.Ordinal >= NativeBattleContactAddress.NativeRowCount)
        {
            return Reject("CHILD_HEADER_GENERATION_MISSING", source, liveSourceGeneration);
        }
        NativeBattleActionPacketsState packets = process.ActionPackets;
        int count = header.ReadContactCount();
        ImmutableArray<byte>.Builder recipients = ImmutableArray.CreateBuilder<byte>(count);
        for (int index = 0; index < count; index++)
        {
            BattleContactCursor cursor = header.FirstContact.Advance(index);
            if (cursor.Ordinal >= NativeBattleContactAddress.NativeRowCount || !packets.ModelAliases.TryEncodeExact(packets.ReadContact(cursor).Primary.Recipient, out byte originalRecipient))
            {
                return Reject("CHILD_CONTACT_NOT_ORIGINAL", source, liveSourceGeneration);
            }
            recipients.Add(originalRecipient);
        }
        byte targetFlags = pending.Reflected.TargetFlags.GetValueOrDefault();
        ushort selected = checked((ushort)((1 << pending.RecipientIndex) | ((targetFlags & 1) != 0 ? 0x4000 : 0) | ((targetFlags & 2) != 0 ? 0x2000 : 0)));
        BattleDiagnosticReflection evidence = new(0x487DF0, source.Serial, sourceHeader, sourceGeneration, headerIndex, headerGeneration, actorId, actorIndex, pending.ReflectorId,
            pending.ReflectorIndex, pending.ReflectorStatuses1, pending.RecipientId, pending.RecipientIndex, sourceCommand, checked((ushort)sourceAction), pending.Original.MagicId,
            pending.Original.AttackType, pending.Original.AttackFlags, pending.Reflected.ActionId, pending.Reflected.MagicId, targetFlags, selected)
        {
            TargetProvenance = new(headerIndex, headerGeneration, header.FirstContact.Ordinal, recipients.MoveToImmutable())
        };
        BattleDiagnosticEffectDispatch linked = dispatch with { Reflection = evidence };
        return BattleDiagnosticReflectionLineage.Matches(linked, source) ? linked : Reject("LINEAGE_FIELDS_REJECTED", source, liveSourceGeneration, evidence);

        BattleDiagnosticEffectDispatch Reject(string reason, BattleDiagnosticEffectDispatch? sourceDispatch = null, long? activeGeneration = null, BattleDiagnosticReflection? evidence = null)
        {
            return dispatch with
            {
                ReflectionRejectionReason = reason,
                ReflectionRejection = new(sourceDispatch?.NativeHeaderIndex, sourceDispatch?.NativeHeaderGeneration, activeGeneration, sourceDispatch?.Serial,
                    diagnosticReflectedCasts?.Count ?? 0, diagnosticReflectionSources?.Count ?? 0, evidence)
            };
        }
    }
}
