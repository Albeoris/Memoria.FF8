using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    public Func<NativeFieldRegistrationResources?>? ReadNativeFieldRegistrationResources { get; set; }

    private void InitializeNativeFieldEyeProcess()
    {
        if (ReadNativeFieldRegistrationResources?.Invoke() is not { } resources)
        {
            return;
        }
        if (services is not GameSession session || session.CurrentNativeBattleMemory is null)
        {
            return;
        }
        NativeBattleRetainedMemory retained = session.ActivateNativeFieldMemory();
        NativeFieldRetainedActor[] retainedActors = Scene.NativeActorObjectIds.Select(id => new NativeFieldRetainedActor(id, actors[id].NativeAnimationHeader?.BoundModelId ?? throw new NotSupportedException("Native initial registration requires each actual completed model binding."), program!.Objects[id].Label, program.Objects[id].Label + program.Objects[id].Scripts.Count)).ToArray();
        NativeFieldRegistrationStep step = NativeFieldRegistrationMachine.ReloadKind3(resources, retainedActors, retained);
        Scene.NativeFieldRegistration = step.State;
        PublishNativeFieldEyeProcess(new(1, step.Memory, session.State.CrtRandom));
    }

    private void PublishNativeFieldEyeProcess(NativeFieldEyeProcess process)
    {
        process.Validate(Scene.NativeFieldRegistration ?? throw new InvalidDataException("Native eye process lost its actual registered instances."));
        if (services is not GameSession session)
        {
            throw new NotSupportedException("Native eye process requires its shared game-session owners.");
        }
        process = process.RestoreOwned();
        session.SetNativeFieldEyeOwners(process.Memory, process.CrtRandom);
        Scene.NativeEyeProcess = process;
    }

    public void PublishNativeFieldTextureAnimations(NativeFieldTextureAnimations animations)
    {
        NativeFieldRegistrationState registration = Scene.NativeFieldRegistration ?? throw new InvalidDataException("Native plane binding lost its actual field registration.");
        NativeFieldEyeProcess process = ReadCurrentNativeEyeProcess(Scene.NativeEyeProcess ?? throw new InvalidDataException("Native plane binding requires its retained graphics process."));
        animations.Validate();
        NativeFieldTextureAnimations current = process.Memory.FieldTextureAnimations ?? throw new InvalidDataException("Native plane binding lost its actual texture animation owner.");
        if (animations.OneHash != registration.OneHash || animations.OneHash != current.OneHash || animations.Planes?.ExecutableHash != current.Planes?.ExecutableHash || !animations.Textures.SequenceEqual(current.Textures))
        {
            throw new InvalidDataException("Native plane binding belongs to another registered source or graphics profile.");
        }
        PublishNativeFieldEyeProcess(process with { Memory = process.Memory with { FieldTextureAnimations = animations } });
    }

    private NativeFieldEyeProcess PrepareNativeBlinkEyes(Fiber fiber)
    {
        if (fiber.Stack.Count < 1 || !fiber.Stack.PeekValue().IsExact || Scene.NativeEyeProcess is not { } process || Scene.NativeFieldRegistration is not { } registration)
        {
            throw new NotSupportedException("BLINKEYES requires its exact DWORD operand and actual registered CPU/VRAM/CRT owners.");
        }
        int? bound = actors[fiber.ObjectId].NativeAnimationHeader?.BoundModelId;
        if (bound is >= 0 && ReadNativeEyes?.Invoke(bound.Value) is null)
        {
            throw new NotSupportedException("BLINKEYES requires its authenticated registered model eye definition.");
        }
        process = ReadCurrentNativeEyeProcess(process);
        NativeBattleFieldResourceReloadResult result = NativeFieldRegistrationMachine.SetEyes532340(registration, process.Memory, fiber.ObjectId, 0x80000, unchecked((byte)fiber.Stack.Peek()), process.CrtRandom);
        return new(1, result.Memory, result.CrtRandomAfter);
    }

    private int[] ExecuteNativeBlinkEyes(Fiber fiber)
    {
        NativeFieldEyeProcess process = PrepareNativeBlinkEyes(fiber);
        int[] args = Pop(fiber, 1);
        ActorState actor = actors[fiber.ObjectId];
        actor.NativeEyeBlinkParameter611 = unchecked((byte)args[0]);
        int? model = actor.NativeAnimationHeader?.BoundModelId;
        NativeFieldEyeDefinition? definition = model is int id ? ReadNativeEyes?.Invoke(id) : null;
        actor.NativeEyes = new(1, 0x80000, model, definition?.Bank.Length > 0 ? (byte)0x10 : null, null);
        PublishNativeFieldEyeProcess(process);
        return args;
    }

    private void PublishNativeExplicitEyes(ActorState actor, bool closed)
    {
        NativeActorEyes eyes = PrepareNativeEyes(actor, closed);
        if (Scene.NativeEyeProcess is { } process && Scene.NativeFieldRegistration is { } registration)
        {
            process = ReadCurrentNativeEyeProcess(process);
            NativeBattleFieldResourceReloadResult result = NativeFieldRegistrationMachine.SetEyes532340(registration, process.Memory, actor.ObjectId, eyes.Mode, null, process.CrtRandom);
            PublishNativeFieldEyeProcess(new(1, result.Memory, result.CrtRandomAfter));
        }
        actor.NativeEyes = eyes;
    }

    private void AdvanceNativeEyeProcess()
    {
        if (Scene.NativeEyeProcess is not { } process || Scene.NativeFieldRegistration is not { } registration)
        {
            return;
        }
        process = ReadCurrentNativeEyeProcess(process);
        NativeBattleFieldResourceReloadResult result = NativeFieldRegistrationMachine.AdvanceEyes530810(registration, process.Memory, process.CrtRandom);
        PublishNativeFieldEyeProcess(new(1, result.Memory, result.CrtRandomAfter));
        foreach (NativeFieldRegisteredInstance instance in registration.Instances)
        {
            ActorState actor = actors[instance.ObjectId];
            if (instance.ModelId >= 0 && actor.NativeEyes is { Mode: 0x80000 } eyes && ReadNativeEyes?.Invoke(instance.ModelId) is { Bank.Length: > 0 })
            {
                byte backend = (byte)(result.Memory.FileStaging!.Read(instance.Address + 114, 1)[0] & 0xF0);
                actor.NativeEyes = eyes with { BackendHighNibble114 = backend };
            }
        }
    }

    private NativeFieldEyeProcess ReadCurrentNativeEyeProcess(NativeFieldEyeProcess cached)
    {
        if (services is GameSession session && OwnsNativeFieldEyeProcess(session))
        {
            NativeBattleRetainedMemory memory = session.CurrentNativeBattleMemory ?? throw new NotSupportedException("Native eye process lost its current shared memory owner.");
            return new(1, memory, session.State.CrtRandom);
        }
        return cached;
    }

    private bool OwnsNativeFieldEyeProcess(GameSession session)
    {
        return !session.HasActiveBattle && session.State.Scene == SceneKind.Field && session.State.FieldId == FieldId;
    }

    private SceneEventState CaptureNativeEyeProcessScene()
    {
        if (Scene.NativeEyeProcess is not { } process)
        {
            return Scene;
        }
        process = ReadCurrentNativeEyeProcess(process);
        NativeFieldEyeProcess detached = services is GameSession session && OwnsNativeFieldEyeProcess(session)
            ? process with { Memory = session.CaptureNativeFieldMemory() ?? throw new InvalidDataException("Field checkpoint lost its retained memory.") }
            : process.CaptureSnapshot();
        return Scene.CreateEyeProcessCheckpointProjection(detached);
    }

    private NativeFieldEyeProcess? ValidateNativeEyeProcessCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeEyeProcess is { } process)
        {
            NativeFieldRegistrationState registration = saved.Scene.NativeFieldRegistration ?? throw new InvalidDataException("Saved eye process lost its actual registration.");
            process.Validate(registration);
            if (registration.FieldId != saved.FieldId || registration.ProgramHash != saved.ProgramHash || !registration.Instances.Select(instance => instance.ObjectId).SequenceEqual(saved.Scene.NativeActorObjectIds))
            {
                throw new InvalidDataException("Saved eye process belongs to another field or physical instance order.");
            }
            return process.RestoreOwned();
        }
        return null;
    }
}
