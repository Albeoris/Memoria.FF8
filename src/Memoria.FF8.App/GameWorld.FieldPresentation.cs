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
        int? frameCount = null;
        if ((uint)visual.ModelId < presentationField!.Models.Count && (uint)visual.AnimationId < presentationField.Models[visual.ModelId].Animations.Count)
        {
            frameCount = presentationField.GetAnimationFrameCount(visual.ModelId, visual.AnimationId);
        }
        return CaptureFieldActor(actor, visual, frameCount, customAnimation);
    }

    internal static FieldActorSample CaptureFieldActor(ActorState actor, ActorVisual visual, int? frameCount, bool customAnimation = false)
    {
        // Completion can move the held phase below the retained first frame or into the terminal word.
        // Sampling bounds must include that pose and its fractional neighbour, independently of playback bounds.
        int first = customAnimation ? 0 : actor.AnimationFirst;
        int terminal = customAnimation ? frameCount ?? throw new InvalidDataException("Custom animation requires its selected clip metadata.") : actor.AnimationLast + 1;
        if (!customAnimation && !actor.AnimationLoop)
        {
            first = Math.Min(first, (int)MathF.Floor(visual.AnimationFrame));
            terminal = Math.Max(terminal, (int)MathF.Ceiling(visual.AnimationFrame) + 1);
        }
        if (frameCount is int count)
        {
            terminal = Math.Min(terminal, count);
        }
        return new FieldActorSample(actor, actor.PresentationPositionRevision, actor.PresentationAnimationRevision, visual, first, terminal, customAnimation || actor.AnimationLoop, customAnimation ? 1 : actor.AnimationSpeed);
    }
}
