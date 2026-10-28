using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticReflection(uint ProducerAddress, long SourceDispatchSerial, int SourceHeaderIndex, long SourceHeaderGeneration, int ReflectedHeaderIndex, long ReflectedHeaderGeneration, string ActorId, byte ActorIndex, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, byte SourceCommand, ushort SourceActionId, ushort SourceEffect, byte SourceAttackType, byte SourceAttackFlags, ushort ReflectedActionId, ushort ReflectedEffect, byte ReflectedTargetFlags, ushort ReflectedTargetMask)
{
    public BattleDiagnosticReflectionTargetProvenance? TargetProvenance { get; init; }
}
public sealed record BattleDiagnosticReflectionTargetResolution(ushort InputMask, ushort SelectedMask, byte Modifiers, ushort AvailableMask, bool DisableAutomaticReplacement, ushort ResultMask);
public sealed record BattleDiagnosticReflectionTargetProvenance(uint ProducerAddress, int HeaderIndex, long HeaderGeneration, int FirstContactIndex, ImmutableArray<BattleDiagnosticReflectionTargetResolution> Resolutions, ImmutableArray<byte> ContactRecipients);
public sealed record BattleDiagnosticReflectionRejection(int? SourceHeaderIndex, long? ExpectedSourceGeneration, long? ActualSourceGeneration, long? SourceDispatchSerial, int PendingHeaderCount, int ObservedHeaderCount, BattleDiagnosticReflection? Candidate);

public static class BattleDiagnosticReflectionLineage
{
    public static bool Matches(BattleDiagnosticEffectDispatch child, BattleDiagnosticEffectDispatch source)
    {
        if (child.Reflection is not { } proof || source.Reflection is not null || proof.ProducerAddress != 0x487DF0 || proof.SourceDispatchSerial != source.Serial || source.Serial >= child.Serial || source.Callback > child.Callback || string.IsNullOrWhiteSpace(proof.ActorId) || proof.SourceEffect == 0 || proof.ReflectedEffect == 0 || proof.ActorIndex >= 7 || proof.ReflectorIndex >= 7 || proof.RecipientIndex >= 7 || proof.SourceHeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || proof.ReflectedHeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || proof.SourceHeaderGeneration <= 0 || proof.ReflectedHeaderGeneration <= proof.SourceHeaderGeneration)
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
            return child.NativeTargetMask == selectedMask;
        }
        if (targets.ProducerAddress != 0x48E830 || targets.HeaderIndex != proof.ReflectedHeaderIndex || targets.HeaderGeneration != proof.ReflectedHeaderGeneration || targets.FirstContactIndex is < 0 or >= NativeBattleActionPacketsState.ContactCapacity || targets.Resolutions.IsDefaultOrEmpty || targets.Resolutions.Length > NativeBattleActionPacketsState.ContactCapacity || targets.ContactRecipients.IsDefaultOrEmpty || targets.ContactRecipients.Length > NativeBattleActionPacketsState.ContactCapacity - targets.FirstContactIndex)
        {
            return false;
        }
        ushort resolved = 0;
        foreach (BattleDiagnosticReflectionTargetResolution resolution in targets.Resolutions)
        {
            if (resolution is null)
            {
                return false;
            }
            int modifiers = selectedMask >> 13;
            int selected = selectedMask & 0x1FFF;
            if ((modifiers & 1) != 0)
            {
                selected = resolution.SelectedMask;
                modifiers &= ~4;
                if (selected != 0 && ((selected & (selected - 1)) != 0 || (selected & (proof.RecipientIndex < 3 ? 7 : 120)) == 0))
                {
                    return false;
                }
            }
            if (resolution.InputMask != selectedMask || resolution.SelectedMask != selected || resolution.Modifiers != modifiers || (resolution.AvailableMask & ~0x7F) != 0)
            {
                return false;
            }
            (int result, _) = NativeBattleActionTargetMask.Resolve(selected, modifiers, resolution.AvailableMask, resolution.DisableAutomaticReplacement);
            if (resolution.ResultMask != result || (result & ~0x7F) != 0)
            {
                return false;
            }
            resolved |= resolution.ResultMask;
        }
        ushort contacts = 0;
        foreach (byte recipient in targets.ContactRecipients)
        {
            if (recipient >= 7 || (recipient < 3) == (proof.ReflectorIndex < 3))
            {
                return false;
            }
            contacts |= (ushort)(1 << recipient);
        }
        return contacts == resolved && child.NativeTargetMask == contacts;
    }
}

