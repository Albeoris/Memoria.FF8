namespace Memoria.FF8.Runtime;

/// <summary>88D550/8836F0 retain their own executable state, then use the normalized original task anchor contract.</summary>
public static class NativeBattleRestorationContext
{
    public static NativeBattleRestorationFrame Initialize(NativeBattleRestorationFrame frame, uint address, NativeBattleRestorationResources resources, NativeBattleRestorationControllerInputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(resources.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (frame.Effect.Binding.Effect != resources.Effect || frame.Effect.Binding.Entry != profile.Entry || task.Callback != profile.GenericCallback || task.ReadDword(0x30) != profile.ExecutableAddress)
        {
            throw new InvalidDataException("Native restoration context requires its own original callback and executable pointer.");
        }
        NativeBattleRestorationExecutableState retained = frame.Memory.GetRestorationExecutable(resources.Effect) ?? throw new NotSupportedException("Native restoration context requires its own retained initialization owner.");
        using NativeBattleNodeInitialization<NativeBattleRestorationExecutableState> executable = retained.Prepare(resources.Executable ?? throw new NotSupportedException("Native restoration context requires its authenticated original executable resource."));
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native restoration context requires50AFC0 parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, profile.ExecutableAddress);
        hierarchy = hierarchy.Write(address, task);
        NativeBattleEffectMemory memory = frame.Memory.WithRestorationExecutable(executable.Publish());
        return NativeBattleTaskOperation.CompleteStandalone(operation, frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools } });
    }
}
