using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public NativeBattleFieldResourceReloadResult ExecuteNativeBattleFieldResourceReload(NativeFieldRegistrationResources resources, byte nativeResult, NativeBattleRetainedMemory retained, NativeCrtRandomState? crtRandom = null)
    {
        long battleEntryFrame = RequireNativeBattleFieldReloadEntryFrame();
        resources.Validate();
        if (!SupportsNativeFieldReturnResult(nativeResult) || NativeRuntimeProfile is not { IsVerified: true } profile || Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } initialized || !initialized.IsFrom(profile, FieldId, programHash) || resources.FieldId != FieldId || resources.ProgramHash != programHash || Scene.NativeActorLayoutVersion != 1 || Scene.NativeBattleFieldReload is not null)
        {
            throw new NotSupportedException("Native kind3 resource reload requires the same retained initialized field/program and ordinary result2/4.");
        }
        NativeFieldRegistrationLoaderInputs loader = resources.Loader ?? throw new NotSupportedException("Native kind3 requires complete original registration inputs.");
        JsmProgram reloaded = JsmProgram.Read(loader.Program.Data.ToArray());
        NativeFieldRetainedActor[] actorsToBind = Scene.NativeActorObjectIds.Select(objectId =>
        {
            ActorState actor = actors[objectId];
            EventObject original = program!.Objects[objectId];
            EventObject replacement = reloaded.Objects[objectId];
            if (original.Label != replacement.Label || original.Scripts.Count != replacement.Scripts.Count || !program.NativeObjects.Take(program.NativeActorCount).Contains(objectId))
            {
                throw new InvalidDataException("Native kind3 cannot replace retained script ranges or compact actors again.");
            }
            int model = actor.ModelId is -1 or ushort.MaxValue ? -1 : actor.ModelId;
            return new NativeFieldRetainedActor(objectId, model, original.Label, checked(original.Label + original.Scripts.Count));
        }).ToArray();
        NativeFieldRetainedPresentation[] presentation = Scene.NativeActorObjectIds.Select(objectId =>
        {
            ActorState actor = actors[objectId];
            NativeActorAnimationHeader header = RequireNativeAnimationHeader(actor);
            NativeActorPartyFlags flags = actor.NativePartyFlags ?? throw new NotSupportedException("Native52B840 requires the retained actor DWORD352 animation flags.");
            byte mode = actor.NativeRendererMode599 ?? throw new NotSupportedException("Native kind3 requires the retained actor BYTE599; explicitly migrate a proved preexisting snapshot rather than invent its renderer mode.");
            System.Numerics.Vector3 color = actor.PolygonColor ?? throw new NotSupportedException("Native52B840 requires retained actor RGB394..396.");
            if (!ValidNativePolygonColor(color))
            {
                throw new InvalidDataException("Native kind3 actor RGB is outside its physical byte domain.");
            }
            NativeActorEyes eyes = actor.NativeEyes ?? throw new NotSupportedException("Native52B840 requires the retained actor eye mode.");
            return new NativeFieldRetainedPresentation(objectId, header.AnimationId, header.Frame, flags.AutoAnimation4, checked((byte)actor.BaseAnimationId), actor.Visible, mode, actor.RootTranslationZ, (byte)color.X, (byte)color.Y, (byte)color.Z, actor.AdditionalControlDisabled, actor.PrimitiveTransparencyEnabled, eyes.Mode) { EyeBlinkParameter611 = actor.NativeEyeBlinkParameter611 };
        }).ToArray();
        if (Scene.NativeBattleFieldReloadState is null)
        {
            ExecuteNativeBattleFieldCacheReset(resources, nativeResult);
        }
        NativeBattleFieldReloadState pending = Scene.NativeBattleFieldReloadState!;
        pending.Validate();
        if (pending.FieldId != FieldId || pending.ProgramHash != programHash || pending.BattleEntryFrame != battleEntryFrame || pending.NativeResult != nativeResult || pending.OneHash != resources.One.Sha256)
        {
            throw new InvalidDataException("Native kind3 pending reset disagrees with the retained battle/source.");
        }
        NativeFieldRegistrationStep step = NativeFieldRegistrationMachine.ReloadKind3(resources, actorsToBind, retained);
        NativeBattleFieldResourceReloadResult result = NativeFieldRegistrationMachine.RestoreRetainedPresentation(step.State, step.Memory, presentation, crtRandom);
        step.State.Validate(result.Memory);
        Dictionary<int, NativeModelPartResourceState> parts = [];
        foreach (NativeFieldRegisteredResource resource in step.State.Resources)
        {
            NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(resource.ModelId) ?? throw new NotSupportedException("Native kind3 requires the original registered model's part definitions.");
            if (definition.SourceHash != resource.SourceHash || definition.SourceOffset != resource.SourceOffset)
            {
                throw new InvalidDataException("Native kind3 part projection differs from its actually registered geometry source.");
            }
            parts.Add(resource.ModelId, NativeModelPartResourceState.Create(definition));
        }
        Dictionary<int, NativeActorAnimationHeader> headers = [];
        Dictionary<int, NativeActorEyes> eyesToBind = [];
        foreach (NativeFieldRegisteredInstance instance in step.State.Instances)
        {
            ActorState actor = actors[instance.ObjectId];
            headers.Add(actor.ObjectId, RequireNativeAnimationHeader(actor) with { BoundModelId = instance.ModelId, EmptyModelTerminal = instance.EmptyTerminal });
            NativeActorEyes eyes = actor.NativeEyes!;
            if (eyes.Mode != 0 && instance.ModelId >= 0)
            {
                NativeFieldEyeDefinition definition = ReadNativeEyes?.Invoke(instance.ModelId) ?? throw new NotSupportedException("Native retained eyes require their reloaded original eye definition.");
                NativeEyeCopy? copy = eyes.Mode == 0x80000 ? null : definition.Copy(eyes.Mode == 0x200000);
                byte? backend = definition.Bank.Length == 0 ? null : eyes.Mode == 0x80000 ? (byte)0x10 : eyes.Mode == 0x200000 ? (byte)0x20 : (byte)0x40;
                eyes = new(1, eyes.Mode, instance.ModelId, backend, copy);
            }
            eyesToBind.Add(actor.ObjectId, eyes);
        }
        //The original same-field branch retains PCs, stacks, waits and actor records. Only physical model bindings are replaced.
        Scene.ModelParts = new(parts, step.State.Instances.Where(instance => instance.ModelId >= 0).Select(instance => new NativeFieldModelInstance(instance.ObjectId, instance.ModelId)).ToArray(), []);
        foreach ((int objectId, NativeActorAnimationHeader header) in headers)
        {
            actors[objectId].NativeAnimationHeader = header;
            actors[objectId].NativeEyes = eyesToBind[objectId];
        }
        Scene.NativeFieldRegistration = step.State;
        if (Scene.NativeEyeProcess is not null)
        {
            Scene.NativeEyeProcess = new(1, result.Memory, result.CrtRandomAfter);
        }
        Scene.NativeBattleFieldReloadState = null;
        Scene.NativeBattleFieldReload = new(1, profile.ExecutableHash, FieldId, programHash, battleEntryFrame, nativeResult, nativeResult == 1 ? ReadNativeDefeatReturnBranch4706B0() : null);
        return result;
    }

    public void ValidateNativeBattleFieldRegistrationMemory(NativeBattleRetainedMemory memory)
    {
        NativeFieldRegistrationState registration = Scene.NativeFieldRegistration ?? throw new InvalidDataException("Native kind3 completion has no executed resource registration.");
        if (registration.FieldId != FieldId || registration.ProgramHash != programHash || !registration.Instances.Select(instance => instance.ObjectId).SequenceEqual(Scene.NativeActorObjectIds) || registration.Instances.Any(instance => instance.ModelId != actors[instance.ObjectId].NativeAnimationHeader?.BoundModelId))
        {
            throw new InvalidDataException("Native kind3 registration no longer matches its retained field/actor owners.");
        }
        registration.Validate(memory);
    }
}