public sealed partial class BattleSession
{
    private sealed record PendingDiagnosticReflection(int SourceHeader, long SourceGeneration, string ActorId, byte ActorIndex, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, byte SourceCommand, ushort SourceActionId, BattleNativeAction Source, BattleNativeAction Reflected, ushort TargetMask)
    {
        internal List<BattleDiagnosticReflectionTargetResolution>? Resolutions { get; set; }
        internal BattleDiagnosticReflectionTargetProvenance? PublishedTargets { get; set; }
    }
    private ConditionalWeakTable<BattleAttackCommand, PendingDiagnosticReflection>? diagnosticReflectedCommands;
    private Dictionary<int, long>? diagnosticHeaderGenerations;
    private Dictionary<int, BattleDiagnosticEffectDispatch>? diagnosticHeaderDispatches;
    private Dictionary<int, PendingDiagnosticReflection>? diagnosticReflectedHeaders;
    private PendingDiagnosticReflection? diagnosticReflectionProduction;
    private long diagnosticHeaderGeneration;

    private void ResetDiagnosticReflectionHeaders()
    {
        if (diagnosticHeaderGenerations is null)
        {
            return;
        }
        diagnosticHeaderGenerations.Clear();
        diagnosticHeaderDispatches?.Clear();
        diagnosticReflectedHeaders?.Clear();
        diagnosticReflectedCommands?.Clear();
        diagnosticReflectionProduction = null;
    }

    private void ObserveDiagnosticPreparedHeader(int header)
    {
        if (DiagnosticEffectDispatched is null)
        {
            if (diagnosticHeaderGenerations is not null)
            {
                diagnosticHeaderGenerations = null;
                diagnosticHeaderDispatches = null;
                diagnosticReflectedHeaders = null;
                diagnosticReflectedCommands = null;
                diagnosticReflectionProduction = null;
            }
            return;
        }
        diagnosticHeaderGenerations ??= [];
        diagnosticHeaderGenerations[header] = ++diagnosticHeaderGeneration;
        diagnosticHeaderDispatches?.Remove(header);
        diagnosticReflectedHeaders?.Remove(header);
        if (diagnosticReflectionProduction is { } proof && nativeActionPackets.Headers[header].ReadByte(1) == 247)
        {
            diagnosticReflectedHeaders ??= [];
            diagnosticReflectedHeaders[header] = proof;
        }
    }

