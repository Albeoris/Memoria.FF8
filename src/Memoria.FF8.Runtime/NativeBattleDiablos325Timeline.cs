using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325Timeline
{
    public static NativeBattleDiablos325Frame AdvanceOpening6545F0(NativeBattleDiablos325Frame frame, NativeBattleDiablos325Inputs inputs)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(item => item.DescriptorAddress == NativeBattleDiablos325Construction.TimelineDescriptor);
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            if (task.Callback != NativeBattleDiablos325Construction.TimelineCallback)
            {
                throw new NotSupportedException("Native Diablos timeline callback was replaced.");
            }
            ushort counter = task.ReadWord(0xC);
            if (counter >= 9)
            {
                throw new NotSupportedException("Native Diablos656B70 opening programme requires its original current transform and primitive resource services.");
            }
            NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
            NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos requires its original files and sound table.");
            NativeBattleEffectMemory memory = frame.Memory;
            if (counter == 0)
            {
                if (resources.Sounds.IsDefaultOrEmpty || inputs.PlaySound is null)
                {
                    throw new NotSupportedException("Native Diablos requires its original first sound descriptor and service.");
                }
                memory = NativeBattleEffectIo.Queue5341D0(memory, 727, checked(state.ModelBuffer5208 + 0x1685C), 3);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 728, checked(state.ModelBuffer5208 + 0x1685C), 2);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 729, checked(state.ModelBuffer5208 + 0x2185C), 5);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 730, checked(state.ModelBuffer5208 + 0x3505C), 5);
                memory = NativeBattleEffectIo.Queue5341D0(memory, 731, checked(state.ModelBuffer5208 + 0x1685C), 5);
            }
            else
            {
                state = state with { Flash51E8 = checked(256u + (uint)(counter - 1) * 512) };
                memory = memory with { Diablos325 = state };
            }
            NativeBattleEffectIoAdvance advanced = NativeBattleEffectIo.Advance534210(memory);
            frame = frame with { Memory = advanced.Memory };
            if (advanced.Request is { } request)
            {
                frame = (inputs.QueueFile ?? throw new NotSupportedException("Native Diablos requires its original file delivery service."))(frame, request);
            }
            if (counter == 0)
            {
                inputs.PlaySound!(resources.Sounds[0], 1, 128);
            }
            task = task.WriteWord(0xC, checked((ushort)(counter + 1)));
            frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(12, task.Payload.AsSpan()) } };
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(item => item.DescriptorAddress == step.Pool.DescriptorAddress ? step.Pool : item).ToImmutableArray() } };
    }
}
