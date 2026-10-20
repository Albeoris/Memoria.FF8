namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89RootInputs(Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRinoa89Frame, int, NativeBattleRinoa89Frame> PrepareBody, Func<NativeBattleRinoa89Frame, uint, int, NativeBattleRinoa89TaskStep> AdvanceChild);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame AdvanceRoot6F9D80(NativeBattleRinoa89Frame frame, NativeBattleRinoa89RootInputs inputs)
    {
        int rootIndex = Find(Root);
        if (rootIndex < 0)
        {
            throw new InvalidDataException("Native6F9D80 requires its original root descriptor.");
        }
        NativeBattleEffectTaskStep advanced = frame.Effect.Pools[rootIndex].Advance((pool, slot) =>
        {
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(rootIndex, pool) } };
            frame = SelectRootPacketBank(frame, slot);
            pool = frame.Effect.Pools[rootIndex];
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.ReadWord(12) == 2)
            {
                frame = SnapshotModels6FA270(frame, inputs.ReadModel);
                if (task.ReadByte(15) == 0)
                {
                    task = task.WriteByte(15, 1);
                    Publish();
                    frame = InitializePools6F9D80(frame);
                }
                int bodyIndex = Index(Body);
                NativeBattleEffectTaskPool body = frame.Effect.Pools[bodyIndex];
                if (body.Slots.Any(current => (current.Flags & 1) == 0))
                {
                    (body, int child) = body.Append(0x6FA780);
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(bodyIndex, body) } };
                    Publish();
                    frame = inputs.PrepareBody(frame, child);
                }
            }
            int bodySurvivors = 1;
            if (task.ReadByte(15) != 0)
            {
                bodySurvivors = Advance(Body);
                Advance(Sprites);
                Advance(Beams);
            }
            pool = frame.Effect.Pools[rootIndex];
            int result = task.ReadByte(15) != 0 && bodySurvivors == 0 ? 2 : 0;
            if (result == 0)
            {
                task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            }
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            return new(pool, result);

            void Publish()
            {
                pool = frame.Effect.Pools[rootIndex];
                pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(rootIndex, pool) } };
            }
            int Advance(uint descriptor)
            {
                int index = Index(descriptor);
                NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((current, childSlot) =>
                {
                    index = Index(descriptor);
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, current) } };
                    NativeBattleRinoa89TaskStep child = inputs.AdvanceChild(frame, descriptor, childSlot);
                    frame = child.Frame;
                    return new(child.Pool, child.Result);
                });
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(Index(descriptor), step.Pool) } };
                return step.Survivors;
            }
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(rootIndex, advanced.Pool), ReturnedDescriptor = advanced.Survivors == 0 ? 0u : Root } };
        if (advanced.Survivors == 0)
        {
            foreach (uint descriptor in new uint[] { Body, Sprites, Beams })
            {
                if (frame.Effect.Pools.Any(pool => pool.DescriptorAddress == descriptor))
                {
                    NativeBattleEffectTaskTransfer detached = NativeBattleEffectTaskMemory.DetachArenaBank(frame.Memory, frame.Effect, descriptor);
                    frame = frame with { Memory = detached.Memory, Effect = detached.Effect };
                }
            }
        }
        return frame;

        int Find(uint descriptor)
        {
            for (int index = 0; index < frame.Effect.Pools.Length; index++)
            {
                if (frame.Effect.Pools[index].DescriptorAddress == descriptor)
                {
                    return index;
                }
            }
            return -1;
        }
        int Index(uint descriptor)
        {
            int index = Find(descriptor);
            return index >= 0 ? index : throw new NotSupportedException("Native6F9D80 lost its required child descriptor.");
        }
    }
}
