using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticReflection(uint ProducerAddress, long SourceDispatchSerial, int SourceHeaderIndex, long SourceHeaderGeneration, int ReflectedHeaderIndex, long ReflectedHeaderGeneration, string ActorId, byte ActorIndex, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, byte SourceCommand, ushort SourceActionId, ushort SourceEffect, byte SourceAttackType, byte SourceAttackFlags, ushort ReflectedActionId, ushort ReflectedEffect, byte ReflectedTargetFlags, ushort ReflectedTargetMask);

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
        return (proof.ReflectorIndex < 3) != (proof.RecipientIndex < 3) && child.Ability == (proof.ActorIndex < 3 ? BattleAbility.Magic : BattleAbility.EnemyAbility) && child.NativeCommand == 247 && proof.ReflectedActionId == unchecked((byte)proof.SourceActionId) && child.ActionId == proof.ReflectedActionId && child.RequestedEffect == proof.ReflectedEffect && child.ExecutedEffect == proof.ReflectedEffect && proof.ReflectedTargetMask == mask && child.NativeTargetMask == mask;
    }
}

public sealed partial class BattleSession
{
    private sealed record PendingDiagnosticReflection(int SourceHeader, long SourceGeneration, string ActorId, byte ActorIndex, string ReflectorId, byte ReflectorIndex, uint ReflectorStatuses1, string RecipientId, byte RecipientIndex, byte SourceCommand, ushort SourceActionId, BattleNativeAction Source, BattleNativeAction Reflected, ushort TargetMask);
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
        if (diagnosticReflectionProduction is { } proof)
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

    private BattleDiagnosticEffectDispatch CompleteDiagnosticReflection(BattleDiagnosticEffectDispatch dispatch)
    {
        if (dispatch.NativeHeaderIndex is not int header || diagnosticHeaderGenerations is not { } generations || !generations.TryGetValue(header, out long generation))
        {
            return dispatch;
        }
        dispatch = dispatch with { NativeHeaderGeneration = generation };
        if (diagnosticReflectedHeaders is { } reflectedHeaders && reflectedHeaders.Remove(header, out var pending) && generations.TryGetValue(pending.SourceHeader, out long sourceGeneration) && sourceGeneration == pending.SourceGeneration && diagnosticHeaderDispatches is { } dispatches && dispatches.TryGetValue(pending.SourceHeader, out var source))
        {
            BattleDiagnosticReflection proof = new(0x487DF0, source.Serial, pending.SourceHeader, pending.SourceGeneration, header, generation, pending.ActorId, pending.ActorIndex, pending.ReflectorId, pending.ReflectorIndex, pending.ReflectorStatuses1, pending.RecipientId, pending.RecipientIndex, pending.SourceCommand, pending.SourceActionId, pending.Source.MagicId, pending.Source.AttackType, pending.Source.AttackFlags, pending.Reflected.ActionId, pending.Reflected.MagicId, pending.Reflected.TargetFlags.GetValueOrDefault(), pending.TargetMask);
            BattleDiagnosticEffectDispatch candidate = dispatch with { Reflection = proof };
            if (BattleDiagnosticReflectionLineage.Matches(candidate, source))
            {
                dispatch = candidate;
            }
        }
        diagnosticHeaderDispatches ??= [];
        diagnosticHeaderDispatches[header] = dispatch;
        return dispatch;
    }
}
