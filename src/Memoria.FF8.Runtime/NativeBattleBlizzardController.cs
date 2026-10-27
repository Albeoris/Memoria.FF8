using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzardFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleBlizzardOperation;
public sealed record NativeBattleBlizzardDraw(NativeBattleModulePoolTask Task) : NativeBattleBlizzardOperation;
public sealed record NativeBattleBlizzardContact(int Contact) : NativeBattleBlizzardOperation;
public sealed record NativeBattleBlizzardSound(int Model) : NativeBattleBlizzardOperation;
public sealed record NativeBattleBlizzardCameraImpulse(short Delta) : NativeBattleBlizzardOperation;
public sealed record NativeBattleBlizzardInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<int, NativeBattleVector> ReadBone240, Func<int, NativeBattleModelTransform> ReadTransform, Func<NativeBattleBlizzardFrame, NativeBattleBlizzardOperation, NativeBattleBlizzardFrame> Execute);

/// <summary>61ABA0/61BF70 and the complete61AC70 physical child scheduler.</summary>
public static class NativeBattleBlizzardController
{
    public static NativeBattleOriginalEffectState Initialize61ABA0(NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets)
    {
        if (effect.Binding.Entry != 0x61ABA0)
        {
            throw new ArgumentException("Native Blizzard requires original61ABA0 entry.", nameof(effect));
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x24BD778, 0x24BD768, 16, 1);
        (main, int mainSlot) = main.Append(0x61BF70);
        main = Put(main, mainSlot, main.Slots[mainSlot].WriteWord(12, 0));
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24BD788, 0x24BD798, 36, 150);
        (children, int childSlot) = children.Append(0x61AC70);
        NativeBattleActionHeaderSlot header = packets.Headers[effect.Parameters!.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native61ABA0 requires its physical first-contact pointer.");
        children = Put(children, childSlot, children.Slots[childSlot].WriteWord(12, 0).WriteWord(14, 0).WriteWord(32, packets.ReadContact(first).Primary.ActorIndex));
        return effect with { Pools = [main, children], ReturnedDescriptor = main.DescriptorAddress };
    }

