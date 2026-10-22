using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDoom8Models
{
    public static NativeBattleEffectOwnedModelState Read(NativeBattleDoom8Frame frame, uint address)
    {
        return RequireModels(frame.State).SingleOrDefault(model => model.TaskAddress == address) ?? throw new NotSupportedException("Doom requires its executed model initialization.");
    }

    public static NativeBattleDoom8Frame Initialize(NativeBattleDoom8Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, ushort argument3, uint defaultColor44)
    {
        ImmutableArray<NativeBattleEffectOwnedModelState> models = RequireModels(frame.State);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Doom requires its addressed model source memory.");
        bool loaded = frame.State.ReaperSourceLoaded ?? throw new NotSupportedException("Doom requires its retained model source initialization owner.");
        if (resource.Address != 0x1636CE8)
        {
            throw new InvalidDataException("Doom model initialization selected an unavailable source.");
        }
        if (!loaded)
        {
            staging = staging.Write(resource.Address, resource.Bytes.AsSpan());
        }
        NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.Initialize8DD0F0(NativeBattleDoom8Controller.Read(frame, address), address, resource, argument3, defaultColor44);
        frame = NativeBattleDoom8Controller.Write(frame, address, step.Task);
        return frame with { Staging = staging, State = frame.State with { ReaperSourceLoaded = true, Models = models.Where(model => model.TaskAddress != address).Append(step.State).ToImmutableArray() } };
    }

    public static NativeBattleDoom8Frame Select(NativeBattleDoom8Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, short animation)
    {
        NativeBattleEffectOwnedModelStep step = NativeBattleEffectOwnedModel.Select8DD190(NativeBattleDoom8Controller.Read(frame, address), Read(frame, address), resource, animation);
        return Publish(frame, step);
    }

    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, bool loop)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        NativeBattleEffectOwnedModelState model = Read(frame, address);
        NativeBattleEffectOwnedModelStep step = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(task, model, resource) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(task, model, resource);
        return new(Publish(frame, step), step.Completed ? 1 : 0);
    }

    private static NativeBattleDoom8Frame Publish(NativeBattleDoom8Frame frame, NativeBattleEffectOwnedModelStep step)
    {
        ImmutableArray<NativeBattleEffectOwnedModelState> models = RequireModels(frame.State);
        frame = NativeBattleDoom8Controller.Write(frame, step.State.TaskAddress, step.Task);
        return frame with { State = frame.State with { Models = models.Select(model => model.TaskAddress == step.State.TaskAddress ? step.State : model).ToImmutableArray() } };
    }

    private static ImmutableArray<NativeBattleEffectOwnedModelState> RequireModels(NativeBattleDoom8State state)
    {
        return state.Models is { IsDefault: false } models ? models : throw new NotSupportedException("Doom requires its retained effect model owner map.");
    }
}
