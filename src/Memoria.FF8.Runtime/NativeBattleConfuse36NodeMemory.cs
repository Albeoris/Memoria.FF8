namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleConfuse36NodeMemory : NativeBattleEffectNodeMemory
{
    private readonly NativeBattleConfuse36Frame frame;
    internal NativeBattleConfuse36State State { get; set; }
    internal NativeBattleConfuse36Profile Profile { get; }

    internal NativeBattleConfuse36NodeMemory(NativeBattleConfuse36Frame frame, uint taskAddress) : base(frame.Memory, frame.Effect, taskAddress, frame.State.Emitters ?? throw new NotSupportedException("Native Confuse36 requires its executed emitter-pool allocation/clear."), NativeBattleConfuse36Profile.ForEffect(frame.Effect.Binding.Effect).EmitterBytes, frame.State.Geometry ?? throw new NotSupportedException("Native Confuse36 requires its executed geometry-pool allocation/clear."), NativeBattleConfuse36Profile.ForEffect(frame.Effect.Binding.Effect).GeometryBytes, true)
    {
        this.frame = frame;
        State = frame.State;
        Profile = NativeBattleConfuse36Profile.ForEffect(frame.Effect.Binding.Effect);
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != Profile.GenericCallback)
        {
            throw new InvalidDataException("Native Confuse36 node context requires its own original generic callback.");
        }
    }

    internal NativeBattleConfuse36Frame Flush()
    {
        NativeBattleEffectNodeMemoryWrite written = FlushStorage();
        return frame with { Memory = written.Memory, Effect = written.Effect, State = State };
    }
}
