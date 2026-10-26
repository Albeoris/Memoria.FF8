using System.Numerics;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeFieldModelInstance([property: JsonRequired] int ObjectId, [property: JsonRequired] int ModelId);
/// <summary>Logical input to an executed model callback; resource-specific skin samples are rebuilt without advancing live state.</summary>
public sealed record NativeModelPartDraw([property: JsonRequired] long Frame, [property: JsonRequired] int ModelId, [property: JsonRequired] NativeModelPartResourceState SampleState);
public sealed record NativeFieldModelPartsState([property: JsonRequired] Dictionary<int, NativeModelPartResourceState> Resources, [property: JsonRequired] NativeFieldModelInstance[] Instances, [property: JsonRequired] Dictionary<int, NativeModelPartDraw> Draws)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldModelCacheResetState? CacheReset { get; init; }
}

public sealed partial class EventEngine
{
    public Func<int, NativeModelPartsDefinition?>? ReadNativeModelParts { get; set; }

    public bool IsNativeModelDrawPending
    {
        get
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || Scene.ModelParts is null || !IsNativeModelRegistrationOnly(Scene, Frame))
            {
                return false;
            }
            ValidateNativeModelParts(Scene, actors, Frame, defaultsStarted, transitionRequested, services.MovieSource);
            return true;
        }
    }

    private static bool IsNativeModelRegistrationOnly(SceneEventState scene, long frame)
    {
        // Menu and combat callbacks leave the registered field model bank dormant.
        return frame == 1 && scene.NativeFieldInitialization is { Stage: NativeFieldInitializationStage.Completed } && scene.FieldCallbackClock is { Version: 1, InitializationTransactions: 1, OrdinaryCallbacks: 0, MovieCallbacks: 0, FieldLoaderCallbacks: 0 };
    }

    private void InitializeNativeModelParts()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || ReadNativeModelParts is null)
        {
            return;
        }
        NativeFieldModelInstance[] instances = Scene.NativeActorObjectIds.Select(id => actors[id]).Where(actor => actor.ModelId >= 0).Select(actor => new NativeFieldModelInstance(actor.ObjectId, actor.ModelId)).ToArray();
        if (instances.Length > 32)
        {
            throw new NotSupportedException("Native model registration exceeds its32 instance slots.");
        }
        Dictionary<int, NativeModelPartResourceState> resources = [];
        foreach (int model in instances.Select(instance => instance.ModelId).Distinct())
        {
            NativeModelPartsDefinition definition = ReadNativeModelParts(model) ?? throw new NotSupportedException("Native model registration requires its actual MCH part table.");
            resources.Add(model, NativeModelPartResourceState.Create(definition));
        }
        Scene.ModelParts = new NativeFieldModelPartsState(resources, instances, []);
    }

    private string? UnsupportedNativeModelDoor(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.MODELDOOROPEN or Jsm.Opcode.MODELDOORCLOSE))
        {
            return null;
        }
        if (fiber.Stack.Count < 1 || fiber.Stack.Peek() is < 0 or > 2)
        {
            return "Native model-door translation requires one fullDWORD selector0/1/2; other values leave original X uninitialized";
        }
        NativeFieldModelPartsState? bank = Scene.ModelParts;
        NativeFieldModelInstance? instance = bank?.Instances.SingleOrDefault(item => item.ObjectId == fiber.ObjectId);
        if (NativeRuntimeProfile is not { IsVerified: true } || instance is null || !bank!.Resources.TryGetValue(instance.ModelId, out NativeModelPartResourceState? state) || ReadNativeModelParts?.Invoke(instance.ModelId) is not NativeModelPartsDefinition definition || definition.Parts.Count <= 1)
        {
            return "Native model-door translation requires its executed instance registration and original part1 resource";
        }
        try
        {
            state.Validate(definition);
            definition.RequireDisjointTranslationGeometry();
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
        return null;
    }

    private int[] ExecuteNativeModelDoor(Fiber fiber, bool close)
    {
        int side = fiber.Stack.Peek();
        short x = side switch { 0 => 0, 1 => 32, 2 => -32, _ => throw new NotSupportedException("Unknown native model-door selector.") };
        short z = 207;
        if (close)
        {
            x = unchecked((short)-x);
            z = -207;
        }
        NativeFieldModelPartsState bank = Scene.ModelParts!;
        NativeFieldModelInstance instance = bank.Instances.Single(item => item.ObjectId == fiber.ObjectId);
        NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(instance.ModelId) ?? throw new NotSupportedException("Native model command lost its registered model definition.");
        NativeModelPartResourceState next = bank.Resources[instance.ModelId].StartTranslation(definition, 1, x, 0, z, 30);
        Dictionary<int, NativeModelPartResourceState> resources = new(bank.Resources) { [instance.ModelId] = next };
        int[] args = Pop(fiber, 1);
        Scene.ModelParts = bank with { Resources = resources };
        return args;
    }

    private void StepNativeModelParts()
    {
        if (Scene.NativeBattleFieldReloadState is not null)
        {
            throw new NotSupportedException("Native field drawing cannot run before its pending kind3 resource registration completes.");
        }
        if (Scene.ModelParts is not NativeFieldModelPartsState bank)
        {
            return;
        }
        Dictionary<int, NativeModelPartResourceState> resources = new(bank.Resources);
        Dictionary<int, NativeModelPartDraw> draws = new(bank.Draws);
        //533CD0 visits registered instances, even after UNUSE; HIDE/SHOW own the disabled bit.
        foreach (NativeFieldModelInstance instance in bank.Instances)
        {
            if (!actors[instance.ObjectId].Visible)
            {
                continue;
            }
            NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(instance.ModelId) ?? throw new NotSupportedException("Native model callback lost its original resource definition.");
            NativeModelPartResourceState sampleState = resources[instance.ModelId];
            NativeModelPartStep step = sampleState.Step(definition);
            resources[instance.ModelId] = step.State;
            draws[instance.ObjectId] = new NativeModelPartDraw(Frame, instance.ModelId, sampleState);
            ActorState actor = actors[instance.ObjectId];
            if (CaptureFootstepModelOrigin is not null)
            {
                actor.NativeFootstepModelOrigin = CaptureFootstepModelOrigin(new NativeFootstepProjectionQuery(actor, ReadNativeScriptPosition(actor), instance.ModelId, Frame));
            }
        }
        Scene.ModelParts = bank with { Resources = resources, Draws = draws };
    }

    public IReadOnlyDictionary<int, Vector3>? GetNativeModelPartTranslations(int objectId)
    {
        if (Scene.NativeBattleFieldReloadState is not null)
        {
            throw new NotSupportedException("Native kind3 has cleared model registrations; old draw samples cannot render a replacement frame.");
        }
        if ((uint)objectId >= actors.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(objectId));
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return null;
        }
        NativeFieldModelPartsState bank = Scene.ModelParts ?? throw new NotSupportedException("Visible native model rendering requires its executed registration and skin callback.");
        NativeFieldModelInstance instance = bank.Instances.SingleOrDefault(item => item.ObjectId == objectId) ?? throw new NotSupportedException("Visible native actor has no retained model instance.");
        NativeModelPartDraw draw = bank.Draws.GetValueOrDefault(objectId) ?? throw new NotSupportedException("Visible native model has no executed skin sample; base geometry is not a replacement.");
        if (draw.Frame != Frame || draw.ModelId != instance.ModelId || actors[objectId].ModelId != instance.ModelId)
        {
            //51D8F0 writes actor WORD536 only; it does not rebind531980's retained resource pointer.
            throw new NotSupportedException("Visible native model requires its current callback sample and retained resource identity; a later SETMODEL is not a new registration.");
        }
        NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(instance.ModelId) ?? throw new NotSupportedException("Model presentation requires its registered model definition.");
        return draw.SampleState.Step(definition).SkinTranslations;
    }

    private void ValidateNativeModelParts(SceneEventState scene, IReadOnlyList<ActorState> savedActors, long frame, bool initialized, bool transitionRequested, NativeMovieSource? movieSource)
    {
        if (scene.NativeBattleFieldReloadState is { } pending)
        {
            pending.Validate();
            if (!initialized || scene.ModelParts is not { CacheReset: { } reset } cleared || cleared.Resources is null || cleared.Resources.Count != 0 || cleared.Instances is null || cleared.Instances.Length != 0 || cleared.Draws is null || cleared.Draws.Count != 0)
            {
                throw new InvalidDataException("Pending kind3 registration must retain exactly the actual cleared model caches.");
            }
            reset.Validate();
            return;
        }
        if (scene.ModelParts is not NativeFieldModelPartsState bank)
        {
            if (savedActors.Any(actor => actor.NativeFootstepModelOrigin is not null))
            {
                throw new InvalidDataException("Saved native footstep model-origin receipt has no registered model bank.");
            }
            if (initialized && NativeRuntimeProfile is { IsVerified: true } && ReadNativeModelParts is not null)
            {
                throw new InvalidDataException("Saved native field is missing its executed model-part resource state; migrate this save explicitly.");
            }
            return;
        }
        if (bank.CacheReset is not null)
        {
            throw new InvalidDataException("Cleared5310F0 caches require their actual pending kind3 registration stage.");
        }
        if (!initialized || scene.NativeActorLayoutVersion != 1 || bank.Resources is null || bank.Resources.Values.Any(state => state is null) || bank.Instances is null || bank.Instances.Any(instance => instance is null) || bank.Draws is null || bank.Instances.Length > 32 || bank.Instances.Select(instance => instance.ObjectId).Distinct().Count() != bank.Instances.Length || !bank.Instances.Select(instance => instance.ObjectId).SequenceEqual(scene.NativeActorObjectIds.Where(id => bank.Instances.Any(instance => instance.ObjectId == id))) || bank.Instances.Any(instance => (uint)instance.ObjectId >= savedActors.Count || instance.ModelId < 0 || !bank.Resources.ContainsKey(instance.ModelId)) || !bank.Resources.Keys.Order().SequenceEqual(bank.Instances.Select(instance => instance.ModelId).Distinct().Order()))
        {
            throw new InvalidDataException("Saved native model registrations are malformed.");
        }
        if (savedActors.Any(actor => actor.NativeFootstepModelOrigin is not null && !bank.Instances.Any(instance => instance.ObjectId == actor.ObjectId)))
        {
            throw new InvalidDataException("Saved native footstep model-origin receipt belongs to an unregistered actor.");
        }
        foreach ((int model, NativeModelPartResourceState state) in bank.Resources)
        {
            NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(model) ?? throw new InvalidDataException("Saved native model resource has no actual MCH definition.");
            state.Validate(definition);
        }
        bool registrationOnly = IsNativeModelRegistrationOnly(scene, frame);
        if (registrationOnly && (bank.Draws.Count != 0 || bank.Resources.Values.Any(state => state.Translations.Count != 0) || savedActors.Any(actor => actor.NativeFootstepModelOrigin is not null)))
        {
            throw new InvalidDataException("Saved initial model registration contains a skin callback or part mutation before its first native callback.");
        }
        foreach ((int objectId, NativeModelPartDraw draw) in bank.Draws)
        {
            NativeFieldModelInstance? instance = bank.Instances.SingleOrDefault(item => item.ObjectId == objectId);
            NativeModelPartsDefinition? definition = instance is null ? null : ReadNativeModelParts?.Invoke(instance.ModelId);
            if (draw is null || definition is null || draw.ModelId != instance!.ModelId || draw.Frame < 0 || draw.Frame > frame || draw.SampleState is null)
            {
                throw new InvalidDataException("Saved native model draw samples are malformed.");
            }
            draw.SampleState.Validate(definition);
            NativeFootstepModelOrigin? origin = savedActors[objectId].NativeFootstepModelOrigin;
            if (CaptureFootstepModelOrigin is not null || origin is not null)
            {
                if (origin is null || origin.Version != 1 || origin.Frame != draw.Frame || origin.ModelId != instance!.ModelId || origin.CameraIndex < 0 || (origin.Translation is null) == (origin.UnsupportedReason is null) || origin.UnsupportedReason is not null && string.IsNullOrWhiteSpace(origin.UnsupportedReason))
                {
                    throw new InvalidDataException("Saved native footstep model-origin receipt differs from its executed model draw.");
                }
            }
        }
        if (bank.Instances.Any(instance => savedActors[instance.ObjectId].NativeFootstepModelOrigin is not null && !bank.Draws.ContainsKey(instance.ObjectId)))
        {
            throw new InvalidDataException("Saved native footstep model-origin receipt has no executed model draw.");
        }
        foreach (NativeFieldModelInstance instance in bank.Instances)
        {
            // Native4767B0 dispatches field/world changes before472B30/530810.
            // The dormant field retains its last executed skin sample, not a new draw.
            //52C650's diagnostic transaction registers models without executing530C30.
            if (registrationOnly || transitionRequested || !savedActors[instance.ObjectId].Visible || !NativeModelDrawEnabled(scene, movieSource, frame, true))
            {
                continue;
            }
            if (!bank.Draws.TryGetValue(instance.ObjectId, out NativeModelPartDraw? draw) || draw.Frame != frame)
            {
                throw new InvalidDataException("Saved visible native model has no current executed skin sample.");
            }
            if (!draw.SampleState.Translations.Keys.Order().SequenceEqual(bank.Resources[instance.ModelId].Translations.Keys.Order()))
            {
                throw new InvalidDataException("Saved native model sample omits its translated logical parts.");
            }
        }
    }

}
