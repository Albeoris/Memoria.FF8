using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal NativeBattleModelLifecycleServices CaptureModelLifecycleServices(BattleModelRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var facts = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleModelActorFacts>();
        foreach (BattleCombatant actor in combatants.Where(actor => actor.Slot is not null))
        {
            BattleModelOwner owner = RequirePresentationModelOwner(roster, actor);
            NativeBattleModelActorFacts captured = new(actor.Id, actor.IsPlayer, actor.ModelId, actor.NativeStatusTimers[12], actor.NativeStatusTimers[10])
            {
                EnemyMessages = actor.NativeAi?.Texts,
                RawBattleStatuses = NativeBattleStatusBits(actor),
                CurrentHp = actor.CurrentHp,
                MaximumHp = actor.MaximumHp
            };
            if (!facts.TryAdd(owner, captured))
            {
                throw new InvalidDataException("Expanded model lifecycle actor has a conflicting roster owner.");
            }
        }
        if (facts.Count != roster.Count)
        {
            throw new InvalidDataException("Expanded model lifecycle roster contains an actor without a current allocation.");
        }
        return new(facts.ToImmutable())
        {
            ActorOpacities = ReadPendingAllocationOpacities(),
            ReadScanDescriptor = owner => CaptureNativeScanDescriptor208(GetActor(RequireCanonicalGraphicsActorId(owner))),
            ReadScanExtra = owner => ReadNativeScanExtra71(GetActor(RequireCanonicalGraphicsActorId(owner)))
        };
    }

    private NativeBattleModelLifecycleServices CaptureHostedModelLifecycleServices(BattleModelRoster roster, HostedModelRegistry? registry)
    {
        var facts = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleModelActorFacts>();
        foreach (BattleModelOwner owner in roster.Owners)
        {
            if (!byId.TryGetValue(RequireCanonicalGraphicsActorId(owner), out BattleCombatant? actor) || owner != CreatePresentationModelOwner(actor))
            {
                throw new InvalidDataException("Hosted frame facts require the registered allocation identity.");
            }
            facts.Add(owner, new(actor.Id, actor.IsPlayer, actor.ModelId, actor.NativeStatusTimers[12], actor.NativeStatusTimers[10])
            {
                EnemyMessages = actor.NativeAi?.Texts,
                RawBattleStatuses = NativeBattleStatusBits(actor),
                CurrentHp = actor.CurrentHp,
                MaximumHp = actor.MaximumHp,
                SavedStatus = NativeSavedStatusBits(actor),
                PresentationSavedStatus = actor.NativePresentationStatus.Saved,
                PresentationBattleStatus = actor.NativePresentationStatus.Battle,
                Information246 = actor.NativeInformation246,
                PendingAllocationStatistics = actor.NativeAllocationPendingStatistics
            });
        }
        HostedNativeAllocationFrame? allocation = null;
        if (nativeScriptState.AllocationControl is { SceneSlot: byte scene } control && (control.ActionGate || control.PauseGate))
        {
            BattleCombatant actor = ResolveLastNativeAllocation();
            if (CreatePresentationModelOwner(actor) is not BattleEnemyActorModel owner ||
                registry?.Loads.TryGetValue(owner, out HostedModelLoadAdmission? admission) != true || admission.Load != ReadHostedEncounterAllocationLoad(actor) ||
                ReadNativeAllocationSourceScene(actor) != scene)
            {
                throw new InvalidDataException("Hosted frame allocation globals lack their registered generation and scene.");
            }
            allocation = new(owner, scene, actor.SourceDefinition.InitialHidden);
        }
        return new(facts.ToImmutable())
        {
            Allocation = allocation,
            GuardianForm = CaptureExpandedHostedGuardianFormFacts(),
            Shot = CaptureExpandedHostedShotOwner(roster),
            Duel = CaptureExpandedHostedDuelOwner(),
            Renzokuken = CaptureExpandedHostedRenzokukenFrame(),
            ActorOpacities = ReadPendingAllocationOpacities(),
            ReadScanDescriptor = owner => CaptureNativeScanDescriptor208(GetActor(RequireCanonicalGraphicsActorId(owner))),
            ReadScanExtra = owner => ReadNativeScanExtra71(GetActor(RequireCanonicalGraphicsActorId(owner)))
        };
    }
}
