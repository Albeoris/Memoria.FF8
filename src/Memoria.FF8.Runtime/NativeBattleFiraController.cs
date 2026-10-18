namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiraFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleFiraPosition(short X, short Y, short Z, short Fourth);
public abstract record NativeBattleFiraOperation;
public sealed record NativeBattleFiraDraw(NativeBattleModulePoolTask Task) : NativeBattleFiraOperation;
public sealed record NativeBattleFiraContact(int Contact) : NativeBattleFiraOperation;
public sealed record NativeBattleFiraSound(int Owner) : NativeBattleFiraOperation;
public sealed record NativeBattleFiraStage(short Value) : NativeBattleFiraOperation;
public sealed record NativeBattleFiraCommonTextureLoad(int DestinationArenaOffset, uint CompletionAddress) : NativeBattleFiraOperation;
public sealed record NativeBattleFiraInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleFiraPosition> ReadPosition, Func<NativeBattleFiraFrame, NativeBattleFiraOperation, NativeBattleFiraFrame> Execute);

/// <summary>61CE10/61DD90's retained task banks and complete reached61CEC0 child lifecycle.</summary>
public static class NativeBattleFiraController
{
    public const uint MainDescriptor = 0x24BFB68;
    public const uint MainBank = 0x24BFB48;
    public const uint ChildDescriptor = 0x24BFB78;
    public const uint ChildBank = 0x24BFB88;

