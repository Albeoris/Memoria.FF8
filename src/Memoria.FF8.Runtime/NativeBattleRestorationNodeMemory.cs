namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleRestorationNodeMemory : NativeBattleEffectNodeMemory
{
    private readonly NativeBattleRestorationFrame frame;
    internal NativeBattleRestorationState State { get; set; }
    internal NativeBattleRestorationProfile Profile { get; }

    internal NativeBattleRestorationNodeMemory(NativeBattleRestorationFrame frame, uint taskAddress) : base(frame.Memory, frame.Effect, taskAddress, frame.State.Emitters ?? throw new NotSupportedException("Native restoration requires its executed emitter-pool allocation/clear."), NativeBattleRestorationProfile.ForEffect(frame.Effect.Binding.Effect).EmitterBytes, frame.State.Geometry ?? throw new NotSupportedException("Native restoration requires its executed geometry-pool allocation/clear."), NativeBattleRestorationProfile.ForEffect(frame.Effect.Binding.Effect).GeometryBytes)
    {
        this.frame = frame;
        State = frame.State;
        Profile = NativeBattleRestorationProfile.ForEffect(frame.Effect.Binding.Effect);
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != Profile.GenericCallback)
        {
            throw new InvalidDataException("Native restoration node context requires its own original generic callback.");
        }
    }

    internal NativeBattleRestorationFrame Flush()
    {
        NativeBattleEffectNodeMemoryWrite written = FlushStorage();
        return frame with { Memory = written.Memory, Effect = written.Effect, State = State };
    }
}
