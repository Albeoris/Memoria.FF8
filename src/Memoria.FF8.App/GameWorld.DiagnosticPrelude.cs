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
        DiagnosticBattleDriverRecipe driver = request.Driver ?? throw new InvalidDataException("An original allocation prelude requires a focal driver.");
        BattleDiagnosticEnemyAbility? focalEnemy = driver.EnemyAbility;
        DiagnosticTargetExpectation expectedTarget = request.Target ?? throw new InvalidDataException("An original allocation prelude requires a focal target declaration.");
        BattleCombatantDefinition targetSource = active.GetCombatantDefinition(expectedTarget.ActorId);
        bool authoredActivation = !declaration.RequireNativeAllocation && request.Roster is not null;
        string deathHash = Convert.ToHexStringLower(SHA256.HashData(source.NativeAi?.Scripts[3] ?? throw new NotSupportedException("Prelude requires the original predecessor death callback.")));
        if (!attacker.IsPlayer || predecessor.IsPlayer || predecessor.ModelId != declaration.DefeatedModel || predecessor.SceneSlot != declaration.DefeatedSceneSlot || !source.InitialActive || !predecessor.IsAlive || targetSource.ModelId != expectedTarget.Model || targetSource.SceneSlot != expectedTarget.SceneSlot || targetSource.NativeTemplate is null || targetSource.InitialActive == declaration.RequireNativeAllocation || !string.Equals(deathHash, declaration.DeathScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Prelude does not match the original predecessor, target prototype, allocation origin or death-script identity.");
        }
        if (authoredActivation && !targetSource.InitialSuspended)
        {
            throw new InvalidDataException("Prelude activation requires an initially suspended authored successor.");
        }
        int initialHp = predecessor.CurrentHp;
        int predecessorIndex = active.ObserveDiagnosticTarget(predecessor.Id).PhysicalIndex;
        string targetInitializationHash = Convert.ToHexStringLower(SHA256.HashData(targetSource.NativeAi?.Scripts[0] ?? throw new NotSupportedException("Prelude target requires its original initialization script.")));
        long started = Stopwatch.GetTimestamp();
        long startCallback = active.NativeHostCallbacks ?? throw new NotSupportedException("Prelude requires its existing native callback ledger.");
        long restores = active.DiagnosticRestoreCount;
        long resets = active.DiagnosticResetCount;
        List<BattleEvent> events = [];
        IReadOnlyList<BattleEvent>? hostedEvents = active.ExpandedPresentationProcess is null ? null : active.Events;
        int nextHostedEvent = hostedEvents?.Count ?? 0;
        List<BattleDiagnosticEffectDispatch> dispatches = [];
        long ordinaryDamage = 0;
        void ObserveEvent(BattleEvent value)
        {
            if (events.Count >= 16384)
            {
                throw new NotSupportedException("Prelude event evidence exceeded its declared bounded capacity.");
            }
            events.Add(value);
            if (value.Kind is "Damage" or "EffectDamage" && value.Ability == BattleAbility.Attack && value.ActorId == attacker.Id && value.TargetId == predecessor.Id && value.Amount > 0)
            {
                ordinaryDamage += value.Amount;
            }
        }
        void ObserveDispatch(BattleDiagnosticEffectDispatch value)
        {
            if (dispatches.Count >= 4096 || value.RequestedEffect == driver.ExpectedEffect || value.ExecutedEffect == driver.ExpectedEffect)
            {
                throw new NotSupportedException("Prelude cannot warm the focal effect or discard dispatch evidence.");
            }
            dispatches.Add(value);
        }
        DiagnosticAllocationProgress ObserveAllocation()
        {
            BattleDiagnosticTarget[] identities = active.Combatants.Where(actor => !actor.IsPlayer && actor.SceneSlot == expectedTarget.SceneSlot && actor.ModelId == expectedTarget.Model)
                .Select(actor => active.ObserveDiagnosticTarget(actor.Id)).TakeLast(4).ToArray();
            BattleEvent[] spawns = events.Where(value => value.Kind == "NativeSpawn" && value.ActorId == predecessor.Id).TakeLast(4).ToArray();
            return new(identities, spawns);
        }
        if (hostedEvents is null)
        {
            active.DiagnosticEventPublished += ObserveEvent;
        }
        active.DiagnosticEffectDispatched += ObserveDispatch;
        try
        {
            int attacks = 0;
            for (int callbacks = 0; callbacks <= declaration.MaximumCallbacks; callbacks++)
            {
                if (hostedEvents is not null)
                {
                    while (nextHostedEvent < hostedEvents.Count)
                    {
                        ObserveEvent(hostedEvents[nextHostedEvent++]);
                    }
                }
                if (active.Outcome != BattleOutcome.Active || active.DiagnosticRestoreCount != restores || active.DiagnosticResetCount != resets || !ReferenceEquals(active, battle))
                {
                    throw new InvalidOperationException("Prelude lost its uninterrupted active battle or changed a restore/reset owner.");
                }
                bool drained = active.NativeEntryReady && active.ObserveDiagnosticDrainBlockers().Count == 0;
                bool predecessorDefeated = predecessor.CurrentHp == 0 && !predecessor.IsAlive;
                if (drained && (predecessorDefeated || authoredActivation && predecessor.Untargetable))
                {
                    if (declaration.RequireNativeAllocation)
                    {
                        foreach (BattleEvent allocation in events.Where(value => value.Kind == "NativeSpawn" && value.ActorId == predecessor.Id && value.Amount == expectedTarget.SceneSlot))
                        {
                            BattleCombatant allocated = active.Combatants.Single(actor => actor.Id == allocation.TargetId);
                            BattleDiagnosticTarget initialized = active.ObserveDiagnosticTarget(allocated.Id);
                            if (initialized.StatisticsReady)
                            {
                                int levelBand = active.GetCombatantDefinition(allocated.Id).NativeAi?.LevelBand ?? throw new NotSupportedException("The observed allocation lacks its original AI bank.");
                                VerifyInitializedPreludeAllocationIdentity(expectedTarget, initialized, focalEnemy?.LevelBand, levelBand, targetInitializationHash);
                            }
                        }
                    }
                    BattleCombatant[] candidates = active.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.ModelId == expectedTarget.Model && actor.SceneSlot == expectedTarget.SceneSlot).ToArray();
                    if (candidates.Length == 1)
                    {
                        BattleCombatant target = candidates[0];
                        BattleDiagnosticTarget observed = active.ObserveDiagnosticTarget(target.Id);
                        bool spawned = events.Any(value => value.Kind == "NativeSpawn" && value.ActorId == predecessor.Id && value.TargetId == target.Id && value.Amount == expectedTarget.SceneSlot);
                        bool activated = declaration.ActivationInvocation is not { } activation || events.Any(value => value.Kind == "Activated" && value.ActorId == target.Id) &&
                            dispatches.Any(value => value.ActorId == activation.ActorId && value.NativeActorIndex == predecessorIndex && value.NativeCommand == activation.NativeCommand &&
                                value.ActionId == activation.ActionId && value.RequestedEffect == activation.EffectId && value.ExecutedEffect == activation.EffectId &&
                                value.NativeAiCallbackStage == activation.CallbackStage && string.Equals(value.NativeAiScriptSha256, activation.ScriptSha256, StringComparison.OrdinalIgnoreCase));
                        bool originalSuccessor = declaration.RequireNativeAllocation ? spawned : !spawned && activated && target.Id == expectedTarget.ActorId;
                        if (attacks > 0 && ordinaryDamage > 0 && originalSuccessor && observed.Alive && observed.StatisticsReady && observed.PhysicalIndex == request.Target.PhysicalIndex && (observed.Flags & request.Target.FlagsMask) == request.Target.FlagsValue)
                        {
                            NativeBattleAiDefinition ai = active.GetCombatantDefinition(target.Id).NativeAi ?? throw new NotSupportedException("Prelude target lacks its original initialized AI bank.");
                            if (focalEnemy is not null && ai.LevelBand != focalEnemy.LevelBand)
                            {
                                throw new InvalidDataException("Native phase allocation did not preserve the declared original ability level band.");
                            }
                            if (focalEnemy is null || request.AtbMode != BattleAtbMode.TurnBased || active.TryHandoffDiagnosticReadinessActor(declaration.Input.ActorId!, target.Id))
                            {
                                observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker, boundary: true, observeAllocation: ObserveAllocation);
                                BattleInput resolvedInput = driver.Input with { ActorId = focalEnemy is null ? driver.Input.ActorId : target.Id, TargetId = driver.Input.TargetId == expectedTarget.ActorId ? target.Id : driver.Input.TargetId };
                                DiagnosticTargetExpectation resolvedTarget = expectedTarget with { ActorId = target.Id };
                                DiagnosticBattleRequest resolved = request with { Target = resolvedTarget, Driver = driver with { Input = resolvedInput } };
                                string readinessScope = focalEnemy is null ? "The declared player retains the existing diagnostic readiness reference." : request.AtbMode == BattleAtbMode.TurnBased ? "Only the diagnostic readiness reference is handed from the declared player to the proved initialized focal owner at the existing idle TurnBased boundary." : "Declared Normal scheduling is retained.";
                                if (request.Roster is not null && declaration.RequireNativeAllocation && predecessor.IsActive)
                                {
                                    throw new InvalidDataException("The native predecessor must retire before the expanded focal roster is measured.");
                                }
                                diagnosticBattle = diagnosticBattle! with { Request = resolved };
                                DiagnosticReceipt = DiagnosticReceipt! with
                                {
                                    Prelude = new(declaration, DiagnosticReceipt.BattleInstanceId, active.DiagnosticLifecycleId, startCallback, active.NativeHostCallbacks!.Value, restores, resets, attacks, Stopwatch.GetElapsedTime(started).TotalMilliseconds, predecessorIndex, initialHp, predecessor.CurrentHp, driver.Input, resolvedInput, expectedTarget, resolvedTarget, observed, events.ToArray(), dispatches.ToArray(), $"Bounded declared ordinary Attack preparation through original death AI and activation/allocation. {readinessScope} Native callbacks and ATB use their existing admission rules; no HP, participant flags, RNG, clock or snapshot is restored. Full preparation wall time remains overhead.")
                                    {
                                        PredecessorFlagsAfter = predecessor.NativeCombatantFlags
                                    }
                                };
                                return resolved;
                            }
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
                    observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker, observeAllocation: ObserveAllocation);
                }
            }
            observer.Report("ordinary_attack_prelude", attacks, ordinaryDamage, predecessor, attacker, boundary: true, observeAllocation: ObserveAllocation);
            throw new NotSupportedException("Ordinary command prelude did not reach the original initialized target within its callback budget.");
        }
        finally
        {
            if (hostedEvents is null)
            {
                active.DiagnosticEventPublished -= ObserveEvent;
            }
            active.DiagnosticEffectDispatched -= ObserveDispatch;
        }
    }

    internal static void VerifyInitializedPreludeAllocationIdentity(DiagnosticTargetExpectation expected, BattleDiagnosticTarget observed, int? expectedLevelBand, int actualLevelBand, string expectedInitializationHash)
    {
        if (!observed.StatisticsReady)
        {
            return;
        }
        if (observed.PhysicalIndex != expected.PhysicalIndex || observed.Model != expected.Model || observed.SceneSlot != expected.SceneSlot || observed.Stage != expected.Stage || expectedLevelBand is int band && actualLevelBand != band || !string.Equals(observed.InitializationScriptSha256, expectedInitializationHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The completed original native allocation contradicts the declared physical owner, model, scene, level band or initialization script.");
        }
    }
}
