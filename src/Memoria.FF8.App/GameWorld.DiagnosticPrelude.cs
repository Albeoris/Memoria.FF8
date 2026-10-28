using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private DiagnosticBattleRequest PrepareDiagnosticPrelude(DiagnosticBattleRequest request, BattleSession active, DiagnosticPreparationProgressObserver observer)
    {
        DiagnosticBattlePrelude declaration = request.Prelude ?? throw new InvalidOperationException("No ordinary attack prelude was declared.");
        declaration.Validate(request);
        BattleCombatant attacker = active.Combatants.Single(actor => actor.Id == declaration.Input.ActorId);
        BattleCombatant predecessor = active.Combatants.Single(actor => actor.Id == declaration.Input.TargetId);
        BattleCombatantDefinition source = active.GetCombatantDefinition(predecessor.Id);
        BattleCombatantDefinition targetSource = active.GetCombatantDefinition(request.Target!.ActorId);
        string deathHash = Convert.ToHexStringLower(SHA256.HashData(source.NativeAi?.Scripts[3] ?? throw new NotSupportedException("Prelude requires the original predecessor death callback.")));
        if (!attacker.IsPlayer || predecessor.IsPlayer || predecessor.ModelId != declaration.DefeatedModel || predecessor.SceneSlot != declaration.DefeatedSceneSlot || !source.InitialActive || !predecessor.IsAlive || targetSource.ModelId != request.Target.Model || targetSource.SceneSlot != request.Target.SceneSlot || targetSource.NativeTemplate is null || targetSource.InitialActive == declaration.RequireNativeAllocation || !string.Equals(deathHash, declaration.DeathScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Prelude does not match the original predecessor, target prototype, allocation origin or death-script identity.");
        }
        int initialHp = predecessor.CurrentHp;
        int predecessorIndex = active.ObserveDiagnosticTarget(predecessor.Id).PhysicalIndex;
        if (declaration.RequireNativeAllocation && request.Target.PhysicalIndex != predecessorIndex)
        {
            throw new InvalidDataException("The declared native allocation must reuse the original predecessor's actual physical slot; its observed allocation must verify that choice.");
        }
        long started = Stopwatch.GetTimestamp();
        long startCallback = active.NativeHostCallbacks ?? throw new NotSupportedException("Prelude requires its existing native callback ledger.");
        long restores = active.DiagnosticRestoreCount;
        long resets = active.DiagnosticResetCount;
        List<BattleEvent> events = [];
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        long ordinaryDamage = 0;
        void ObserveEvent(BattleEvent value)
        {
            if (events.Count >= 16384)
            {
                throw new NotSupportedException("Prelude event evidence exceeded its declared bounded capacity.");
            }
            events.Add(value);
            if (value.Kind == "Damage" && value.ActorId == attacker.Id && value.TargetId == predecessor.Id && value.Amount > 0)
            {
                ordinaryDamage += value.Amount;
            }
        }
        void ObserveDispatch(BattleDiagnosticEffectDispatch value)
        {
            if (dispatches.Count >= 4096 || value.RequestedEffect == request.Driver!.ExpectedEffect || value.ExecutedEffect == request.Driver.ExpectedEffect)
            {
                throw new NotSupportedException("Prelude cannot warm the focal effect or discard dispatch evidence.");
            }
            dispatches.Add(value);
        }
        active.DiagnosticEventPublished += ObserveEvent;
        active.DiagnosticEffectDispatched += ObserveDispatch;
        try
        {
            int attacks = 0;
            for (int callbacks = 0; callbacks <= declaration.MaximumCallbacks; callbacks++)
            {
                if (active.Outcome != BattleOutcome.Active || active.DiagnosticRestoreCount != restores || active.DiagnosticResetCount != resets || !ReferenceEquals(active, battle))
                {
                    throw new InvalidOperationException("Prelude lost its uninterrupted active battle or changed a restore/reset owner.");
                }
                bool drained = active.NativeEntryReady && active.ObserveDiagnosticDrainBlockers().Count == 0;
                if (drained && predecessor.CurrentHp == 0 && !predecessor.IsAlive)
                {
                    BattleCombatant[] candidates = active.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.ModelId == request.Target.Model && actor.SceneSlot == request.Target.SceneSlot).ToArray();
                    if (candidates.Length == 1)
                    {
                        BattleCombatant target = candidates[0];
                        BattleDiagnosticTarget observed = active.ObserveDiagnosticTarget(target.Id);
                        bool spawned = events.Any(value => value.Kind == "NativeSpawn" && value.ActorId == predecessor.Id && value.TargetId == target.Id && value.Amount == request.Target.SceneSlot);
                        bool damaged = events.Any(value => value.Kind == "Damage" && value.ActorId == attacker.Id && value.TargetId == predecessor.Id && value.Amount > 0);
                        if (attacks > 0 && damaged && spawned == declaration.RequireNativeAllocation && (declaration.RequireNativeAllocation || target.Id == request.Target.ActorId) && observed.Alive && observed.StatisticsReady && observed.PhysicalIndex == request.Target.PhysicalIndex && (observed.Flags & request.Target.FlagsMask) == request.Target.FlagsValue)
                        {
                            observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker, boundary: true);
                            NativeBattleAiDefinition ai = active.GetCombatantDefinition(target.Id).NativeAi ?? throw new NotSupportedException("Prelude target lacks its original initialized AI bank.");
                            if (ai.LevelBand != request.Driver!.EnemyAbility!.LevelBand)
                            {
                                throw new InvalidDataException("Native phase allocation did not preserve the declared original ability level band.");
                            }
                            BattleInput resolvedInput = request.Driver.Input with { ActorId = target.Id, TargetId = request.Driver.Input.TargetId == request.Target.ActorId ? target.Id : request.Driver.Input.TargetId };
                            DiagnosticTargetExpectation resolvedTarget = request.Target with { ActorId = target.Id };
                            DiagnosticBattleRequest resolved = request with { Target = resolvedTarget, Driver = request.Driver with { Input = resolvedInput } };
                            diagnosticBattle = diagnosticBattle! with { Request = resolved };
                            DiagnosticReceipt = DiagnosticReceipt! with { Prelude = new(declaration, DiagnosticReceipt.BattleInstanceId, active.DiagnosticLifecycleId, startCallback, active.NativeHostCallbacks!.Value, restores, resets, attacks, Stopwatch.GetElapsedTime(started).TotalMilliseconds, predecessorIndex, initialHp, predecessor.CurrentHp, request.Driver.Input, resolvedInput, request.Target, resolvedTarget, observed, events.ToArray(), dispatches.ToArray(), "Bounded declared ordinary Attack preparation through original death AI and activation/allocation. Native callbacks and ATB advance normally; no HP, participant flags, RNG, snapshot or scheduler owner is restored. Full preparation wall time remains overhead.") };
                            return resolved;
                        }
                    }
                }
                if (drained && predecessor.IsAlive && (predecessor.NativeCombatantFlags & 0xC3) == 1 && active.CanChoosePlayerCommand(attacker.Id))
                {
                    if (attacks == declaration.MaximumAttacks)
                    {
                        throw new NotSupportedException("Declared ordinary attacks did not reach the native phase boundary within their attack budget.");
                    }
                    ExecuteBattleCommand(active, declaration.Input);
                    attacks++;
                }
                if (callbacks != declaration.MaximumCallbacks)
                {
                    active.Tick();
                    observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker);
                }
            }
            observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker, boundary: true);
            throw new NotSupportedException("Ordinary command prelude did not reach the original initialized target within its callback budget.");
        }
        finally
        {
            active.DiagnosticEventPublished -= ObserveEvent;
            active.DiagnosticEffectDispatched -= ObserveDispatch;
        }
    }
}
