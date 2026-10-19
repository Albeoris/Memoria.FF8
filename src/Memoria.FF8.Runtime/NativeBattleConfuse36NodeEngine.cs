namespace Memoria.FF8.Runtime;

/// <summary>860620 uses the shared original context phase kernel and its own per-pass count words.</summary>
public static class NativeBattleConfuse36NodeEngine
{
    public static NativeBattleConfuse36ContextStep Advance(NativeBattleConfuse36Frame frame, uint contextTask, Func<NativeBattleConfuse36Frame, uint, NativeBattleConfuse36Frame> birthUpdateAndDraw)
    {
        NativeBattleConfuse36Profile profile = NativeBattleConfuse36Profile.ForEffect(frame.Effect.Binding.Effect);
        if (Read().Callback != profile.GenericCallback)
        {
            throw new InvalidDataException("Native Confuse36 node engine requires its original generic context task.");
        }
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(Read, Write, () => frame = birthUpdateAndDraw(frame, contextTask));
        ushort emitters = frame.State.PassEmitterCount ?? throw new NotSupportedException("Native88DFA0/884140 requires its actual per-pass emitter count reset.");
        ushort geometry = frame.State.PassGeometryCount ?? throw new NotSupportedException("Native88DFA0/884140 requires its actual per-pass geometry count reset.");
        return new(frame with { State = frame.State with { PassEmitterCount = unchecked((ushort)(emitters + step.EmitterCount)), PassGeometryCount = unchecked((ushort)(geometry + step.GeometryCount)) } }, step.Complete);

        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(contextTask);

        void Write(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(contextTask, task).Pools } };
        }
    }
}
