namespace Memoria.FF8.Runtime;

/// <summary>87E230's676-byte task context and original87E4C0/87E810 anchor vectors; no separate reconstructed actor state.</summary>
public static class NativeBattleCuragaContext
{
    public static NativeBattleCuragaFrame Initialize87E230(NativeBattleCuragaFrame frame, uint address, NativeBattleCuragaResources resources, NativeBattleCuragaControllerInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x87E1A0 || task.ReadDword(0x30) != NativeBattleCuragaExecutableState.Address)
        {
            throw new NotSupportedException("Native87E230 requires its actual676-byte callback and17D5F4C executable resource.");
        }
        NativeBattleCuragaExecutableState executable = (frame.Memory.CuragaExecutable ?? throw new NotSupportedException("Native87E230 requires the retained Curaga executable initialization owner.")).Initialize87EA20(resources.Executable ?? throw new NotSupportedException("Native87EA20 requires the authenticated executable source."));
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native87E230 requires its50AFC0 header base."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, NativeBattleCuragaExecutableState.Address);
        hierarchy = hierarchy.Write(address, task);
        return frame with { Memory = frame.Memory with { CuragaExecutable = executable }, Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }
}
