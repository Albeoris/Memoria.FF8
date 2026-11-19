using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleComponentSourceTests
{
    internal sealed record NaturalRenzPreludeFact(long Callback, string Boundary, string? Actor, int? Model, int? HitPoints, int? Action);
    internal sealed record NaturalRenzPreludeReceipt(ushort Effect, int Attacks, int Defeated, long Callbacks, double CpuMilliseconds, NaturalRenzPreludeFact[] Facts);

    internal static NaturalRenzPreludeReceipt AdvanceOriginalRenz335337Prelude(GameWorld world, BattleSession source, ushort effect)
    {
        (int formation, int targetModel) = effect switch
        {
            335 => (63, 77),
            337 => (813, 117),
            _ => throw new InvalidDataException("The selected component has no original activation prelude.")
        };
        if (!ReferenceEquals(world.Battle, source) || source.ReadDefinition().EncounterId != formation || !source.ReadDefinition().UsesOriginalEnemySlots || !source.NativeEntryReady)
        {
            throw new InvalidDataException("The natural prelude requires its initialized full original encounter.");
        }
        BattleCombatant squall = source.Combatants.Single(actor => actor.IsPlayer && actor.NativeActorIndex == 0 && actor.ModelId == 0);
        BattlePresentationProcess process = source.Presentation ?? throw new InvalidDataException("The natural prelude requires its current presentation owner.");
        using Process cpu = Process.GetCurrentProcess();
        TimeSpan cpuStart = cpu.TotalProcessorTime;
        long callbackStart = source.NativeHostCallbacks ?? throw new InvalidDataException("The natural prelude requires its native callback ledger.");
        List<NaturalRenzPreludeFact> facts = [];
        Dictionary<string, BattleCombatant> predecessors = new(StringComparer.Ordinal);
        HashSet<string> defeated = new(StringComparer.Ordinal);
        HashSet<int> finalInitialization = [];
        string? finalInitializationActor = null;
        BattleDiagnosticCounterCommand? finalNonvisualAdmission = null;
        bool finalInitializationCompleted = false;
        bool observingFinalInitialization = false;
        bool norgActivation = false;
        int attacks = 0;
        TimeSpan cpuBudget = TimeSpan.FromSeconds(effect == 337 ? 270 : 90);
        Add("entry", squall);
        source.DiagnosticEffectDispatched += ObserveDispatch;
        try
        {
            for (int callback = 0; callback < 90000 && source.Outcome == BattleOutcome.Active; callback++)
            {
                if ((callback & 31) == 0 && cpu.TotalProcessorTime - cpuStart >= cpuBudget)
                {
                    throw Failure("cpu-budget");
                }
                if (effect == 337)
                {
                    foreach (BattleCombatant actor in source.Combatants.Where(actor => !actor.IsPlayer && actor.ModelId is 115 or 116))
                    {
                        predecessors.TryAdd(actor.Id, actor);
                    }
                    foreach (BattleCombatant actor in predecessors.Values)
                    {
                        if (!actor.IsAlive && actor.CurrentHp == 0 && defeated.Add(actor.Id))
                        {
                            Add("native-death", actor);
                        }
                    }
                    if (defeated.Count >= 8 && !observingFinalInitialization)
                    {
                        source.DiagnosticCounterCommandQueued += ObserveCommand;
                        source.DiagnosticCounterCallbackObserved += ObserveCallback;
                        observingFinalInitialization = true;
                    }
                }
                BattleCombatant? target = source.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.ModelId == targetModel && actor.IsActive && actor.IsAlive && actor.CurrentHp > 0 && !actor.Hidden && !actor.Suspended && !actor.Untargetable);
                bool drained = source.ActiveAction is null && source.QueuedActions.Count == 0 && process.ActiveActionToken is null && source.ObserveDiagnosticDrainBlockers().Count == 0;
                if (target is not null && drained && source.CanChoosePlayerCommand(squall.Id) && !squall.IsBusy)
                {
                    BattleModelOwner owner = source.ReadPresentationModelOwner(target.Id);
                    if (target.NativeActorIndex is not int index || process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))) != owner || process.Graphics.ActorModels.Get(owner).CurrentBodyBoneMatrices is null)
                    {
                        throw Failure("target-binding");
                    }
                    if (attacks == 0 || effect == 335 && !norgActivation || effect == 337 && (defeated.Count != 10 || !finalInitialization.SetEquals([327, 74]) || !finalInitializationCompleted || target.Id != finalInitializationActor || target.Id != finalNonvisualAdmission?.ActorId || process.NativeStage?.Extended?.Stage137.ShakeAmplitude != 1))
                    {
                        throw Failure("native-prelude-proof");
                    }
                    Add("target-ready", target);
                    NaturalRenzPreludeReceipt receipt = Receipt();
                    Console.WriteLine("natural-renz-prelude: " + JsonSerializer.Serialize(receipt));
                    return receipt;
                }
                if (drained && source.CanChoosePlayerCommand(squall.Id) && !squall.IsBusy)
                {
                    BattleCombatant? victim = effect == 335
                        ? source.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.ModelId == 68 && Eligible(actor))
                        : source.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.ModelId is 115 or 116 && Eligible(actor));
                    if (victim is not null)
                    {
                        if (++attacks > 2048)
                        {
                            throw Failure("attack-budget");
                        }
                        source.QueueAttack(squall.Id, victim.Id);
                    }
                }
                source.Tick();
            }
            throw Failure(source.Outcome == BattleOutcome.Active ? "callback-budget" : "battle-ended");
        }
        finally
        {
            source.DiagnosticEffectDispatched -= ObserveDispatch;
            source.DiagnosticCounterCommandQueued -= ObserveCommand;
            source.DiagnosticCounterCallbackObserved -= ObserveCallback;
        }

        bool Eligible(BattleCombatant actor)
        {
            return actor.IsActive && actor.IsAlive && actor.CurrentHp > 0 && !actor.Suspended && !actor.Untargetable && actor.NativeActorIndex is int index && process.FindOriginalActor(NativeBattleModelSlot.Decode(checked((byte)index))) == source.ReadPresentationModelOwner(actor.Id);
        }

        void ObserveDispatch(BattleDiagnosticEffectDispatch dispatch)
        {
            BattleCombatant? actor = source.Combatants.FirstOrDefault(candidate => candidate.Id == dispatch.ActorId);
            if (effect == 335 && actor?.ModelId == 68 && dispatch.NativeCommand == 8 && dispatch.ActionId == 287 && dispatch.ExecutedEffect == 20 && !norgActivation)
            {
                norgActivation = true;
                Add("native-activation20", actor, dispatch.ActionId);
            }
            if (effect == 337 && actor?.ModelId == 117 && dispatch.NativeCommand == 8 && dispatch.ActionId == 327 && finalInitialization.Add(327))
            {
                finalInitializationActor = actor.Id;
                Add("native-final-init", actor, dispatch.ActionId);
            }
        }

        void ObserveCommand(BattleDiagnosticCounterCommand admitted)
        {
            BattleCombatant? actor = source.Combatants.FirstOrDefault(candidate => candidate.Id == admitted.ActorId);
            if (actor is not { ModelId: 117, IsActive: true, NativeAi: { } ai } || admitted is not { Command: 8, ActionId: 74, Sequence: 15, AiStage: 0, AiScope: > 0, CallbackEpoch: > 0, HeaderGeneration: > 0 } || admitted.ScriptSha256 != Convert.ToHexString(SHA256.HashData(ai.Scripts[0])))
            {
                return;
            }
            if (finalInitialization.Add(74))
            {
                finalNonvisualAdmission = admitted;
                Add("native-final-nonvisual-admitted", actor, admitted.ActionId);
            }
        }

        void ObserveCallback(BattleDiagnosticCounterCallbackObservation observed)
        {
            if (!finalInitializationCompleted && observed is { Phase: BattleDiagnosticCounterCallbackPhase.Completed, Queue.Stage: 0, Actor.ModelId: 117 } && finalNonvisualAdmission is { } admitted && observed.Queue.ActorId == admitted.ActorId && observed.Queue.Epoch == admitted.CallbackEpoch)
            {
                finalInitializationCompleted = true;
                Add("native-final-init-completed", source.Combatants.First(candidate => candidate.Id == admitted.ActorId));
            }
        }

        void Add(string boundary, BattleCombatant? actor, int? action = null)
        {
            if (facts.Count < 16)
            {
                facts.Add(new(source.NativeHostCallbacks ?? source.Frame, boundary, actor?.Id, actor?.ModelId, actor?.CurrentHp, action));
            }
        }

        NaturalRenzPreludeReceipt Receipt() => new(effect, attacks, defeated.Count, (source.NativeHostCallbacks ?? source.Frame) - callbackStart, (cpu.TotalProcessorTime - cpuStart).TotalMilliseconds, facts.ToArray());

        InvalidDataException Failure(string boundary)
        {
            Add(boundary, source.Combatants.FirstOrDefault(actor => !actor.IsPlayer && (actor.ModelId == targetModel || actor.ModelId == 68)));
            return new("Natural original Renzokuken prelude stopped: " + JsonSerializer.Serialize(Receipt()));
        }
    }
}
