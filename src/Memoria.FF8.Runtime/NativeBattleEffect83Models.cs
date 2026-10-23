namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83ModelStep(NativeBattleEffect83Frame Frame, bool Completed);
public sealed record NativeBattleEffect83DrawOwnedModel(uint TaskAddress) : NativeBattleEffect83Operation;

public static class NativeBattleEffect83Models
{
    public static NativeBattleEffect83Frame Initialize(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(hierarchy.Read(address), address, resources.Model, 3, inputs.ReadColor(frame, 0xB8B9A8));
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, initialized.Task).Pools }, State = frame.State with { OwnedModels = frame.State.OwnedModels.SetItem(address, initialized.State) } };
    }

    public static NativeBattleEffect83Frame Select(NativeBattleEffect83Frame frame, uint address, short animation, NativeBattleEffect83Resources resources)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectOwnedModelStep selected = NativeBattleEffectOwnedModel.Select8DD190(hierarchy.Read(address), Model(frame, address), resources.Model, animation);
        NativeBattleFileStaging staging = NativeBattleEffectOwnedModelRendering.PublishDecodedPose(frame.Staging ?? throw new NotSupportedException("Effect83 pose selection requires its current addressed skeleton."), selected.State, resources.Model);
        return frame with { Staging = staging, Effect = frame.Effect with { Pools = hierarchy.Write(address, selected.Task).Pools }, State = frame.State with { OwnedModels = frame.State.OwnedModels.SetItem(address, selected.State) } };
    }

    public static NativeBattleEffect83ModelStep Advance(NativeBattleEffect83Frame frame, uint address, bool loop, NativeBattleEffect83Resources resources)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectOwnedModelStep step = loop ? NativeBattleEffectOwnedModel.AdvanceLoop8DD1C0(hierarchy.Read(address), Model(frame, address), resources.Model) : NativeBattleEffectOwnedModel.AdvanceOnce8DD220(hierarchy.Read(address), Model(frame, address), resources.Model);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, step.Task).Pools }, State = frame.State with { OwnedModels = frame.State.OwnedModels.SetItem(address, step.State) } };
        if (!step.Completed || loop)
        {
            frame = frame with { Staging = NativeBattleEffectOwnedModelRendering.PublishDecodedPose(frame.Staging ?? throw new NotSupportedException("Effect83 pose advancement requires its current addressed skeleton."), step.State, resources.Model) };
        }
        return new(frame, step.Completed);
    }

    public static NativeBattleEffect83Frame Draw79E780(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleEffectOwnedModel.Validate(new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address), Model(frame, address), resources.Model);
        return inputs.Execute(frame, new NativeBattleEffect83DrawOwnedModel(address));
    }

    private static NativeBattleEffectOwnedModelState Model(NativeBattleEffect83Frame frame, uint address) => frame.State.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model) ? model : throw new NotSupportedException("Effect83 model requires its actual retained decoded owner.");
}
