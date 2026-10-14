using System.Numerics;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record DebugSceneObservation(long Index, string Mode, int FieldId, DebugSceneCommand Command);

internal sealed partial class GameWorld
{
    private readonly List<DebugSceneObservation> debugActions = [];
    public IReadOnlyList<DebugSceneObservation> DebugActions => debugActions;

    public void ApplyDebug(DebugSceneCommand command)
    {
        EnsureField();
        DebugSceneView current = session.State.DebugView ?? new DebugSceneView(Field!.Id);
        DebugSceneView? next = current;
        switch (command.Target)
        {
            case DebugSceneTarget.Clear:
                next = null;
                break;
            case DebugSceneTarget.Dialogue:
                next = current with { DialogueVisible = command.Visible ?? throw new ArgumentException("Dialogue visibility is required.") };
                break;
            case DebugSceneTarget.Background:
                bool visible = command.Visible ?? throw new ArgumentException("Background visibility is required.");
                if (command.ModuleId is int module)
                {
                    if (module is < 0 or > 255)
                    {
                        throw new ArgumentOutOfRangeException(nameof(command), "Module ID must be within0..255.");
                    }
                    HashSet<int> modules = new(current.HiddenBackgroundModules);
                    if (visible)
                    {
                        modules.Remove(module);
                    }
                    else
                    {
                        modules.Add(module);
                    }
                    next = current with { HiddenBackgroundModules = modules };
                }
                else
                {
                    next = current with { BackgroundVisible = visible };
                }
                break;
            case DebugSceneTarget.Actor:
                ActorState actor = Events.Actors.SingleOrDefault(actor => actor.ObjectId == command.ActorId) ?? throw new ArgumentException("Actor ID does not exist in the current field.");
                DebugActorView previous = current.Actors.GetValueOrDefault(actor.ObjectId) ?? new DebugActorView();
                DebugActorView replacement = previous with { Visible = command.Visible ?? previous.Visible, X = command.X ?? previous.X, Y = command.Y ?? previous.Y, Z = command.Z ?? previous.Z, AnimationId = command.AnimationId ?? previous.AnimationId, Frame = command.Frame ?? previous.Frame, Playing = command.Playing ?? previous.Playing, FramesPerSecond = command.FramesPerSecond ?? previous.FramesPerSecond };
                if (replacement.AnimationId is not null || replacement.Frame is not null || replacement.Playing)
                {
                    int count = Field!.GetAnimationFrameCount(actor.ModelId, replacement.AnimationId ?? actor.AnimationId);
                    replacement = replacement with { Frame = replacement.Frame ?? actor.AnimationFrame };
                    if (replacement.Frame < 0 || replacement.Frame >= count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(command), "Frame is outside the selected model animation.");
                    }
                }
                Dictionary<int, DebugActorView> actors = new(current.Actors) { [actor.ObjectId] = replacement };
                next = current with { Actors = actors };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
        session.SetDebugView(next);
        debugActions.Add(new DebugSceneObservation(debugActions.Count + 1, "DebugPresentation", Field!.Id, command));
    }

    public void AdvanceDebug(TimeSpan elapsed)
    {
        if (session.State.DebugView is not DebugSceneView view || elapsed <= TimeSpan.Zero)
        {
            return;
        }
        Dictionary<int, DebugActorView> actors = new(view.Actors);
        foreach ((int id, DebugActorView actorView) in view.Actors)
        {
            if (!actorView.Playing)
            {
                continue;
            }
            ActorState actor = Events.Actors.Single(actor => actor.ObjectId == id);
            int count = Field!.GetAnimationFrameCount(actor.ModelId, actorView.AnimationId ?? actor.AnimationId);
            float frame = (float)(((actorView.Frame ?? actor.AnimationFrame) + elapsed.TotalSeconds * actorView.FramesPerSecond) % count);
            actors[id] = actorView with { Frame = frame };
        }
        session.SetDebugView(view with { Actors = actors });
    }

    private ActorVisual CreateVisual(ActorState actor)
    {
        DebugActorView? view = session.State.DebugView?.Actors.GetValueOrDefault(actor.ObjectId);
        bool visible = view?.Visible ?? actor.Active && actor.Visible;
        IReadOnlyDictionary<int, Vector3>? translations = visible && actor.ModelId >= 0 ? Events.GetNativeModelPartTranslations(actor.ObjectId) : null;
        return new ActorVisual(actor.ObjectId, actor.ModelId, view?.X ?? actor.X + actor.DisplayOffset.X, view?.Y ?? actor.Y + actor.DisplayOffset.Y, view?.Z ?? actor.Z + actor.DisplayOffset.Z, actor.Direction, view?.AnimationId ?? actor.AnimationId, view?.Frame ?? actor.AnimationFrame, visible, RootTranslationZ: actor.RootTranslationZ, FirstControlRotation: actor.FirstControlRotation, AdditionalControlDisabled: actor.AdditionalControlDisabled, PolygonColor: actor.PolygonColor, GroundShadowRadius601To608: actor.NativeShadowSet?.Radius601To608, GroundShadowLevel609: actor.NativeShadowLevel?.Level609 ?? actor.NativeShadowAllocation?.Level609, GroundShadowCentre: new Vector3(actor.X, actor.Y, actor.Z), GroundShadowRadii601To608: actor.NativeShadowForm?.Radii601To608, SkinTranslations: translations);
    }
}
