using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame Advance(NativeBattleEffect314Frame frame, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 3) || frame.State.FixedBanks[RootBank] is not null)
        {
            throw new InvalidDataException("Effect314 requires its exclusively active physical task banks.");
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        if (survivors != 0)
        {
            return frame;
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect314 cleanup requires its current COMMON owner.");
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools.Skip(1))
        {
            arena = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffectTaskMemory.CaptureBank(pool), 0, arena, checked((int)(pool.BankAddress - CommonAddress)), pool.Stride * pool.Slots.Length);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = [] }, State = frame.State.AttachBank(RootBank, NativeBattleEffectTaskMemory.CaptureBank(frame.Effect.Pools[0])) };
    }

    private static (NativeBattleEffect314Frame Frame, int Survivors) AdvancePool(NativeBattleEffect314Frame frame, int index, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        NativeBattleEffect314Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            NativeBattleEffect314Step result = pool.Slots[slot].Callback switch
            {
                RootCallback => AdvanceRoot662970(current, address, resources, inputs),
                MainCallback => AdvanceMain662F40(current, address, resources, inputs),
                ParticleCallback => AdvanceParticle664D20(current, address, resources, inputs),
                _ => throw new NotSupportedException("Effect314 reached an unsupported task callback.")
            };
            current = result.Frame;
            return new(current.Effect.Pools[index], result.Result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }
}
