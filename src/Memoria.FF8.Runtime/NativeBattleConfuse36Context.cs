namespace Memoria.FF8.Runtime;

/// <summary>85FBD0 retains its already relocated actor directory and uses the shared original task anchor contract.</summary>
public static class NativeBattleConfuse36Context
{
    public static NativeBattleConfuse36Frame Initialize(NativeBattleConfuse36Frame frame, uint address, NativeBattleConfuse36Resources resources, NativeBattleConfuse36ControllerInputs inputs)
    {
        NativeBattleConfuse36Profile profile = NativeBattleConfuse36Profile.ForEffect(resources.Effect);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (frame.Effect.Binding.Effect != resources.Effect || frame.Effect.Binding.Entry != profile.Entry || task.Callback != profile.GenericCallback || task.ReadDword(0x30) != profile.ExecutableAddress)
        {
            throw new InvalidDataException("Native Confuse36 context requires its own original callback and executable pointer.");
        }
        bool initialized = frame.Memory.Confuse36ExecutableInitialized ?? throw new NotSupportedException("Native Confuse requires its retained executable-bank materialization owner; missing historical state is unknown.");
        System.Collections.Immutable.ImmutableArray<byte> executable = initialized ? frame.Memory.Confuse36Executable ?? throw new NotSupportedException("Native Confuse requires its previously materialized mutable executable bank.") : resources.Executable;
        if (executable.IsDefault || executable.Length != 0xBE78 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan().Slice(0xBD74)) != 1)
        {
            throw new InvalidDataException("Native Confuse requires its original already relocated actor directory.");
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native Confuse36 context requires50AFC0 parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, profile.ExecutableAddress);
        hierarchy = hierarchy.Write(address, task);
        NativeBattleEffectMemory memory = frame.Memory with { Confuse36Executable = executable, Confuse36ExecutableInitialized = true };
        return frame with { Memory = memory, Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }
}
