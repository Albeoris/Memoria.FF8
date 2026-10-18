using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99ParticleEmission(NativeBattleEffectHierarchy Hierarchy, uint Address, NativeCrtRandomState Random);
public sealed record NativeBattleChoco99ParticleStep(NativeBattleModulePoolTask Task, ImmutableArray<NativeBattleModulePacketBatch> Packets, bool ReadyToComplete);

public static class NativeBattleChoco99Particles
{
    public const uint AuxiliaryDescriptor = 0x2565C20;
    public const uint TrailCallback = 0x720B90;
    public const uint RadialCallback = 0x720EA0;

    public static NativeBattleChoco99ParticleEmission EmitTrail7209B0(NativeBattleEffectHierarchy hierarchy, uint parentAddress, ushort mode, NativeCrtRandomState random, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchyAppend appended = Append(hierarchy, parentAddress, TrailCallback, selectActors);
        hierarchy = appended.Hierarchy;
        NativeBattleModulePoolTask parent = hierarchy.Read(parentAddress);
        NativeBattleModulePoolTask child = hierarchy.Read(appended.Address).WriteWord(0xAE, mode);
        int[] delta = Enumerable.Range(0, 3).Select(axis => (int)unchecked((short)((Word(parent, 0x540 + axis * 2) - Word(parent, 0x1C + axis * 2)) / 4))).ToArray();
        ushort[] position = Enumerable.Range(0, 4).Select(axis => parent.ReadWord(0x1C + axis * 2)).ToArray();
        for (int index = 0; index < 4; index++)
        {
            for (int axis = 0; axis < 4; axis++)
            {
                child = child.WriteWord(0x1C + axis * 2, position[axis]);
            }
            short yaw = (short)(Draw() & 4095);
            short pitch = (short)(Draw() & 4095);
            NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
            basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, pitch);
            short radius = (short)(Draw() % 256);
            NativeBattleVector kick = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, radius));
            int[] kickComponents = [kick.X, kick.Y, kick.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                child = child.WriteWord(0x1C + axis * 2, unchecked((ushort)(child.ReadWord(0x1C + axis * 2) + kickComponents[axis])));
            }
            child = CopyRaw(child, 0x1C, 0x6C + index * 8);
            for (int axis = 0; axis < 3; axis++)
            {
                child = child.WriteWord(0x8C + index * 8 + axis * 2, unchecked((ushort)((Draw() & 1023) - 512)));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                position[axis] = unchecked((ushort)(position[axis] + delta[axis]));
            }
        }
        return new(hierarchy.Write(appended.Address, child), appended.Address, random);

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleChoco99ParticleEmission EmitRadial720D70(NativeBattleEffectHierarchy hierarchy, uint parentAddress, ushort mode, NativeCrtRandomState random, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchyAppend appended = Append(hierarchy, parentAddress, RadialCallback, selectActors);
        hierarchy = appended.Hierarchy;
        NativeBattleModulePoolTask parent = hierarchy.Read(parentAddress);
        NativeBattleModulePoolTask child = hierarchy.Read(appended.Address).WriteWord(0xAE, mode);
        int initial = Draw() & 4095;
        for (int index = 0; index < 4; index++)
        {
            int angle = (initial + index * 1024) & 4095;
            int sine = NativeBattleCameraMath.Sine(angle);
            int cosine = NativeBattleCameraMath.Cosine(angle);
            int radius = (Draw() & 127) + 128;
            int destination = 0x6C + index * 8;
            child = child.WriteDword(destination, parent.ReadDword(0x1C)).WriteDword(destination + 4, parent.ReadDword(0x20));
            child = child.WriteWord(destination, unchecked((ushort)(Word(child, destination) + unchecked(sine * radius) / 4096))).WriteWord(destination + 4, unchecked((ushort)(Word(child, destination + 4) + unchecked(cosine * radius) / 4096)));
            int horizontal = (Draw() & 2047) + 1024;
            int up = (Draw() & 1023) + 512;
            int velocity = 0x8C + index * 8;
            child = child.WriteWord(velocity, unchecked((ushort)(unchecked(sine * horizontal) / 4096))).WriteWord(velocity + 2, (ushort)up).WriteWord(velocity + 4, unchecked((ushort)(unchecked(cosine * horizontal) / 4096)));
        }
        return new(hierarchy.Write(appended.Address, child), appended.Address, random);

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleChoco99ParticleStep Advance(NativeBattleModulePoolTask task, NativeBattleChoco99ParticleResources resources, Func<short, bool> modeAtLeast, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        resources.Validate();
        if (task.Payload.IsDefault || task.Payload.Length != 0xB0 - 12 || task.Callback is not (TrailCallback or RadialCallback))
        {
            throw new InvalidDataException("Native Choco99 particle requires its actual B0-byte four-point task.");
        }
        NativeBattleChoco97SpriteResources sprite = task.Callback == TrailCallback ? resources.Trail : resources.Radial;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, sprite.Address).WriteWord(0x52, 11);
                Next();
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                bool complete = Word(task, 0x50) > Word(task, 0x52);
                if (complete)
                {
                    task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
                }
                if (complete || modeAtLeast(Word(task, 0xAE)))
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    Next();
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco99 four-point particle phase leaves its three original phases.");
        }
        ImmutableArray<NativeBattleModulePacketBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleModulePacketBatch>();
        for (int index = 0; index < 4; index++)
        {
            int point = 0x6C + index * 8;
            int velocity = 0x8C + index * 8;
            for (int axis = 0; axis < 3; axis++)
            {
                short current = Word(task, velocity + axis * 2);
                short damped = unchecked((short)(current - current / 8));
                task = task.WriteWord(velocity + axis * 2, unchecked((ushort)damped));
                task = task.WriteWord(point + axis * 2, unchecked((ushort)(Word(task, point + axis * 2) + damped / 16)));
            }
            task = CopyRaw(task, point, 0x1C);
            NativeBattleModulePacketBatch? packet = Draw(task, sprite, view, projection, cameraRoll);
            if (packet is not null)
            {
                packets.Add(packet);
            }
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, packets.ToImmutable(), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }

    private static NativeBattleModulePacketBatch? Draw(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        if (task.ReadDword(0x4C) != sprite.Address)
        {
            throw new NotSupportedException("Native Choco99 four-point particle lost its actual retained projected resource pointer.");
        }
        NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
        ushort frame = task.ReadWord(0x50);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(frame),
            Phase = frame,
            Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0),
            Scale = task.Callback == TrailCallback ? 4096 : 8192,
            DepthOffset = Word(task, 0x54),
            SpriteFlags = 0
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
    }

    private static NativeBattleEffectHierarchyAppend Append(NativeBattleEffectHierarchy hierarchy, uint parentAddress, uint callback, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        int pool = Enumerable.Range(0, hierarchy.Pools.Length).Single(index => hierarchy.Pools[index].DescriptorAddress == AuxiliaryDescriptor);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, 0xB0, parentAddress, selectActors);
        if (appended.Address == 0)
        {
            throw new NotSupportedException("Native Choco99 four-point emitter requires its mandatory original auxiliary allocation.");
        }
        return appended;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask CopyRaw(NativeBattleModulePoolTask task, int source, int destination) => task.WriteDword(destination, task.ReadDword(source)).WriteDword(destination + 4, task.ReadDword(source + 4));
}
