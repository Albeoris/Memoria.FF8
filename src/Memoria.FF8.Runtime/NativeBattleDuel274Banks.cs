namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Banks
{
    public static NativeBattleDuel274Frame DetachUnscheduledPrivateBanks(NativeBattleDuel274Frame frame)
    {
        foreach (uint descriptor in new uint[] { 0x2508498, 0x2508488, 0x2508478 })
        {
            if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor))
            {
                NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
                frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
            }
        }
        return frame;
    }

    public static int Index(NativeBattleDuel274Frame frame, uint descriptor)
    {
        int selected = -1;
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            if (frame.Effect.Pools[index].DescriptorAddress == descriptor)
            {
                if (selected >= 0)
                {
                    throw new InvalidDataException("Native Duel descriptor has multiple physical scheduling owners.");
                }
                selected = index;
            }
        }
        return selected >= 0 ? selected : throw new NotSupportedException("Native Duel descriptor has no physical task bank.");
    }

    public static NativeBattleDuel274Frame Initialize508300(NativeBattleDuel274Frame frame, uint descriptor, uint address, ushort stride, ushort count)
    {
        if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor))
        {
            NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
            frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
        }
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, address, stride, count);
        return frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, [pool]), Effect = frame.Effect with { Pools = frame.Effect.Pools.Add(pool) } };
    }

    public static NativeBattleDuel274Frame Advance508420(NativeBattleDuel274Frame frame, uint descriptor, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274TaskStep> callback)
    {
        int index = Index(frame, descriptor);
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
            NativeBattleDuel274TaskStep step = callback(frame, slot);
            frame = step.Frame;
            return new(step.Pool, step.Result);
        });
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, advanced.Pool) } };
    }
}
