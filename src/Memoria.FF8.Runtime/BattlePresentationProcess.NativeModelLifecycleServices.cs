using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

    private NativeBattleGraphicsState PublishHostedModelLifecycleCue(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleGraphicsModel model, NativeBattleModelActorFacts actor, NativeBattleSequenceCue cue, NativeBattleModelLifecycleServices services)
    {
        switch (cue.Kind)
        {
            case NativeBattleSequenceCueKind.Text:
                NativeBattleGraphicsResource resource = ReadStandingPresentationModelResource(graphics, owner) ?? throw new InvalidDataException("Hosted Native91 lost its standing actor resource binding.");
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
