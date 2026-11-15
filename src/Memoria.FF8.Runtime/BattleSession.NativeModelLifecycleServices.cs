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
            ActorOpacities = ReadPreparedActorOpacities() ?? ImmutableDictionary<string, float>.Empty,
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
                RawBattleStatuses = NativeBattleStatusBits(actor),
                CurrentHp = actor.CurrentHp,
                MaximumHp = actor.MaximumHp,
                SavedStatus = NativeSavedStatusBits(actor),
                Information246 = actor.NativeInformation246,
                PendingAllocationStatistics = actor.NativeAllocationPendingStatistics
            });
        }
        HostedNativeAllocationFrame? allocation = null;
        if (nativeScriptState.AllocationControl is { SceneSlot: byte scene } && nativeLastAllocatedTarget is not null)
        {
            BattleCombatant actor = ResolveLastNativeAllocation();
            if (actor.SceneSlot != scene || CreatePresentationModelOwner(actor) is not BattleEnemyActorModel owner || registry?.Loads.ContainsKey(owner) != true)
            {
                throw new InvalidDataException("Hosted frame allocation globals lack their registered generation and scene.");
            }
            allocation = new(owner, scene, actor.SourceDefinition.InitialHidden);
        }
        return new(facts.ToImmutable())
        {
            Allocation = allocation,
            Shot = CaptureExpandedHostedShotOwner(roster),
            Duel = CaptureExpandedHostedDuelOwner(),
            Renzokuken = CaptureExpandedHostedRenzokukenFrame(),
            ActorOpacities = ReadPreparedActorOpacities() ?? ImmutableDictionary<string, float>.Empty,
            ReadScanDescriptor = owner => CaptureNativeScanDescriptor208(GetActor(RequireCanonicalGraphicsActorId(owner))),
            ReadScanExtra = owner => ReadNativeScanExtra71(GetActor(RequireCanonicalGraphicsActorId(owner)))
        };
    }

    private NativeBattleGraphicsState PublishHostedModelLifecycleCue(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsModel model, NativeBattleModelActorFacts actor, NativeBattleSequenceCue cue, NativeBattleModelLifecycleServices services)
    {
        switch (cue.Kind)
        {
            case NativeBattleSequenceCueKind.Text:
                NativeBattleGraphicsResource resource = ReadOriginalPresentationModelResource(owner) ?? throw new InvalidDataException("Hosted Native91 lost its original actor resource binding.");
                services.Emit(new NativeBattleModelTextPublished(owner, actor.ActorId, resource.ReadText91(cue.First), cue.Second, actor.IsParty ? null : BattleTextIdentity.Enemy(actor.OriginalModelId, cue.First)));
                break;
            case NativeBattleSequenceCueKind.SuspendParticipant:
                NativeBattleActionHeaderSlot? header = graphics.ActionGraphics is { } action && nativeActionPackets.ActiveHeader == action.Header && action.ResolveActor(graphics) == owner
                    ? nativeActionPackets.ReadHeader(action.Header)
                    : null;
                services.Emit(new NativeBattleModelParticipantSuspended(owner, actor.ActorId, checked((byte)cue.First)) { ActiveHeader = header });
                break;
            case NativeBattleSequenceCueKind.ActionCallback:
                throw new InvalidDataException("Native AA must publish its active header contacts inline.");
            case NativeBattleSequenceCueKind.TriggerWindow:
                graphics = PrepareNativeTriggerWidget(graphics with { ActorModels = graphics.ActorModels.Set(owner, model) }, owner, cue);
                services.Emit(new NativeBattleModelTriggerRegistered(owner, actor.ActorId, graphics.TriggerWidget!));
                break;
        }
        return graphics;
    }
}
