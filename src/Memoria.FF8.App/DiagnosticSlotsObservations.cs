using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class DiagnosticSlotsObservations
{
    internal static DiagnosticSlotsObservation Observe(BattleSession battle, Guid instance, string actorId, DiagnosticSlotsSource? source = null)
    {
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == actorId);
        BattleSnapshot snapshot = battle.Capture();
        BattleDiagnosticCombatant native = battle.ObserveDiagnosticCombatants().Single(candidate => candidate.Id == actorId);
        BattleNativeLimitDefinition rules = actor.NativeLimit ?? throw new NotSupportedException("Slots observation requires its actual character Limit owner.");
        if (source is not null && JsonSerializer.Serialize(rules) != JsonSerializer.Serialize(source.Rules))
        {
            throw new InvalidDataException("Slots actor rules changed from the initialized resource source.");
        }
        byte learned = battle.NativeScriptState.LimitSpecialFlagsAfter ?? snapshot.Definition.LimitProgress?.SpecialMagicFlags ?? throw new NotSupportedException("Slots observation requires its initialized saved learned owner.");
        return new(instance, battle.DiagnosticLifecycleId, battle.DiagnosticRestoreCount, battle.DiagnosticResetCount, battle.NativeHostCallbacks ?? battle.Frame, battle.Frame, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, actor.Id, checked((byte)(native.NativeIndex ?? throw new NotSupportedException("Slots actor requires its physical party index."))), actor.ModelId, rules.CharacterId, rules.Command, actor.Level, native.CurrentHp, native.MaximumHp, native.Statuses0, native.Statuses1, battle.GetNativeLimitLevel(actor.Id), learned, battle.ObserveDiagnosticCombatants().Count(candidate => battle.Combatants.Single(member => member.Id == candidate.Id) is { IsPlayer: true, IsActive: true } && (candidate.Statuses0 & 1) != 0), snapshot.Definition.NativeLimitsDisabled ?? throw new NotSupportedException("Slots requires the actual native Limit prohibition field."), snapshot.Definition.LimitOncePerTurn, snapshot.Combatants.Single(candidate => candidate.Id == actorId).LimitTurnCrisis, battle.CanChoosePlayerCommand(actor.Id), actor.IsPlayer && actor.IsAlive && actor.IsActive && battle.Outcome == BattleOutcome.Active, battle.SelphieCardSlots, battle.PendingSlotsDeal is not null, snapshot.Random ?? throw new NotSupportedException("Slots observation requires the actual native battle random owner."), battle.GetNativeSlotsRoll(actor.Id));
    }
}
