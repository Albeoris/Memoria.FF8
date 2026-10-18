using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzaraFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleBlizzaraOperation;
public sealed record NativeBattleBlizzaraMainDraw(NativeBattleModulePoolTask Task, int Sequence, int Phase, short YScale, uint Flags) : NativeBattleBlizzaraOperation;
public sealed record NativeBattleBlizzaraDraw(NativeBattleModulePoolTask Task) : NativeBattleBlizzaraOperation;
public sealed record NativeBattleBlizzaraContact(int Contact) : NativeBattleBlizzaraOperation;
public sealed record NativeBattleBlizzaraSound(NativeBattleModuleAnchor Position) : NativeBattleBlizzaraOperation;
public sealed record NativeBattleBlizzaraInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleVector> ReadBone241, Func<int, NativeBattleModelTransform> ReadTransform, Func<NativeBattleBlizzaraFrame, NativeBattleBlizzaraOperation, NativeBattleBlizzaraFrame> Execute);

/// <summary>Blizzara's retained root, five ordered arena banks and task-owned child lifecycles.</summary>
public static class NativeBattleBlizzaraController
{
    public const uint MainDescriptor = 0x2543EB8;
    public const uint MainBank = 0x2543EA0;
    public const uint PrimaryDescriptor = 0x2543EC8;
    public const uint SecondaryDescriptor = 0x2543ED8;
    public const uint TertiaryDescriptor = 0x2543EE8;
    public const uint SmokeDescriptor = 0x2543EF8;
    public const uint TrailDescriptor = 0x2543F08;
    public const uint PrimaryBank = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint ActorBank = 0x1D972C0;

