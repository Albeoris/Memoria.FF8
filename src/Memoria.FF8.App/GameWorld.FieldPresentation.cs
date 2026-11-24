using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly FieldPresentation fieldPresentation = new();
    private readonly Dictionary<int, FieldActorSpringState> fieldSpringBones = [];

    private sealed record FieldActorSpringState(object Scene, object Actor, FieldModel Model, long Frame, long PositionRevision, FieldSpringBoneVisual Visual);

    private void ResetFieldPresentation()
    {
        fieldPresentation.Reset();
        fieldSpringBones.Clear();
    }

    internal void CommitFieldPresentation()
    {
        if (session.State.Scene != SceneKind.Field || Events.FieldId != session.State.FieldId || !CanPresentCurrentField)
        {
            ResetFieldPresentation();
            return;
        }
        fieldPresentation.Commit(Events.Scene, Events.Frame, () => CaptureFieldPresentation(advanceSprings: true));
    }

    private FieldPresentationFrame CaptureFieldPresentation(bool advanceSprings = false)
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
        scroll = scroll with { LayerScales = Field!.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => layer <= Events.Scene.CameraIndex ? (short)256 : Events.Scene.BackgroundScrollLayers.GetValueOrDefault(layer)?.ScaleX ?? (short)256) };
        FieldActorSample[] actors = Events.Actors.Select(CaptureFieldActor).ToArray();
        if (fieldSpringBones.Count != 0 || presentationField!.Models.Any(model => model.SpringBones.Count != 0))
        {
            SampleFieldSpringBones(actors, advanceSprings);
        }
        return new FieldPresentationFrame(Events.Scene, Events.Frame, Events.Scene.CameraIndex, actors, scroll, Events.Scene.PresentationCameraRevision, cameraActorId, Events.DrawPointParticleFrame, Events.FieldFade, Events.GetBackgroundModuleShades(), Events.Scene.NativeFieldParticles, Events.FieldParticleSprites);
    }

    private void SampleFieldSpringBones(FieldActorSample[] actors, bool advance)
    {
        object scene = Events.Scene;
        long frame = Events.Frame;
        IReadOnlyList<FieldModel> models = presentationField!.Models;
        for (int index = 0; index < actors.Length; index++)
        {
            FieldActorSample actor = actors[index];
            ActorVisual visual = actor.Visual;
            if (!visual.Visible || (uint)visual.ModelId >= models.Count || models[visual.ModelId] is not { SpringBones.Count: > 0 } model)
            {
                continue;
            }
            FieldActorSpringState? state = fieldSpringBones.GetValueOrDefault(visual.ObjectId);
            bool sameActor = state is not null && ReferenceEquals(state.Scene, scene) && ReferenceEquals(state.Actor, actor.Owner) && ReferenceEquals(state.Model, model) && state.PositionRevision == actor.PositionRevision;
            if (advance && (!sameActor || state!.Frame != frame))
            {
                FieldSpringBoneSimulation simulation = sameActor && state!.Frame == frame - 1 ? state.Visual.Simulation : new(model.GetSpringBoneParents(), model.SpringBones);
                FieldAnimationBlend animation = new(visual.AnimationFrame, visual.AnimationFrame, 1, actor.FirstFrame, actor.TerminalFrame, actor.Loop);
                var transforms = model.ReadPresentationBoneTransforms(visual.AnimationId, visual.AnimationFrame, visual.FirstControlRotation, visual.RootTranslationZ, animation);
                simulation.Step(transforms, visual.ModelToField(), 1f / EventEngine.SimulationFps);
                state = new(scene, actor.Owner, model, frame, actor.PositionRevision, new(simulation, simulation.Capture()));
                fieldSpringBones[visual.ObjectId] = state;
                sameActor = true;
            }
            if (sameActor && state!.Frame == frame)
            {
                actors[index] = actor with { Visual = visual with { SpringBones = state.Visual } };
            }
        }
        if (advance)
        {
            foreach (int objectId in fieldSpringBones.Where(pair => pair.Value.Frame != frame || !ReferenceEquals(pair.Value.Scene, scene)).Select(pair => pair.Key).ToArray())
            {
                fieldSpringBones.Remove(objectId);
            }
        }
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
        if (frameCount is int count && count > 0)
        {
            // Native range selection can publish an empty interval at the end of a clip before the next callback completes it.
            first = Math.Clamp(first, 0, count - 1);
            terminal = Math.Clamp(terminal, first + 1, count);
        }
        return new FieldActorSample(actor, actor.PresentationPositionRevision, actor.PresentationAnimationRevision, visual, first, terminal, customAnimation || actor.AnimationLoop, customAnimation ? 1 : actor.PresentationAnimationStep ?? actor.AnimationSpeed);
    }
}
