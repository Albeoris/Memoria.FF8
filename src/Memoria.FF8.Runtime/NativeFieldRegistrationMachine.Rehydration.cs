namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    /// <summary>Reconstructs selected static model caches without replaying a callback or initializing a new eye sequence.</summary>
    public static NativeFieldRegistrationStep RehydrateInactiveResources(NativeFieldRegistrationResources source, NativeFieldRegistrationState previous, NativeBattleRetainedMemory retained, IReadOnlyList<NativeFieldRetainedActor> actors, IReadOnlyList<NativeFieldRetainedPresentation> presentation)
    {
        source.Validate();
        previous.Validate(retained);
        if (previous.FieldId != source.FieldId || previous.ProgramHash != source.ProgramHash || !previous.Instances.Select(instance => (instance.ObjectId, instance.ModelId)).SequenceEqual(actors.Select(actor => (actor.ObjectId, actor.ModelId))) || !actors.Select(actor => actor.ObjectId).SequenceEqual(presentation.Select(actor => actor.ObjectId)))
        {
            throw new InvalidDataException("Model cache rehydration requires the same field program and retained actor/model bindings.");
        }
        if (presentation.Any(actor => actor.EyeMode != 0))
        {
            throw new NotSupportedException("Changed model resources with active eye commands require semantic eye-progress rehydration; static cache reconstruction cannot reset them.");
        }
        NativeBattleFileStaging storage = retained.FileStaging!;
        if (previous.Instances.Where(instance => instance.ModelId >= 0).Any(instance => (storage.Read(instance.Address + 114, 1)[0] & 0xF0) != 0))
        {
            throw new NotSupportedException("Changed model resources with active native eye progression cannot be reconstructed as static caches.");
        }
        if (retained.FieldTextureAnimations is { } animations)
        {
            animations.Validate();
            if (animations.Textures.Any(animation => animation.Frames.Length != 0 || animation.CurrentFrame != -1) || animations.Planes is { } planes && planes.Slots.Any(plane => plane.Closed || plane.Pending != 0 || plane.Retain))
            {
                throw new NotSupportedException("Changed model resources with retained texture-copy or eye-plane progress require semantic copy-history rehydration; static cache reconstruction cannot discard it.");
            }
        }
        if (retained.EffectMemory?.Textures is { Count: not 0 })
        {
            throw new NotSupportedException("Model cache reconstruction cannot replace resources while native VRAM operations are pending.");
        }
        NativeFieldRegistrationStep rebuilt = ReloadKind3(source, actors, retained);
        // Snapshot rehydration retains the sampled phase even when the actor owns automatic animation.
        NativeFieldRetainedPresentation[] sampled = presentation.Select(actor => actor with { Animation = actor.AutoAnimation ? actor.BaseAnimation : actor.Animation, AutoAnimation = false }).ToArray();
        NativeBattleFieldResourceReloadResult restored = RestoreRetainedPresentation(rebuilt.State, rebuilt.Memory, sampled);
        return rebuilt with { Memory = restored.Memory };
    }
}