    private PendingDiagnosticReflection? PrepareDiagnosticReflection(int header, BattleCombatant actor, BattleCombatant reflector, BattleCombatant recipient, BattleNativeAction source, BattleNativeAction reflected, ushort targetMask)
    {
        if (DiagnosticEffectDispatched is null || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long generation))
        {
            return null;
        }
        NativeBattleActionHeaderSlot original = nativeActionPackets.Headers[header];
        return new(header, generation, actor.Id, checked((byte)actor.NativeIndex), reflector.Id, checked((byte)reflector.NativeIndex), reflector.NativeStatus1, recipient.Id, checked((byte)recipient.NativeIndex), original.ReadByte(1), (ushort)(original.ReadByte(4) | original.ReadByte(5) << 8), source, reflected, targetMask);
    }

    private void ObserveDiagnosticReflectionTargetResolution(ushort input, int selected, int modifiers, int available, int result)
    {
        if (diagnosticReflectionProduction is not { } pending || nativeActionScratch.Command != 247)
        {
            return;
        }
        pending.Resolutions ??= [];
        if (pending.Resolutions.Count < NativeBattleActionPacketsState.ContactCapacity)
        {
            pending.Resolutions.Add(new(input, checked((ushort)selected), checked((byte)modifiers), checked((ushort)available), nativeScriptState.DisableAutomaticTargetReplacement, checked((ushort)result)));
        }
    }

    private void ObserveDiagnosticPublishedReflectionHeader(int header)
    {
        if (diagnosticReflectedHeaders is not { } reflectedHeaders || !reflectedHeaders.TryGetValue(header, out var pending) || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long generation))
        {
            return;
        }
        NativeBattleActionHeaderSlot slot = nativeActionPackets.Headers[header];
        int firstContact = slot.FirstContactIndex!.Value;
        int count = slot.ReadByte(16);
        ImmutableArray<byte>.Builder recipients = ImmutableArray.CreateBuilder<byte>(count);
        for (int index = 0; index < count; index++)
        {
            recipients.Add(nativeActionPackets.ReadContact(firstContact + index).Primary.ActorIndex);
        }
        pending.PublishedTargets = new(0x48E830, header, generation, firstContact, pending.Resolutions?.ToImmutableArray() ?? [], recipients.MoveToImmutable());
    }

    private BattleDiagnosticEffectDispatch CompleteDiagnosticReflection(BattleDiagnosticEffectDispatch dispatch)
    {
        if (dispatch.NativeHeaderIndex is not int header || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long generation))
        {
            return RejectDiagnosticReflection(dispatch, "CHILD_HEADER_GENERATION_MISSING");
        }
        dispatch = dispatch with { NativeHeaderGeneration = generation };
        if (dispatch.NativeCommand == 247)
        {
            if (diagnosticReflectedHeaders is not { } reflectedHeaders || !reflectedHeaders.Remove(header, out var pending))
            {
                dispatch = RejectDiagnosticReflection(dispatch, "PENDING_HEADER_ABSENT");
            }
            else if (!generations.TryGetValue(pending.SourceHeader, out long sourceGeneration))
            {
                dispatch = RejectDiagnosticReflection(dispatch, "SOURCE_HEADER_GENERATION_MISSING", pending);
            }
            else if (sourceGeneration != pending.SourceGeneration)
            {
                dispatch = RejectDiagnosticReflection(dispatch, "SOURCE_HEADER_GENERATION_STALE", pending, sourceGeneration);
            }
            else if (diagnosticHeaderDispatches is not { } dispatches || !dispatches.TryGetValue(pending.SourceHeader, out var source))
            {
                dispatch = RejectDiagnosticReflection(dispatch, "SOURCE_DISPATCH_MISSING", pending, sourceGeneration);
            }
            else
            {
                BattleDiagnosticReflection proof = new(0x487DF0, source.Serial, pending.SourceHeader, pending.SourceGeneration, header, generation, pending.ActorId, pending.ActorIndex, pending.ReflectorId, pending.ReflectorIndex, pending.ReflectorStatuses1, pending.RecipientId, pending.RecipientIndex, pending.SourceCommand, pending.SourceActionId, pending.Source.MagicId, pending.Source.AttackType, pending.Source.AttackFlags, pending.Reflected.ActionId, pending.Reflected.MagicId, pending.Reflected.TargetFlags.GetValueOrDefault(), pending.TargetMask) { TargetProvenance = pending.PublishedTargets };
                BattleDiagnosticEffectDispatch candidate = dispatch with { Reflection = proof };
                dispatch = BattleDiagnosticReflectionLineage.Matches(candidate, source) ? candidate : RejectDiagnosticReflection(dispatch, "LINEAGE_FIELDS_REJECTED", pending, sourceGeneration, source.Serial, proof);
            }
        }
        diagnosticHeaderDispatches ??= [];
        diagnosticHeaderDispatches[header] = dispatch;
        return dispatch;
    }

    private BattleDiagnosticEffectDispatch RejectDiagnosticReflection(BattleDiagnosticEffectDispatch dispatch, string reason, PendingDiagnosticReflection? pending = null, long? sourceGeneration = null, long? sourceSerial = null, BattleDiagnosticReflection? candidate = null)
    {
        if (dispatch.NativeCommand != 247)
        {
            return dispatch;
        }
        return dispatch with { ReflectionRejectionReason = reason, ReflectionRejection = new(pending?.SourceHeader, pending?.SourceGeneration, sourceGeneration, sourceSerial, diagnosticReflectedHeaders?.Count ?? 0, diagnosticHeaderDispatches?.Count ?? 0, candidate) };
    }
}
