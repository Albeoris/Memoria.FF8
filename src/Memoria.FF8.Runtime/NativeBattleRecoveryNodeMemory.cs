namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleRecoveryNodeMemory : NativeBattleEffectNodeMemory
{
    private readonly NativeBattleRecoveryFrame frame;
    internal NativeBattleRecoveryState State { get; set; }
    internal NativeBattleRecoveryProfile Profile { get; }

    internal NativeBattleRecoveryNodeMemory(NativeBattleRecoveryFrame frame, uint taskAddress) : base(frame.Memory, frame.Effect, taskAddress, frame.State.Emitters ?? throw new NotSupportedException("Native recovery requires its executed emitter-pool allocation/clear."), NativeBattleRecoveryProfile.ForEffect(frame.Effect.Binding.Effect).EmitterBytes, frame.State.Geometry ?? throw new NotSupportedException("Native recovery requires its executed geometry-pool allocation/clear."), NativeBattleRecoveryProfile.ForEffect(frame.Effect.Binding.Effect).GeometryBytes)
    {
        this.frame = frame;
        State = frame.State;
        Profile = NativeBattleRecoveryProfile.ForEffect(frame.Effect.Binding.Effect);
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != Profile.GenericCallback)
        {
            throw new InvalidDataException("Native recovery node context requires its own original generic callback.");
        }
    }

    internal NativeBattleRecoveryFrame Flush()
    {
        NativeBattleEffectNodeMemoryWrite written = FlushStorage();
        return frame with { Memory = written.Memory, Effect = written.Effect, State = State };
    }
}
