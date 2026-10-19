namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAngelWing84Body
{
    public static NativeBattleAngelWing84Frame Initialize(NativeBattleAngelWing84Frame frame, uint address, NativeBattleAngelWing84BodyResources resources)
    {
        resources.Validate();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x7961E0)
        {
            throw new InvalidDataException("Native Angel Wing body requires its original340-byte task.");
        }
        NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(task, address, resources.Model, 3, resources.DefaultColor44);
        hierarchy = hierarchy.Write(address, initialized.Task);
        NativeBattleBodyBoneMatrices loaded = NativeBattleBodyBoneMatrices.ReadLoadedSkeleton(resources.Model.PoseSource);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { BodyModel = initialized.State, BodyBones = frame.State.BodyBones ?? loaded.Matrices } };
    }
}
