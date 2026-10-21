namespace Memoria.FF8.Runtime;

/// <summary>Curaga's typed owner over the common one-callback node byte view.</summary>
internal sealed class NativeBattleCuragaNodeMemory : NativeBattleEffectNodeMemory
{
    private readonly NativeBattleCuragaFrame frame;
    internal NativeBattleCuragaState State { get; set; }
    internal NativeBattleCuragaFamilyProfile Profile { get; }

    internal NativeBattleCuragaNodeMemory(NativeBattleCuragaFrame frame, uint taskAddress) : base(frame.Memory, frame.Effect, taskAddress, frame.State.Auxiliary25F8 ?? throw new NotSupportedException("Native Curaga emitter storage requires the executed87DE10 allocation/clear."), NativeBattleCuragaFamilyProfile.ForGenericNodes(frame.Effect.Binding.Effect).EmitterStorageLength, frame.State.Auxiliary16F80 ?? throw new NotSupportedException("Native Curaga geometry storage requires the executed87DE10 allocation/clear."), NativeBattleCuragaFamilyProfile.ForGenericNodes(frame.Effect.Binding.Effect).GeometryStorageLength, frame.Effect.Binding.Effect is 16 or 19 or 23 or 37 or 60 or 61 or 73 or 78 or 81 or 82)
    {
        this.frame = frame;
        Profile = NativeBattleCuragaFamilyProfile.ForGenericTask(frame, taskAddress);
        State = frame.State;
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != Profile.Callback(0x87E1A0))
        {
            throw new InvalidDataException("Native Curaga node context requires its own87E1A0 callback.");
        }
    }

    internal NativeBattleCuragaFrame Flush()
    {
        NativeBattleEffectNodeMemoryWrite written = FlushStorage();
        return frame with { Memory = written.Memory, Effect = written.Effect, State = State };
    }
}
