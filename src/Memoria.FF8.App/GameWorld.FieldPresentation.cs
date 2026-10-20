using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly FieldPresentation fieldPresentation = new();

    internal void CommitFieldPresentation()
    {
        if (session.State.Scene != SceneKind.Field || Events.FieldId != session.State.FieldId || !CanPresentCurrentField)
        {
            fieldPresentation.Reset();
            return;
        }
        fieldPresentation.Commit(Events.Scene, Events.Frame, CaptureFieldPresentation);
    }

    private FieldPresentationFrame CaptureFieldPresentation()
    {
        RefreshSceneMods();
        BackgroundScrollFrame? scroll = ActorBackgroundScrollCertificate.Prepare(Field!, Events.Scene);
        int? cameraActorId = scroll is not null ? Events.Scene.ActorBackgroundScroll!.ObjectId : null;
        if (scroll is null)
        {
            scroll = DirectBackgroundScrollCertificate.Prepare(Field!, Events.Scene);
            if (scroll is null)
            {
                scroll = NativeWrappedBackgroundScrollCertificate.Prepare(Field!, Events.Scene, Events.Actors) ?? NormalCameraCertificate.Prepare(Field!, Events.Scene, Events.Actors);
                cameraActorId = Events.PlayerActor?.ObjectId;
            }
        }
        if (scroll is null)
        {
            throw new NotSupportedException("Field rendering requires a supported current native camera contract.");
        }
        FieldActorSample[] actors = Events.Actors.Select(CaptureFieldActor).ToArray();
        return new FieldPresentationFrame(Events.Scene, Events.Frame, Events.Scene.CameraIndex, actors, scroll, Events.Scene.PresentationCameraRevision, cameraActorId);
    }

    internal FieldActorSample CaptureFieldActor(ActorState actor)
    {
        ActorVisual visual = CreateVisual(actor);
        bool patched = modActors.TryGetValue(actor.ObjectId, out var mod) && IsActorPatchActive(mod.Patch);
        DebugActorView? view = session.State.DebugView?.Actors.GetValueOrDefault(actor.ObjectId);
        bool customAnimation = patched || view?.AnimationId is not null || view?.Frame is not null;
        int first = customAnimation ? 0 : actor.AnimationFirst;
        int terminal = customAnimation ? presentationField!.GetAnimationFrameCount(visual.ModelId, visual.AnimationId) : Math.Max(actor.AnimationLast + 1, (int)MathF.Ceiling(visual.AnimationFrame) + 1);
        if ((uint)visual.ModelId < presentationField!.Models.Count && (uint)visual.AnimationId < presentationField.Models[visual.ModelId].Animations.Count)
        {
            terminal = Math.Min(terminal, presentationField.GetAnimationFrameCount(visual.ModelId, visual.AnimationId));
        }
        // A native held animation may retain its final frame in the terminal word itself.
        // Presentation includes that current sample without changing the physical header or completion state.
        return new FieldActorSample(actor, actor.PresentationPositionRevision, actor.PresentationAnimationRevision, visual, first, terminal, customAnimation || actor.AnimationLoop, customAnimation ? 1 : actor.AnimationSpeed);
    }
}
