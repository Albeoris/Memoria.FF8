namespace Memoria.FF8.Runtime;

/// <summary>812540 and its normalized recovery profiles retain their own executable state, then use the normalized original task anchor contract.</summary>
public static class NativeBattleRecoveryContext
{
    public static NativeBattleRecoveryFrame Initialize(NativeBattleRecoveryFrame frame, uint address, NativeBattleRecoveryResources resources, NativeBattleRecoveryControllerInputs inputs)
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(resources.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (frame.Effect.Binding.Effect != resources.Effect || frame.Effect.Binding.Entry != profile.Entry || task.Callback != profile.GenericCallback || task.ReadDword(0x30) != profile.DirectoryAddress)
        {
            throw new InvalidDataException("Native recovery context requires its own original callback and executable pointer.");
        }
        NativeBattleRecoveryExecutableState retained = frame.Memory.GetRecoveryExecutable(resources.Effect) ?? throw new NotSupportedException("Native recovery context requires its own retained initialization owner.");
        NativeBattleRecoveryExecutableState executable = retained.Initialize(resources.Executable ?? throw new NotSupportedException("Native recovery context requires its authenticated original executable resource."));
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native recovery context requires50AFC0 parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, profile.DirectoryAddress, true);
        hierarchy = hierarchy.Write(address, task);
        NativeBattleEffectMemory memory = frame.Memory.WithRecoveryExecutable(executable);
        return frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }
}