    public static NativeBattleBlizzardFrame Advance61BF70(NativeBattleBlizzardFrame frame, NativeBattleBlizzardInputs inputs)
    {
        frame.Effect.Validate();
        NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x24BD788);
        NativeBattleEffectTaskPool main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x24BD778);
        bool frozen = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleEffectTaskStep mainStep = main.Advance((pool, slot) =>
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            if (task.Callback != 0x61BF70)
            {
                throw new NotSupportedException("Native Blizzard main pool requires61BF70.");
            }
            frame = frame with { Effect = frame.Effect with { DrawArenaOffset = (task.ReadWord(12) & 1) != 0 ? 0xC000 : 0 } };
            NativeBattleEffectTaskStep step = children.Advance(Callback);
            children = step.Pool;
            return new(Put(pool, slot, Add(task, 12, 1)), step.Survivors == 0 ? 2 : 0);
        });
        return frame with { Effect = frame.Effect with { Pools = [mainStep.Pool, children], ReturnedDescriptor = mainStep.Survivors == 0 ? 0u : mainStep.Pool.DescriptorAddress } };

        NativeBattleEffectTaskCallback Callback(NativeBattleEffectTaskPool pool, int slot)
        {
            NativeBattleModulePoolTask task = pool.Slots[slot];
            short phase = Word(task, 12);
            if (task.Callback == 0x61AC70)
            {
                if (frozen)
                {
                    return new(pool, 0);
                }
                int sequence = Word(task, 14);
                if (phase == 0)
                {
                    task = Position(task, inputs.ReadBone240(Word(task, 32))).WriteWord(22, unchecked((ushort)inputs.ReadTransform(Word(task, 32)).Angle36));
                    (NativeBattleBlizzardFaces faces, NativeCrtRandomState random) = NativeBattleBlizzardFaceMotion.Initialize61B190(frame.Memory.BlizzardFaces, sequence, Resources().PrimaryMesh, frame.Random);
                    frame = frame with { Memory = frame.Memory with { BlizzardFaces = faces }, Random = random };
                }
                if (phase == 1)
                {
                    (pool, int child) = pool.Append(0x61B740);
                    NativeBattleModulePoolTask next = Position(pool.Slots[child], task).WriteWord(12, 0).WriteWord(14, unchecked((ushort)sequence));
                    next = Add(next, 18, -900).WriteWord(24, (ushort)(Rand() % 4096)).WriteWord(26, (ushort)(Rand() % 300 + 600)).WriteWord(28, 1462).WriteWord(32, 80);
                    next = next.WriteWord(22, unchecked((ushort)((short)(Word(next, 18) - Word(task, 22) + 196) / 2)));
                    pool = Put(pool, child, next);
                }
                if (phase is >= 2 and <= 27 && (phase & 1) != 0)
                {
                    (pool, int child) = pool.Append(0x61B660);
                    NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, 0).WriteWord(14, (ushort)(Rand() % 4));
                    next = Add(Position(next, task), 18, -1000);
                    next = Add(next, 16, Rand() % 1000 - 500);
                    next = Add(next, 18, Rand() % 1000 - 500);
                    next = Add(next, 20, Rand() % 1000 - 500).WriteWord(28, (ushort)(Rand() % 1024 + 1024));
                    pool = Put(pool, child, next);
                }
                if (phase is >= 18 and <= 32)
                {
                    for (int index = 0; index < phase / 16 + 1; index++)
                    {
                        (pool, int child) = pool.Append(0x61B5B0);
                        NativeBattleModulePoolTask next = Add(Position(pool.Slots[child], task), 18, -600).WriteWord(12, 0);
                        next = Add(next, 16, Rand() % 1000 - 500);
                        next = Add(next, 18, Rand() % 1000 - 500);
                        next = Add(next, 20, Rand() % 1000 - 500).WriteWord(22, (ushort)(Rand() % 30 + 25)).WriteWord(28, (ushort)(Rand() % 2304 + 1536));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase is >= 37 and <= 39)
                {
                    for (int index = 0; index < 20; index++)
                    {
                        (pool, int child) = pool.Append(0x61B4F0);
                        NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, 0).WriteWord(16, task.ReadWord(16)).WriteWord(18, task.ReadWord(22)).WriteWord(20, task.ReadWord(20));
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand() % 4096 - 2048, 0, Rand() % 4096 - 2048);
                        int radius = Rand() % 350 + 400;
                        next = Add(next, 16, unchecked(direction.X * radius) >> 12);
                        next = Add(next, 20, unchecked(direction.Z * radius) >> 12);
                        int speed = Rand() % 40 + 30;
                        next = next.WriteWord(24, unchecked((ushort)(direction.X * speed >> 12))).WriteWord(28, unchecked((ushort)(direction.Z * speed >> 12))).WriteWord(32, (ushort)(Rand() % 1536 + 2304));
                        pool = Put(pool, child, next);
                    }
                }
                if (phase == 37)
                {
                    (pool, int child) = pool.Append(0x61B3A0);
                    NativeBattleModulePoolTask next = pool.Slots[child].WriteWord(12, 0).WriteWord(16, task.ReadWord(16)).WriteWord(18, task.ReadWord(22)).WriteWord(20, task.ReadWord(20)).WriteWord(28, 637).WriteWord(30, 637);
                    pool = Put(pool, child, next);
                    NativeBattleActionHeaderSlot header = Header(sequence);
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Native61AC70 contact requires its physical header contact pointer.");
                    frame = inputs.Execute(frame, new NativeBattleBlizzardContact(first));
                }
                if (phase == 28 && sequence + 1 <= Header(0).ReadByte(17))
                {
                    (pool, int child) = pool.Append(0x61AC70);
                    int first = Header(sequence + 1).FirstContactIndex ?? throw new NotSupportedException("Native repeated Blizzard requires its next physical contact pointer.");
                    pool = Put(pool, child, pool.Slots[child].WriteWord(12, 0).WriteWord(14, unchecked((ushort)(sequence + 1))).WriteWord(32, inputs.Packets.ReadContact(first).Primary.ActorIndex));
                }
                if (phase == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleBlizzardSound(Word(task, 32)));
                }
                task = Add(task, 12, 1);
                return new(Put(pool, slot, task), Word(task, 12) > 44 ? 2 : 0);
            }
            if (task.Callback == 0x61B660 && Word(task, 14) > 0)
            {
                return new(frozen ? pool : Put(pool, slot, Add(task, 14, -1)), 0);
            }
            frame = inputs.Execute(frame, new NativeBattleBlizzardDraw(task));
            if (frozen)
            {
                return new(pool, 0);
            }
            int lifetime;
            switch (task.Callback)
            {
                case 0x61B740:
                    if (phase < 28)
                    {
                        task = Add(task, 24, Word(task, 26));
                        task = Add(task, 26, -(Word(task, 26) / 10));
                    }
                    else if (phase < 33)
                    {
                        task = Add(task, 18, -Word(task, 32));
                        task = Add(task, 32, -(Word(task, 32) / 10));
                    }
                    else if (phase is >= 35 and < 37)
                    {
                        task = Add(task, 18, -Word(task, 22));
                    }
                    if (phase == 37)
                    {
                        frame = inputs.Execute(frame, new NativeBattleBlizzardCameraImpulse(32));
                    }
                    lifetime = 48;
                    break;
                case 0x61B3A0:
                    task = Add(task, 28, Word(task, 30));
                    task = Add(task, 30, -(Word(task, 30) >> 3));
                    lifetime = 14;
                    break;
                case 0x61B4F0:
                    task = Add(task, 16, Word(task, 24));
                    task = Add(task, 20, Word(task, 28));
                    task = Add(task, 24, -(Word(task, 24) >> 4));
                    task = Add(task, 28, -(Word(task, 28) >> 4));
                    lifetime = 16;
                    break;
                case 0x61B5B0:
                    task = Add(task, 18, Word(task, 22));
                    task = Add(task, 22, Word(task, 22) >> 6);
                    lifetime = 16;
                    break;
                case 0x61B660:
                    lifetime = 8;
                    break;
                default:
                    throw new NotSupportedException($"Native Blizzard child{task.Callback:X8} is unsupported.");
            }
            task = Add(task, 12, 1);
            return new(Put(pool, slot, task), Word(task, 12) >= lifetime ? 2 : 0);
        }

        int Rand()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }
        NativeBattleActionHeaderSlot Header(int sequence) => inputs.Packets.Headers[checked(frame.Effect.Parameters!.HeaderIndex7C + sequence)];
        NativeBattleBlizzardResources Resources() => frame.Effect.Binding.BlizzardResources ?? throw new NotSupportedException("Native Blizzard requires its original executable resources.");
    }

    internal static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    internal static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
    private static NativeBattleEffectTaskPool Put(NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task) => pool.WriteSlot(slot, task);
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector anchor) => task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModulePoolTask source) => task.WriteWord(16, source.ReadWord(16)).WriteWord(18, source.ReadWord(18)).WriteWord(20, source.ReadWord(20)).WriteWord(22, source.ReadWord(22));
}
