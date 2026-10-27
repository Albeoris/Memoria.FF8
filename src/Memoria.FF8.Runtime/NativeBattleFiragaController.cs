namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiragaFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaDraw(NativeBattleModulePoolTask Task) : NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaContact(int Contact) : NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaSfx(int Owner) : NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaStage(short Value, uint ColorFlags) : NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaFlash(byte A, byte B, byte C, byte D) : NativeBattleFiragaOperation;
public sealed record NativeBattleFiragaInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleFiraPosition> ReadPosition, Func<int, NativeBattleModelTransform> ReadTransform, Func<NativeBattleFiragaFrame, NativeBattleFiragaOperation, NativeBattleFiragaFrame> Execute);

/// <summary>61BFE0/61CDA0's current task banks, three physical cache cells and reached61C0A0 lifecycle.</summary>
public static class NativeBattleFiragaController
{
    public const uint MainDescriptor = 0x24BECE0;
    public const uint MainBank = 0x24BECD0;
    public const uint ChildDescriptor = 0x24BECF0;
    public const uint ChildBank = 0x24BED00;

    public static NativeBattleOriginalEffectState Initialize61BFE0(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x61BFE0 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Firaga requires its original61BFE0 entry and actual50AFC0 action parameters.");
        }
        NativeBattleActionHeaderSlot first = packets.Headers[effect.Parameters.HeaderIndex7C];
        if (first.ReadByte(17) >= 3)
        {
            throw new NotSupportedException("Native Firaga's explicit position/alive cache owns at most three physical headers.");
        }
        NativeBattleFiragaState cache = effect.Firaga ?? NativeBattleFiragaState.FromUnknown();
        cache.Validate();
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 16, 1);
        (main, int root) = main.Append(0x61CDA0);
        main = Put(main, root, main.Slots[root].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
        (children, int controller) = children.Append(0x61C0A0);
        children = Put(children, controller, InitializeController(children.Slots[controller], 0, first, packets));
        return effect with { Pools = [main, children], Firaga = cache, ReturnedDescriptor = MainDescriptor };
    }

    public static NativeBattleFiragaFrame Advance61CDA0(NativeBattleFiragaFrame frame, NativeBattleFiragaInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Firaga lost its actual action parameters.");
        Cache().Validate();
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == MainDescriptor);
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == ChildDescriptor);
        bool frozen = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep mainStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x61CDA0)
            {
                throw new NotSupportedException("Native Firaga's main task requires61CDA0.");
            }
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (task.ReadWord(12) & 1) != 0 ? 0x10000 : 0 } };
            NativeBattleEffectTaskStep step = children.Advance(Child);
            children = step.Pool;
            return new(Put(pool, slot, Add(task, 12, 1)), step.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [mainStep.Pool, children], ReturnedDescriptor = mainStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Child(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            short phase = Word(task, 12);
            if (task.Callback == 0x61C0A0)
            {
                if (frozen)
                {
                    return new(pool, 0);
                }
                int sequence = Word(task, 14);
                int last = Header(0).ReadByte(17);
                if (last >= 3 || (uint)sequence >= 3)
                {
                    throw new NotSupportedException("Native Firaga's current header sequence exceeds its three owned position cells.");
                }
                int owner = Word(task, 32);
                if (phase == 0)
                {
                    NativeBattleFiraPosition position = inputs.ReadPosition(owner);
                    frame = frame with { Effect = frame.Effect with { Firaga = Cache().Write(sequence, new(position.X, position.Y, position.Z, 1)) } };
                }
                if (phase is >= 1 and <= 14)
                {
                    int count = phase / 6 + 1;
                    for (int index = 0; index < count; index++)
                    {
                        (pool, int child) = pool.Append(0x61CBB0);
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand(4096) - 2048, Rand(4096) - 2048, Rand(4096) - 2048);
                        var second = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand(4096) - 2048, Rand(4096) - 2048, Rand(4096) - 2048);
                        int x = unchecked(direction.Y * second.Z - direction.Z * second.Y) >> 12;
                        int y = unchecked(direction.Z * second.X - direction.X * second.Z) >> 12;
                        int z = unchecked(direction.X * second.Y - direction.Y * second.X) >> 12;
                        NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, 0).WriteWord(14, unchecked((ushort)sequence)).WriteWord(16, unchecked((ushort)direction.X)).WriteWord(18, unchecked((ushort)direction.Y)).WriteWord(20, unchecked((ushort)direction.Z)).WriteWord(22, 0).WriteWord(24, unchecked((ushort)x)).WriteWord(26, unchecked((ushort)y)).WriteWord(28, unchecked((ushort)z)).WriteWord(30, (ushort)(Rand(30) + 20)).WriteWord(34, unchecked((ushort)(((Rand(2500) + 1500) << 12) / 1900)));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase == 4)
                {
                    (pool, int child) = pool.Append(0x61CB10);
                    pool = Put(pool, child, Position(pool.Slots[child], Cache().Read(sequence)).WriteWord(12, 0).WriteWord(14, unchecked((ushort)sequence)).WriteWord(28, 0).WriteWord(30, 128));
                }
                if (phase == 32)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiragaFlash(0, 1, 2, 255));
                }
                if (phase == 33)
                {
                    NativeBattleModuleAnchor position = Cache().Read(sequence) with { W = 0 };
                    frame = frame with { Effect = frame.Effect with { Firaga = Cache().Write(sequence, position) } };
                    (pool, int firstFlare) = pool.Append(0x61C9C0);
                    NativeBattleModulePoolTask first = Position(pool.Slots[firstFlare], position).WriteWord(12, 0).WriteWord(24, (ushort)Rand(4096)).WriteWord(26, (ushort)(Rand(30) + 60)).WriteWord(28, 0x355).WriteWord(30, 0x355);
                    pool = Put(pool, firstFlare, first);
                    (pool, int secondFlare) = pool.Append(0x61C9C0);
                    NativeBattleModulePoolTask second = Position(pool.Slots[secondFlare], position).WriteWord(12, 0).WriteWord(24, (ushort)Rand(4096)).WriteWord(26, unchecked((ushort)(-40 - Rand(20)))).WriteWord(28, 0x2AA).WriteWord(30, 0x2AA);
                    pool = Put(pool, secondFlare, second);
                    (pool, int ring) = pool.Append(0x61C870);
                    pool = Put(pool, ring, Position(pool.Slots[ring], position).WriteWord(12, 0).WriteWord(28, 0x555).WriteWord(30, 0x555));
                }
                if (phase is >= 36 and <= 48)
                {
                    NativeBattleModelTransform transform = inputs.ReadTransform(owner);
                    int span = Math.Min(1800, unchecked(3400 * transform.Scale38) >> 12);
                    int half = span >> 1;
                    for (int index = 0; index < 2; index++)
                    {
                        (pool, int child) = pool.Append(0x61C6B0);
                        NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, 0).WriteWord(14, (ushort)(Rand(6) + index));
                        next = Position(next, Cache().Read(sequence));
                        next = Add(next, 16, Rand(span) - half);
                        next = Add(next, 18, Rand(span) - half);
                        next = Add(next, 20, Rand(span) - half).WriteWord(22, unchecked((ushort)transform.Angle36));
                        if (Word(next, 18) > transform.Angle36 - 450)
                        {
                            next = Add(next, 18, -350 - Rand(200));
                        }
                        next = next.WriteWord(28, (ushort)(Rand(3072) + 2816));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase == 48)
                {
                    int first = Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Firaga requires its actual header contact pointer.");
                    frame = inputs.Execute(frame, new NativeBattleFiragaContact(first));
                }
                if (phase == 44 && sequence + 1 <= last)
                {
                    (pool, int child) = pool.Append(0x61C0A0);
                    pool = Put(pool, child, InitializeController(pool.Slots[child], sequence + 1, Header(sequence + 1), inputs.Packets));
                }
                if (sequence == 0 && phase <= 8)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiragaStage(unchecked((short)(phase << 8)), 0x101060));
                }
                else if (sequence == last && phase >= 52)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiragaStage(unchecked((short)((60 - phase) << 8)), 0x101080));
                }
                if (phase == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleFiragaSfx(owner));
                }
                task = Add(task, 12, 1);
                if (Word(task, 12) > 60)
                {
                    if (sequence == last)
                    {
                        frame = inputs.Execute(frame, new NativeBattleFiragaStage(0, 0));
                    }
                    return new(Put(pool, slot, task), 2);
                }
                return new(Put(pool, slot, task), 0);
            }
            if (task.Callback == 0x61C6B0 && Word(task, 14) > 0)
            {
                return new(frozen ? pool : Put(pool, slot, Add(task, 14, -1)), 0);
            }
            if (task.Callback is not (0x61C6B0 or 0x61C870 or 0x61C9C0 or 0x61CB10 or 0x61CBB0))
            {
                throw new NotSupportedException($"Native Firaga child{task.Callback:X8} is unsupported.");
            }
            frame = inputs.Execute(frame, new NativeBattleFiragaDraw(task));
            if (frozen)
            {
                return new(pool, 0);
            }
            int lifetime;
            switch (task.Callback)
            {
                case 0x61C6B0:
                    lifetime = 16;
                    break;
                case 0x61C870:
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, -(Word(task, 30) / 12));
                    lifetime = 20;
                    break;
                case 0x61C9C0:
                    task = Add(task, 24, Word(task, 26));
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, -(Word(task, 30) / 4));
                    lifetime = 24;
                    break;
                case 0x61CB10:
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, Word(task, 30) >> 6);
                    task = Add(task, 12, 1);
                    return new(Put(pool, slot, task), Cache().Read(Word(task, 14)).W == 0 ? 2 : 0);
                default:
                    task = Add(task, 22, Word(task, 30));
                    lifetime = 20;
                    break;
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= lifetime ? 2 : 0);
        }

        int Rand(int modulus)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            if (modulus == 0)
            {
                throw new NotSupportedException("Native Firaga scatter reached its actual zero signed divisor.");
            }
            return value % modulus;
        }

        NativeBattleFiragaState Cache() => frame.Effect.Firaga ?? throw new NotSupportedException("Native Firaga requires its retained position/alive cache owner.");
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[checked(parameters.HeaderIndex7C + sequence)];
    }

    private static NativeBattleModulePoolTask InitializeController(NativeBattleModulePoolTask task, int sequence, NativeBattleActionHeaderSlot header, NativeBattleActionPacketsState packets)
    {
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Firaga requires its physical first-contact pointer.");
        return task.WriteWord(12, 0).WriteWord(14, unchecked((ushort)sequence)).WriteWord(32, packets.ReadContact(first).Primary.ActorIndex);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModuleAnchor position) => task.WriteWord(16, unchecked((ushort)position.X)).WriteWord(18, unchecked((ushort)position.Y)).WriteWord(20, unchecked((ushort)position.Z)).WriteWord(22, unchecked((ushort)position.W));
}
