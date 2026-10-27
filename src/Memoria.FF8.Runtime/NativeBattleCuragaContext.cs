namespace Memoria.FF8.Runtime;

/// <summary>87E230's676-byte task context and original87E4C0/87E810 anchor vectors; no separate reconstructed actor state.</summary>
public static class NativeBattleCuragaContext
{
    public static NativeBattleCuragaFrame Initialize87E230(NativeBattleCuragaFrame frame, uint address, NativeBattleCuragaResources resources, NativeBattleCuragaControllerInputs inputs)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericTask(frame, address);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != profile.Callback(0x87E1A0) || task.ReadDword(0x30) != profile.ExecutableAddress)
        {
            throw new NotSupportedException("Native87E230 requires its actual676-byte callback and17D5F4C executable resource.");
        }
        if ((profile.Effect is 16 or 61 or 81 or 82) && unchecked((short)task.ReadWord(0x29C)) != 0)
        {
            throw new NotSupportedException("The actor director generic producer supports only its original mode0 anchors.");
        }
        using NativeBattleNodeInitialization<NativeBattleCuragaExecutableState> executable = profile.RequireExecutable(frame.Memory).Prepare87EA20(resources.Executable ?? throw new NotSupportedException("Native87EA20 requires the authenticated executable source."));
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native87E230 requires its50AFC0 header base."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, profile.ExecutableAddress, observeBone: (actor, selector) => frame = inputs.ObserveBoneAnchor?.Invoke(frame, actor, selector) ?? frame);
        hierarchy = hierarchy.Write(address, task);
        return NativeBattleTaskOperation.CompleteStandalone(operation, frame with { Memory = profile.WithExecutable(frame.Memory, executable.Publish()), Effect = frame.Effect with { Pools = hierarchy.Pools } });
    }
}
