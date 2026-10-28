using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Reconstructs inactive physical model caches from the currently selected resources without executing a game callback.</summary>
    public bool RehydrateNativeModelResources()
    {
        if (Scene.NativeFieldRegistration is not { } registration)
        {
            return false;
        }
        NativeFieldRegistrationResources source = ReadNativeFieldRegistrationResources?.Invoke() ?? throw new NotSupportedException("Model restoration requires the selected field registration resources.");
        bool changed = registration.OneHash != source.One.Sha256 || registration.Resources.Any(resource => (ReadNativeModelRegistrationParts ?? ReadNativeModelParts)?.Invoke(resource.ModelId) is not { } definition || definition.SourceHash != resource.SourceHash || definition.SourceOffset != resource.SourceOffset);
        if (!changed)
        {
            return false;
        }
        if (services is not GameSession session || session.HasActiveBattle || Scene.NativeBattleFieldReloadState is not null)
        {
            throw new NotSupportedException("Model resource replacement cannot reconstruct caches during a pending battle resource transaction.");
        }
        NativeFieldEyeProcess process = ReadCurrentNativeEyeProcess(Scene.NativeEyeProcess ?? throw new NotSupportedException("Model resource restoration requires the retained physical presentation owner."));
        NativeFieldRetainedActor[] retained = registration.Instances.Select(instance =>
        {
            EventObject script = program!.Objects[instance.ObjectId];
            return new NativeFieldRetainedActor(instance.ObjectId, instance.ModelId, script.Label, checked(script.Label + script.Scripts.Count));
        }).ToArray();
        NativeFieldRetainedPresentation[] presentation = registration.Instances.Select(instance =>
        {
            ActorState actor = actors[instance.ObjectId];
            NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
            NativeActorPartyFlags flags = actor.NativePartyFlags ?? throw new NotSupportedException("Model resource restoration requires retained animation flags.");
            byte renderer = actor.NativeRendererMode599 ?? throw new NotSupportedException("Model resource restoration requires retained renderer mode.");
            System.Numerics.Vector3 color = actor.PolygonColor ?? throw new NotSupportedException("Model resource restoration requires retained polygon color.");
            if (!ValidNativePolygonColor(color))
            {
                throw new InvalidDataException("Retained model polygon color is outside its byte domain.");
            }
            NativeActorEyes eyes = actor.NativeEyes ?? throw new NotSupportedException("Model resource restoration requires retained logical eye state.");
            return new NativeFieldRetainedPresentation(actor.ObjectId, header.AnimationId, header.Frame, flags.AutoAnimation4, checked((byte)actor.BaseAnimationId), actor.Visible, renderer, actor.RootTranslationZ, (byte)color.X, (byte)color.Y, (byte)color.Z, actor.AdditionalControlDisabled, actor.PrimitiveTransparencyEnabled, eyes.Mode) { EyeBlinkParameter611 = actor.NativeEyeBlinkParameter611 };
        }).ToArray();
        NativeFieldRegistrationStep rebuilt = NativeFieldRegistrationMachine.RehydrateInactiveResources(source, registration, process.Memory, retained, presentation);
        NativeFieldEyeProcess next = new(process.Version, rebuilt.Memory, process.CrtRandom);
        next.Validate(rebuilt.State);
        Scene.NativeFieldRegistration = rebuilt.State;
        if (OwnsNativeFieldEyeProcess(session))
        {
            PublishNativeFieldEyeProcess(next);
        }
        else
        {
            Scene.NativeEyeProcess = next;
        }
        return true;
    }
}
