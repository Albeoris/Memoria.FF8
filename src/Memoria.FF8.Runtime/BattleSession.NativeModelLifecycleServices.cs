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
        return new(facts.ToImmutable()) { ActorOpacities = ReadPreparedActorOpacities() ?? ImmutableDictionary<string, float>.Empty };
    }

    private NativeBattleGraphicsState PublishHostedModelLifecycleCue(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsModel model, NativeBattleModelActorFacts actor, NativeBattleSequenceCue cue, NativeBattleModelLifecycleServices services)
    {
        switch (cue.Kind)
        {
            case NativeBattleSequenceCueKind.Text:
                NativeBattleGraphicsResource resource = ReadHostedPresentationModelResource(owner) ?? throw new InvalidDataException("Hosted Native91 lost its original actor resource binding.");
                services.Emit(new NativeBattleModelTextPublished(owner, actor.ActorId, resource.ReadText91(cue.First), cue.Second, actor.IsParty ? null : BattleTextIdentity.Enemy(actor.OriginalModelId, cue.First)));
                break;
            case NativeBattleSequenceCueKind.SuspendParticipant:
                NativeBattleActionHeaderSlot? header = graphics.ActionGraphics is { } action && action.Actor == owner && nativeActionPackets.ActiveHeader == action.Header
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
