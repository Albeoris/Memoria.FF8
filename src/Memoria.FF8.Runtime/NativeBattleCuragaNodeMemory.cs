namespace Memoria.FF8.Runtime;

/// <summary>Curaga's typed owner over the common one-callback node byte view.</summary>
internal sealed class NativeBattleCuragaNodeMemory : NativeBattleEffectNodeMemory
{
    private readonly NativeBattleCuragaFrame frame;
    internal NativeBattleCuragaState State { get; set; }

    internal NativeBattleCuragaNodeMemory(NativeBattleCuragaFrame frame, uint taskAddress) : base(frame.Memory, frame.Effect, taskAddress, frame.State.Auxiliary25F8 ?? throw new NotSupportedException("Native Curaga emitter storage requires the executed87DE10 allocation/clear."), 0x25F8, frame.State.Auxiliary16F80 ?? throw new NotSupportedException("Native Curaga geometry storage requires the executed87DE10 allocation/clear."), 0x16F80)
    {
        this.frame = frame;
        State = frame.State;
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != 0x87E1A0)
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
