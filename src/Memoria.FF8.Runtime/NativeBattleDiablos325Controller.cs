using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Diablos's root packet-bank parity and ordered timeline creation boundary.</summary>
public static class NativeBattleDiablos325Controller
{
    public static NativeBattleDiablos325Frame Advance654350(NativeBattleDiablos325Frame frame, Func<NativeBattleDiablos325Frame, uint, NativeBattleDiablos325Frame>? publishFlash = null, NativeBattleDiablos325Inputs? inputs = null)
    {
        NativeBattleDiablos325Entry.RequireBinding(frame.Effect);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleDiablos325Entry.MainDescriptor) ?? throw new InvalidDataException("Native Diablos requires its original root pool.");
        if (pool.BankAddress != NativeBattleDiablos325Entry.MainBank || pool.Stride != 0x10 || pool.Slots.Length != 1)
        {
            throw new InvalidDataException("Native Diablos root bank has a different physical layout.");
        }
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback != NativeBattleDiablos325Entry.MainCallback)
            {
                throw new NotSupportedException("Native Diablos root callback was replaced.");
            }
            NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
            bool alternate = task.ReadByte(0xE) == 0;
            state = state with { PacketCursor51D0 = unchecked(state.ModelBuffer5208 + (alternate ? 0xF85Cu : 0x885Cu)), Flash51E8 = 0 };
            task = task.WriteByte(0xE, alternate ? (byte)1 : (byte)0);
            if (task.ReadWord(0xC) == 2 && state.Pause5180 == 0 && task.ReadByte(0xF) == 0)
            {
                if (inputs is null)
                {
                    throw new NotSupportedException("Native Diablos6543BA timeline creation requires its original programme, vibration and physical IO initialization services.");
                }
                task = task.WriteByte(0xF, 1);
                frame = frame with { Memory = frame.Memory with { Diablos325 = state } };
                frame = NativeBattleDiablos325Construction.Initialize6543BA(frame, inputs);
            }
            if (task.ReadByte(0xF) != 0)
            {
                frame = NativeBattleDiablos325Timeline.AdvanceOpening6545F0(frame, inputs ?? throw new NotSupportedException("Native Diablos requires its original ordered timeline services."));
                state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
                NativeBattleEffectTaskPool? particles = frame.Effect.Pools.SingleOrDefault(item => item.DescriptorAddress == 0x2505190);
                if (particles?.Head is not null)
                {
                    throw new NotSupportedException("Native Diablos requires its original ordered particle callback services.");
                }
            }
            frame = frame with { Memory = frame.Memory with { Diablos325 = state } };
            if (state.Flash51E8 != state.PreviousFlash51EC)
            {
                frame = (publishFlash ?? throw new NotSupportedException("Native Diablos flash transition requires its stage and four physical entity flag publications."))(frame, state.Flash51E8);
                state = NativeBattleDiablos325Entry.RequireState(frame.Memory) with { PreviousFlash51EC = state.Flash51E8 };
                frame = frame with { Memory = frame.Memory with { Diablos325 = state } };
            }
            if (state.Pause5180 == 0)
            {
                task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            }
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(item => item.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : item).ToImmutableArray(), ReturnedDescriptor = step.Survivors == 0 ? 0u : NativeBattleDiablos325Entry.MainDescriptor } };
    }
}