    public static NativeBattleOriginalEffectState Initialize6DA310(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x6DA300 || effect.Parameters is null)
        {
            throw new InvalidDataException("Native Blizzara requires its original6DA300 entry and actual action parameters.");
        }
        int first = effect.Parameters.HeaderIndex7C;
        int last = packets.Headers[first].ReadByte(17);
        if (last >= 3 || first + last >= NativeBattleActionPacketsState.HeaderCapacity)
        {
            throw new NotSupportedException("Native Blizzara owns exactly three header-indexed mesh pairs.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainBank, 20, 1);
        (main, int root) = main.Append(0x6DA3A0);
        main = Put(main, root, Clear(main.Slots[root], 8));
        return effect with { Pools = [main], ReturnedDescriptor = MainDescriptor };
    }

    public static NativeBattleBlizzaraFrame Advance6DA3A0(NativeBattleBlizzaraFrame frame, NativeBattleBlizzaraInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new InvalidDataException("Native Blizzara lost its action parameters.");
        NativeBattleBlizzaraResources resources = frame.Effect.Binding.BlizzaraResources ?? throw new NotSupportedException("Native Blizzara requires its original mesh and sprite resources.");
        NativeBattleEffectTaskPool main = Pool(MainDescriptor)!;
        NativeBattleEffectTaskPool? primary = Pool(PrimaryDescriptor);
        NativeBattleEffectTaskPool? secondary = Pool(SecondaryDescriptor);
        NativeBattleEffectTaskPool? tertiary = Pool(TertiaryDescriptor);
        NativeBattleEffectTaskPool? smoke = Pool(SmokeDescriptor);
        NativeBattleEffectTaskPool? trail = Pool(TrailDescriptor);
        NativeBattleEffectTaskStep rootStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DA3A0);
            bool alternate = Dword(task, 16) == 0;
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = alternate ? 0x296D4 : 0x196D4 } };
            task = WriteDword(task, 16, alternate ? 1u : 0u);
            if (Word(task, 12) == 1 && task.ReadByte(15) == 0)
            {
                primary = NativeBattleEffectTaskPool.Initialize(PrimaryDescriptor, PrimaryBank, 0x53CC, 3);
                secondary = NativeBattleEffectTaskPool.Initialize(SecondaryDescriptor, PrimaryBank + 0xFB64, 0x28, 12);
                tertiary = NativeBattleEffectTaskPool.Initialize(TertiaryDescriptor, PrimaryBank + 0xFD44, 0x20, 1);
                smoke = NativeBattleEffectTaskPool.Initialize(SmokeDescriptor, PrimaryBank + 0xFD64, 0x24, 32);
                trail = NativeBattleEffectTaskPool.Initialize(TrailDescriptor, PrimaryBank + 0x101E4, 0x28, 64);
                task = WriteByte(task, 15, 1);
            }
            if (Word(task, 12) == 1 && task.ReadByte(14) <= Header(0).ReadByte(17))
            {
                int sequence = task.ReadByte(14);
                NativeBattleActionHeaderSlot header = Header(sequence);
                int contact = header.FirstContactIndex ?? throw new NotSupportedException("Native Blizzara requires an actual header contact owner.");
                int actor = inputs.Packets.ReadContact(contact).Primary.ActorIndex;
                uint actorAddress = ActorAddress(actor);
                bool duplicate = primary!.Slots.Any(current => (current.Flags & 1) != 0 && Dword(current, 16) == actorAddress && Word(current, 12) <= 60);
                if (duplicate)
                {
                    task = task.WriteWord(12, 0);
                }
                else
                {
                    (primary, int child) = primary.Append(0x6DAA30);
                    NativeBattleModulePoolTask next = Clear(primary.Slots[child], 0x53C0).WriteWord(14, (ushort)sequence).WriteWord(38, 0x7FFF);
                    next = WriteDword(next, 16, actorAddress);
                    next = WriteDword(next, 20, PrimaryBank + (uint)NativeBattleBlizzaraFaces.PrimaryMeshOffset(sequence));
                    next = WriteDword(next, 24, PrimaryBank + (uint)NativeBattleBlizzaraFaces.PartnerMeshOffset(sequence));
                    NativeBattleBlizzaraFaceInitialization initialized = NativeBattleBlizzaraFaces.Initialize(frame.Memory, next, sequence, resources);
                    frame = frame with { Memory = initialized.Memory };
                    primary = Put(primary, child, initialized.Task);
                    task = WriteByte(task, 14, (byte)(sequence + 1));
                }
            }
            if (task.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep first = primary!.Advance(Primary);
                primary = first.Pool;
                NativeBattleEffectTaskStep second = secondary!.Advance(Sky);
                secondary = second.Pool;
                NativeBattleEffectTaskStep third = tertiary!.Advance(Ring);
                tertiary = third.Pool;
                NativeBattleEffectTaskStep fourth = smoke!.Advance(Smoke);
                smoke = fourth.Pool;
                NativeBattleEffectTaskStep fifth = trail!.Advance(Trail);
                trail = fifth.Pool;
                if (first.Survivors + second.Survivors + third.Survivors + fourth.Survivors + fifth.Survivors == 0)
                {
                    return new(Put(pool, slot, task), 2);
                }
            }
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 20)
            {
                task = task.WriteWord(12, 0);
            }
            return new(Put(pool, slot, task), 0);
        });
        ImmutableArray<NativeBattleEffectTaskPool> pools = primary is null ? [rootStep.Pool] : [rootStep.Pool, primary, secondary!, tertiary!, smoke!, trail!];
        return frame with { Effect = frame.Effect with { Pools = pools, ReturnedDescriptor = rootStep.Survivors == 0 ? 0u : MainDescriptor } };

        NativeBattleEffectTaskCallback Primary(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DAA30);
            int phase = Word(task, 12);
            int sequence = task.ReadWord(14);
            int actor = ActorIndex(Dword(task, 16));
            short ground = inputs.ReadTransform(actor).Angle36;
            if (phase == 0)
            {
                task = Position(task, 28, inputs.ReadBone241(actor)).WriteWord(30, unchecked((ushort)ground));
            }
            if (phase is 0 or 4 or 8 or 12)
            {
                if (Enumerable.Range(0, 4).All(index => task.ReadByte(40 + index) != 0))
                {
                    throw new NotSupportedException("Native Blizzara's four unique ordinal owners are already occupied.");
                }
                (secondary, int child) = secondary!.Append(0x6DBB20);
                NativeBattleModulePoolTask next = Clear(secondary.Slots[child], 28);
                next = WriteDword(next, 16, PrimaryBank + (uint)slot * 0x53CC);
                next = Position(next, 20, inputs.ReadBone241(actor));
                int ordinal;
                do
                {
                    ordinal = Rand() * 4 >> 15;
                }
                while (task.ReadByte(40 + ordinal) != 0);
                task = WriteByte(task, 40 + ordinal, 1);
                next = Add(next, 22, ((ordinal << 10) - 1535) >> 1);
                next = next.WriteWord(30, unchecked((ushort)((ordinal & 1) != 0 ? 31 : -31))).WriteWord(34, unchecked((ushort)(NativeBattleCameraMath.Sine(ordinal * 512 + 256) * 1024 / 11000))).WriteWord(26, 6);
                secondary = Put(secondary, child, next);
            }
            if (phase == 6)
            {
                Emit(new NativeBattleBlizzaraSound(Anchor(task, 28)));
            }
            if (phase == 20)
            {
                (tertiary, int child) = tertiary!.Append(0x6DBD20);
                NativeBattleModulePoolTask next = Position(Clear(tertiary.Slots[child], 20), 16, inputs.ReadBone241(actor)).WriteWord(18, unchecked((ushort)ground)).WriteWord(26, 100).WriteWord(28, 195).WriteWord(30, unchecked((ushort)-16));
                tertiary = Put(tertiary, child, next);
            }
            int yScale = 4096;
            if (phase is >= 20 and <= 30)
            {
                int growth = Math.Min(phase - 20, 3);
                yScale = growth >= 3 ? 4096 : (growth << 12) / 3;
                Emit(new NativeBattleBlizzaraMainDraw(task, sequence, phase, (short)yScale, 0x6600C));
                task = task.WriteWord(38, unchecked((ushort)(ground - 3400 * yScale / 10)));
            }
            if (phase is >= 30 and <= 70)
            {
                NativeBattleBlizzaraFaceStep step = NativeBattleBlizzaraFaces.Advance(frame.Memory, task, sequence, phase, frame.Random);
                frame = frame with { Memory = step.Memory, Random = step.Random };
                task = step.Task;
                Emit(new NativeBattleBlizzaraMainDraw(task, sequence, phase, 4096, 0x660C0));
            }
            if (phase is >= 20 and <= 30)
            {
                SpawnSmoke(task, ground, yScale);
            }
            if (phase is >= 20 and <= 40)
            {
                SpawnTrail(task);
            }
            if (phase == 60)
            {
                Emit(new NativeBattleBlizzaraContact(Header(sequence).FirstContactIndex ?? throw new NotSupportedException("Native Blizzara lost its header contact owner.")));
            }
            return new(Put(pool, slot, phase >= 70 ? task : Add(task, 12, 1)), phase >= 70 ? 2 : 0);
        }

        void SpawnSmoke(NativeBattleModulePoolTask parent, short ground, int yScale)
        {
            int seed = Rand();
            for (int index = 0; index < 2; index++)
            {
                if (smoke!.Slots.All(task => (task.Flags & 1) != 0))
                {
                    continue;
                }
                (smoke, int child) = smoke.Append(0x6DBEA0);
                int a = Rand();
                int b = Rand();
                int angle = seed + ((a + b - 32768) >> 8);
                seed += 2048;
                NativeBattleModulePoolTask next = Clear(smoke.Slots[child], 24);
                next = WriteDword(next, 16, Dword(parent, 16));
                next = next.WriteWord(20, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) >> 3))).WriteWord(24, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) >> 3))).WriteWord(26, (ushort)(4096 - (Rand() >> 4)));
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(next, 20), 0, Word(next, 24));
                next = next.WriteWord(28, unchecked((ushort)((short)normalized.X >> 6))).WriteWord(32, unchecked((ushort)((short)normalized.Z >> 6)));
                next = Add(Add(next, 20, Word(parent, 28)), 24, Word(parent, 32));
                short height = unchecked((short)((Rand() >> 3) - (yScale >> 1) - 1024));
                next = next.WriteWord(22, unchecked((ushort)(Math.Min((int)height, -1000) + ground))).WriteWord(30, 32).WriteWord(34, 0xFFFF);
                smoke = Put(smoke, child, next);
            }
        }

        void SpawnTrail(NativeBattleModulePoolTask parent)
        {
            int seed = Rand();
            for (int index = 0; index < 4; index++)
            {
                if (trail!.Slots.All(task => (task.Flags & 1) != 0))
                {
                    continue;
                }
                (trail, int child) = trail.Append(0x6DBFF0);
                int a = Rand();
                int b = Rand();
                int angle = seed + ((a + b - 32768) >> 8);
                seed += 1024;
                NativeBattleModulePoolTask next = Clear(trail.Slots[child], 28);
                next = next.WriteWord(16, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) >> 4))).WriteWord(20, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) >> 4))).WriteWord(22, (ushort)(4096 - (Rand() >> 4)));
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(next, 16), 0, Word(next, 20));
                int vx = unchecked((short)normalized.X) / 24;
                int vz = unchecked((short)normalized.Z) / 24;
                next = next.WriteWord(24, unchecked((ushort)vx)).WriteWord(28, unchecked((ushort)vz)).WriteWord(32, unchecked((ushort)(-vx / 14))).WriteWord(36, unchecked((ushort)(-vz / 14)));
                next = Add(Add(next, 16, Word(parent, 28)), 20, Word(parent, 32)).WriteWord(18, parent.ReadWord(30));
                trail = Put(trail, child, next);
            }
        }

        NativeBattleEffectTaskCallback Sky(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DBB20);
            int phase = Word(task, 12);
            int fade = phase < 6 ? 4096 - NativeBattleCameraMath.Sine((phase << 10) / 6) : phase < 18 ? 0 : NativeBattleCameraMath.Sine(((phase - 18) << 10) / 12);
            if (fade < 4096)
            {
                Emit(new NativeBattleBlizzaraDraw(task));
            }
            task = Add(Add(task, 28, Word(task, 30)), 12, 1);
            if (Word(task, 12) >= 30)
            {
                return new(Put(pool, slot, task), 2);
            }
            NativeBattleModulePoolTask parent = primary!.Slots[PrimarySlot(Dword(task, 16))];
            if (task.ReadByte(38) == 0 && Word(parent, 38) <= Word(task, 22))
            {
                task = WriteByte(task.WriteWord(26, 240).WriteWord(36, unchecked((ushort)-128)), 38, 1);
            }
            task = Add(task, 36, 8);
            task = task.WriteWord(36, unchecked((ushort)Math.Min(0, (int)Word(task, 36))));
            task = Add(task, 26, Word(task, 36));
            task = task.WriteWord(26, unchecked((ushort)Math.Max(5, (int)Word(task, 26))));
            task = Add(task, 34, Word(task, 26));
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskCallback Ring(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DBD20);
            int phase = Word(task, 12);
            int fade = phase < 6 ? 4096 - NativeBattleCameraMath.Sine((phase << 10) / 6) : phase < 14 ? 0 : NativeBattleCameraMath.Sine(((phase - 14) << 10) / 6);
            if (fade < 4096)
            {
                Emit(new NativeBattleBlizzaraDraw(task));
            }
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 20)
            {
                return new(Put(pool, slot, task), 2);
            }
            task = Add(task, 28, Word(task, 30));
            task = task.WriteWord(28, unchecked((ushort)Math.Max(30, (int)Word(task, 28))));
            return new(Put(pool, slot, Add(task, 26, Word(task, 28))), 0);
        }

        NativeBattleEffectTaskCallback Smoke(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DBEA0);
            Emit(new NativeBattleBlizzaraDraw(task));
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 16)
            {
                return new(Put(pool, slot, task), 2);
            }
            short ground = inputs.ReadTransform(ActorIndex(Dword(task, 16))).Angle36;
            if (Word(task, 22) < ground - 4 * Word(task, 30))
            {
                task = Add(task, 30, 4);
            }
            else if (Word(task, 30) != 0)
            {
                if (Word(task, 34) == -1)
                {
                    task = task.WriteWord(34, unchecked((ushort)(Word(task, 30) / 6)));
                }
                var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word(task, 28), 0, Word(task, 32));
                task = task.WriteWord(30, 0);
                task = Add(task, 28, unchecked((short)normalized.X) * Word(task, 34) >> 10);
                task = Add(task, 32, unchecked((short)normalized.Z) * Word(task, 34) >> 10);
            }
            task = Add(Add(Add(task, 22, Word(task, 30)), 20, Word(task, 28) >> 2), 24, Word(task, 32) >> 2);
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskCallback Trail(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            RequireCallback(task, 0x6DBFF0);
            Emit(new NativeBattleBlizzaraDraw(task));
            task = Add(task, 12, 1);
            if (Word(task, 12) >= 16)
            {
                return new(Put(pool, slot, task), 2);
            }
            task = Add(Add(task, 28, Word(task, 36)), 24, Word(task, 32));
            task = Add(Add(task, 20, Word(task, 28)), 16, Word(task, 24));
            return new(Put(pool, slot, task), 0);
        }

        NativeBattleEffectTaskPool? Pool(uint descriptor) => frame.Effect.Pools.SingleOrDefault(pool => pool.DescriptorAddress == descriptor);
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[parameters.HeaderIndex7C + sequence];
        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
        void Emit(NativeBattleBlizzaraOperation operation) => frame = inputs.Execute(frame, operation);
    }

    public static int PrimarySlot(uint pointer)
    {
        uint delta = unchecked(pointer - PrimaryBank);
        if (delta % 0x53CC != 0 || delta / 0x53CC >= 3)
        {
            throw new NotSupportedException("Native Blizzara's parent pointer is outside its three primary task owners.");
        }
        return (int)(delta / 0x53CC);
    }

    public static int ActorIndex(uint pointer)
    {
        uint delta = unchecked(pointer - ActorBank);
        if (delta % 156 != 0 || delta / 156 >= 7)
        {
            throw new NotSupportedException("Native Blizzara's graphics row pointer has no actual actor owner.");
        }
        return (int)(delta / 156);
    }

    private static uint ActorAddress(int actor) => actor is >= 0 and < 7 ? ActorBank + (uint)actor * 156 : throw new NotSupportedException("Native Blizzara requires an actual actor graphics row.");
    private static void RequireCallback(NativeBattleModulePoolTask task, uint callback)
    {
        if (task.Callback != callback)
        {
            throw new NotSupportedException($"Native Blizzara callback{task.Callback:X8} is unsupported in this task bank.");
        }
    }
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Dword(NativeBattleModulePoolTask task, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(offset - 12, 4));
    private static NativeBattleModulePoolTask WriteDword(NativeBattleModulePoolTask task, int offset, uint value)
    {
        byte[] bytes = task.Payload.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset - 12, 4), value);
        return task with { Payload = bytes.ToImmutableArray() };
    }
    private static NativeBattleModulePoolTask WriteByte(NativeBattleModulePoolTask task, int offset, byte value) => task with { Payload = task.Payload.SetItem(offset - 12, value) };
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector position) => task.WriteWord(offset, unchecked((ushort)position.X)).WriteWord(offset + 2, unchecked((ushort)position.Y)).WriteWord(offset + 4, unchecked((ushort)position.Z));
    private static NativeBattleModuleAnchor Anchor(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4), Word(task, offset + 6));
    private static NativeBattleModulePoolTask Clear(NativeBattleModulePoolTask task, int length)
    {
        byte[] bytes = task.Payload.ToArray();
        bytes.AsSpan(0, length).Clear();
        return task with { Payload = bytes.ToImmutableArray() };
    }
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) };
}