    public static NativeBattleOriginalEffectState Initialize61CE10(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x61CE10 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Fira requires its original61CE10 entry and actual50AFC0 parameters.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 16, 2);
        (main, int root) = main.Append(0x61DD90);
        main = Put(main, root, main.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 150);
        (children, int controller) = children.Append(0x61CEC0);
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters.HeaderIndex7C];
        children = Put(children, controller, InitializeController(children.Slots[controller], 0, header, packets));
        return effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress };
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        NativeBattleEffectTaskPool pool = effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        long offset = (long)address - MainBank - 14;
        if (pool.BankAddress != MainBank || pool.Stride != 16 || pool.Slots.Length != 2 || offset < 0 || offset % 16 != 0 || offset / 16 >= pool.Slots.Length)
        {
            throw new InvalidDataException("Native Fira completion must address an actual main-task completion byte.");
        }
        int slot = (int)(offset / 16);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if ((task.Flags & 1) == 0 || task.Callback != 0x61DE10)
        {
            throw new InvalidDataException("Native Fira completion requires its live61DE10 task owner.");
        }
        task = task.WriteWord(14, (ushort)((task.ReadWord(14) & 0xFF00) | value));
        int index = effect.Pools.IndexOf(pool);
        return effect with { Pools = effect.Pools.SetItem(index, Put(pool, slot, task)) };
    }

    public static NativeBattleFiraFrame Advance61DD90(NativeBattleFiraFrame frame, NativeBattleFiraInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Fira lost its actual action parameter owner.");
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        bool frozen = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep mainStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback == 0x61DD90)
            {
                frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (task.ReadWord(12) & 1) != 0 ? 0x10000 : 0 } };
                NativeBattleEffectTaskStep step = children.Advance(Child);
                children = step.Pool;
                if (step.Survivors == 0 && (parameters.Flags79 & 2) == 0)
                {
                    (pool, int final) = pool.Append(0x61DE10);
                    pool = Put(pool, final, pool.Slots[final].WriteWord(12, 0).WriteWord(14, 0));
                }
                return new(Put(pool, slot, Add(task, 12, 1)), step.Survivors == 0 ? 2 : 0);
            }
            if (task.Callback != 0x61DE10)
            {
                throw new NotSupportedException($"Native Fira main callback{task.Callback:X8} is unsupported.");
            }
            if (Word(task, 12) == 1)
            {
                frame = frame with { Effect = frame.Effect with { Pools = [pool, children] } };
                frame = inputs.Execute(frame, new NativeBattleFiraCommonTextureLoad(0, MainBank + (uint)slot * 16 + 14));
                pool = frame.Effect.Pools.Single(current => current.DescriptorAddress == MainDescriptor);
                children = frame.Effect.Pools.Single(current => current.DescriptorAddress == ChildDescriptor);
                task = pool.Slots[slot];
            }
            bool complete = task.ReadWord(14) != 0;
            return new(Put(pool, slot, Add(task, 12, 1)), complete ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [mainStep.Pool, children], ReturnedDescriptor = mainStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Child(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            short phase = Word(task, 12);
            if (task.Callback == 0x61CEC0)
            {
                if (frozen)
                {
                    return new(pool, 0);
                }
                int sequence = Word(task, 14);
                int last = Header(0).ReadByte(17);
                if (phase == 0)
                {
                    task = WorkingPosition(task, 16, inputs.ReadPosition(Word(task, 32)));
                    task = WorkingPosition(task, 24, inputs.ReadPosition(Word(task, 34)));
                }
                if (phase == 1)
                {
                    (pool, int child) = pool.Append(0x61DAF0);
                    NativeBattleModulePoolTask next = Position(pool.Slots[child], task, 24).WriteWord(12, 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        next = next.WriteWord(24 + 2 * axis, unchecked((ushort)((Word(task, 16 + 2 * axis) - Word(task, 24 + 2 * axis)) / 8)));
                    }
                    pool = Put(pool, child, next);
                }
                if (phase == 9)
                {
                    (pool, int child) = pool.Append(0x61D9A0);
                    NativeBattleModulePoolTask next = Position(pool.Slots[child], task).WriteWord(12, 0).WriteWord(24, (ushort)Rand(4096)).WriteWord(26, (ushort)(Rand(40) + 70)).WriteWord(28, 0x104F).WriteWord(32, 0).WriteWord(34, 0x2B7);
                    pool = Put(pool, child, next);
                }
                if (phase == 11)
                {
                    for (int index = 0; index < 3; index++)
                    {
                        (pool, int child) = pool.Append(0x61D7F0);
                        NativeBattleModulePoolTask next = Position(pool.Slots[child], task).WriteWord(12, 0).WriteWord(14, (ushort)(Rand(6) + index));
                        next = Add(next, 18, -600 * (index + 1));
                        next = Add(next, 18, Rand(300) - 150).WriteWord(24, (ushort)Rand(4096)).WriteWord(26, (ushort)(Rand(90) + 40)).WriteWord(32, 0x9C9);
                        next = next.WriteWord(28, unchecked((ushort)(((Rand(600) + 1200) << 12) / 981)));
                        next = next.WriteWord(30, unchecked((ushort)(Word(next, 28) / 30)));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase == 9)
                {
                    (pool, int child) = pool.Append(0x61D6B0);
                    pool = Put(pool, child, Position(pool.Slots[child], task).WriteWord(12, 0).WriteWord(24, (ushort)Rand(4096)).WriteWord(28, 0x1C00));
                }
                if (phase is 10 or 11)
                {
                    for (int index = 0; index < 10; index++)
                    {
                        (pool, int child) = pool.Append(0x61D5D0);
                        NativeBattleModulePoolTask next = Position(pool.Slots[child], task).WriteWord(12, 0).WriteWord(14, (ushort)(Rand(6) + index));
                        next = Add(next, 16, Rand(1800) - 900);
                        next = Add(next, 18, -Rand(1100) - 400);
                        next = Add(next, 20, Rand(1800) - 900).WriteWord(22, unchecked((ushort)(-40 - Rand(100)))).WriteWord(28, (ushort)(Rand(1280) + 2560));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase is >= 9 and <= 11)
                {
                    for (int index = 0; index < 12; index++)
                    {
                        (pool, int child) = pool.Append(0x61D4C0);
                        NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, (ushort)(Rand(8) + 10)).WriteWord(14, (ushort)(Rand(6) + index));
                        next = next.WriteWord(16, unchecked((ushort)(Word(task, 16) + Rand(1800) - 900)));
                        next = next.WriteWord(18, unchecked((ushort)(Word(task, 22) - 200 - Rand(1000))));
                        next = next.WriteWord(20, unchecked((ushort)(Word(task, 20) + Rand(1800) - 900)));
                        next = next.WriteWord(24, unchecked((ushort)(Rand(200) - 100))).WriteWord(26, unchecked((ushort)(-40 - Rand(70)))).WriteWord(28, unchecked((ushort)(Rand(200) - 100))).WriteWord(32, (ushort)(Rand(1280) + 3840));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase == 14)
                {
                    int first = Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Fira contact requires its actual header contact pointer.");
                    frame = inputs.Execute(frame, new NativeBattleFiraContact(first));
                }
                if (phase == 21 && sequence + 1 <= last)
                {
                    (pool, int child) = pool.Append(0x61CEC0);
                    pool = Put(pool, child, InitializeController(pool.Slots[child], sequence + 1, Header(sequence + 1), inputs.Packets));
                }
                if (sequence == 0 && phase <= 6)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiraStage(unchecked((short)(341 * phase))));
                }
                else if (sequence == last && phase >= 20)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiraStage(unchecked((short)(8868 - 341 * phase))));
                }
                if (phase == 3)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiraSound(Word(task, 32)));
                }
                task = Add(task, 12, 1);
                if (Word(task, 12) > 26)
                {
                    if (sequence == last)
                    {
                        frame = inputs.Execute(frame, new NativeBattleFiraStage(0));
                    }
                    return new(Put(pool, slot, task), 2);
                }
                return new(Put(pool, slot, task), 0);
            }
            if (task.Callback == 0x61DAF0)
            {
                if (frozen)
                {
                    return new(pool, 0);
                }
                int count = (phase >> 1) + 1;
                int span = 400 * count;
                for (int index = 0; index < count; index++)
                {
                    (pool, int child) = pool.Append(0x61DC20);
                    NativeBattleModulePoolTask next = Position(pool.Slots[child], task).WriteWord(12, 0);
                    next = Add(next, 16, Rand(span) - (span >> 1));
                    next = Add(next, 20, Rand(span) - (span >> 1)).WriteWord(24, (ushort)Rand(4096)).WriteWord(28, 0);
                    next = next.WriteWord(30, (ushort)(Rand(80 * count) + Rand(144) + 80)).WriteWord(14, (ushort)(next.ReadWord(24) & 1));
                    pool = Put(pool, child, next);
                }
                task = Add(Add(Add(task, 16, Word(task, 24)), 18, Word(task, 26)), 20, Word(task, 28));
                task = Add(task, 12, 1);
                return new(Put(pool, slot, task), Word(task, 12) >= 10 ? 2 : 0);
            }
            if (task.Callback is 0x61D4C0 or 0x61D5D0 or 0x61D7F0 or 0x61DC20 && Word(task, 14) > 0)
            {
                return new(frozen ? pool : Put(pool, slot, Add(task, 14, -1)), 0);
            }
            if (task.Callback is not (0x61D4C0 or 0x61D5D0 or 0x61D6B0 or 0x61D7F0 or 0x61D9A0 or 0x61DC20))
            {
                throw new NotSupportedException($"Native Fira child{task.Callback:X8} is unsupported.");
            }
            frame = inputs.Execute(frame, new NativeBattleFiraDraw(task));
            if (frozen)
            {
                return new(pool, 0);
            }
            int lifetime;
            switch (task.Callback)
            {
                case 0x61D4C0:
                    task = Add(task, 32, -40);
                    task = Add(Add(Add(task, 16, Word(task, 24)), 18, Word(task, 26)), 20, Word(task, 28));
                    task = Add(task, 24, -(Word(task, 24) >> 4));
                    task = Add(task, 26, Word(task, 26) >> 4);
                    task = Add(task, 28, -(Word(task, 28) >> 4));
                    task = Add(task, 12, -1);
                    return new(Put(pool, slot, task), Word(task, 12) < 0 ? 2 : 0);
                case 0x61D5D0:
                    task = Add(task, 18, Word(task, 22));
                    task = Add(task, 22, Word(task, 22) >> 5);
                    task = Add(task, 28, -(Word(task, 28) >> 6));
                    lifetime = 16;
                    break;
                case 0x61D6B0:
                    lifetime = 22;
                    break;
                case 0x61D7F0:
                    task = Add(task, 24, Word(task, 26));
                    task = Add(task, 26, -(Word(task, 26) >> 4));
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, -(Word(task, 30) / 10));
                    lifetime = 18;
                    break;
                case 0x61D9A0:
                    task = Add(task, 24, Word(task, 26));
                    task = Add(task, 26, -(Word(task, 26) >> 4));
                    task = Add(task, 32, Word(task, 34));
                    task = Add(task, 34, -(Word(task, 34) >> 3));
                    lifetime = 22;
                    break;
                default:
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, -(Word(task, 30) >> 3));
                    lifetime = 16;
                    break;
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= lifetime ? 2 : 0);
        }

        int Rand(int modulus)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value % modulus;
        }

        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[checked(parameters.HeaderIndex7C + sequence)];
    }

    private static NativeBattleModulePoolTask InitializeController(NativeBattleModulePoolTask task, int sequence, NativeBattleActionHeaderSlot header, NativeBattleActionPacketsState packets)
    {
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Fira requires its actual physical first-contact pointer.");
        return task.WriteWord(12, 0).WriteWord(14, unchecked((ushort)sequence)).WriteWord(32, packets.ReadContact(first).Primary.ActorIndex).WriteWord(34, header.ReadByte(0));
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) };
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModulePoolTask source, int offset = 16) => task.WriteWord(16, source.ReadWord(offset)).WriteWord(18, source.ReadWord(offset + 2)).WriteWord(20, source.ReadWord(offset + 4)).WriteWord(22, source.ReadWord(offset + 6));
    private static NativeBattleModulePoolTask WorkingPosition(NativeBattleModulePoolTask task, int offset, NativeBattleFiraPosition source) => task.WriteWord(offset, unchecked((ushort)source.X)).WriteWord(offset + 2, unchecked((ushort)source.Fourth)).WriteWord(offset + 4, unchecked((ushort)source.Z)).WriteWord(offset + 6, unchecked((ushort)source.Y));
}
