namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurePacketOperation(NativeBattleModulePacketBatch Batch) : NativeBattleCureOperation;

public static partial class NativeBattleCureParticles
{
    public static NativeBattleCureParticleStep AdvanceWedge8DCC20(NativeBattleCureFrame frame, uint address, NativeBattleCureParticleInputs inputs)
    {
        NativeBattleCureFrame current = frame;
        NativeBattleEffectWedgeStep step = NativeBattleEffectWedge.Advance8DCC20(new(frame.Memory, frame.Effect.Pools), address, inputs.CrtRandom, target => inputs.Controller.ReadModel(target).Transform ?? throw new NotSupportedException("Native8DCB80 requires the target's original position WORDs."), inputs.Projection, Execute, frame.Effect.Binding.Effect == 1 ? (ushort)1 : null);
        return new(current with { Memory = step.Frame.Memory, Effect = current.Effect with { Pools = step.Frame.Pools } }, step.CrtRandomAfter, step.Result);

        NativeBattleEffectWedgeFrame Execute(NativeBattleEffectWedgeFrame state, NativeBattleModulePacketBatch batch)
        {
            current = inputs.Execute(current with { Memory = state.Memory, Effect = current.Effect with { Pools = state.Pools } }, new NativeBattleCurePacketOperation(batch));
            return new(current.Memory, current.Effect.Pools);
        }
    }

    public static NativeBattleModulePacketBatch EmitWedge8DCC20(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection)
    {
        return NativeBattleEffectWedge.Emit8DCC20(task, projection);
    }
}
