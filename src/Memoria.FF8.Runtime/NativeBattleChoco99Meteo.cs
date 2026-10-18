namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco99MeteoEmitterStep(NativeBattleEffectHierarchy Hierarchy, NativeBattleModulePoolTask Task, NativeCrtRandomState Random, bool ReadyToComplete);
public sealed record NativeBattleChoco99CameraYStep(NativeBattleModulePoolTask Task, short? Y, bool ReadyToComplete);

public static class NativeBattleChoco99Meteo
{
    public static NativeBattleChoco97SpriteStep Advance720500(NativeBattleModulePoolTask task, NativeBattleChoco99MeteoResources resources, uint sourceIo134, Func<NativeBattleModulePoolTask, NativeBattleChoco97AppendProgram, NativeBattleModulePoolTask> appendProgram, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        Require(task, 0x720500, 0xB0);
        resources.Validate();
        switch (Stage(task))
        {
            case 0:
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteDword(0x4C, resources.Meteo.Address).WriteWord(0x52, 14).WriteWord(0x1E, unchecked((ushort)-8192)).WriteWord(0xAC, 16);
                task = Next(task);
                break;
            case 1:
                task = task.WriteWord(0xAC, unchecked((ushort)(task.ReadWord(0xAC) - 1)));
                if (Word(task, 0xAC) <= 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)));
                }
                break;
            case 2:
                (task, bool complete) = AdvanceFrame(task);
                if (complete)
                {
                    task = appendProgram(task, new(0x720610, sourceIo134, 0x2C, 4, 0));
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException("Native Choco99 meteo phase leaves its four original phases.");
        }
        NativeBattleModulePacketBatch? packets = Draw(task, resources.Meteo, 6144, view, projection, cameraRoll);
        task = AddCounter(task);
        return new(task, packets, Complete(task));
    }

    public static NativeBattleChoco97SpriteStep Advance7208D0(NativeBattleModulePoolTask task, NativeBattleChoco99MeteoResources resources, Func<short, bool> modeAtLeast, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        Require(task, 0x7208D0, 0xB0);
        resources.Validate();
        switch (Stage(task))
        {
            case 0:
                task = Next(task.WriteDword(0x4C, resources.Secondary[0].Address).WriteWord(0x52, 6).WriteWord(0x1E, unchecked((ushort)-8192)));
                break;
            case 1:
                (task, bool complete) = AdvanceFrame(task);
                if (complete || modeAtLeast(8))
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco99 secondary sprite phase leaves its three original phases.");
        }
        NativeBattleModulePacketBatch? packets = Draw(task, resources.Secondary[0], 5632, view, projection, cameraRoll);
        task = AddCounter(task);
        return new(task, packets, Complete(task));
    }

    public static NativeBattleChoco99MeteoEmitterStep Advance720790(NativeBattleEffectHierarchy hierarchy, uint address, NativeBattleChoco99MeteoResources resources, Func<short, bool> modeAtLeast, NativeCrtRandomState random, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        resources.Validate();
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        Require(task, 0x720790, 0xB0);
        switch (Stage(task))
        {
            case 0:
                int pool = Enumerable.Range(0, hierarchy.Pools.Length).Single(index => hierarchy.Pools[index].DescriptorAddress == NativeBattleChoco99Particles.AuxiliaryDescriptor);
                for (int index = 0; index < 2; index++)
                {
                    NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, 0x7208D0, 0xB0, address, selectActors);
                    if (appended.Address == 0)
                    {
                        throw new NotSupportedException("Native Choco99 meteo emitter requires both mandatory auxiliary allocations.");
                    }
                    hierarchy = appended.Hierarchy;
                    NativeBattleModulePoolTask child = hierarchy.Read(appended.Address).WriteDword(0x4C, resources.Secondary[Draw() % 3].Address).WriteWord(0x52, 6);
                    short yaw = (short)(Draw() & 4095);
                    short pitch = (short)(Draw() % 640 + 128);
                    NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
                    basis = NativeBattleCuragaMatrices.RotateX8DD7E0(basis, pitch);
                    NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, -9216, 0));
                    child = child.WriteWord(0x1C, unchecked((ushort)position.X)).WriteWord(0x1E, unchecked((ushort)position.Y)).WriteWord(0x20, unchecked((ushort)position.Z));
                    hierarchy = hierarchy.Write(appended.Address, child);
                }
                task = hierarchy.Read(address);
                if (modeAtLeast(8))
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                break;
            case 1:
                break;
            default:
                throw new NotSupportedException("Native Choco99 meteo emitter leaves its two original phases.");
        }
        task = AddCounter(task);
        hierarchy = hierarchy.Write(address, task);
        return new(hierarchy, task, random, Complete(task));

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleChoco99CameraYStep Advance71F880(NativeBattleModulePoolTask task, NativeBattleChoco99CameraYProgram program)
    {
        return AdvanceCameraYProfiled(task, program, 0x71F880);
    }

    internal static NativeBattleChoco99CameraYStep AdvanceCameraYProfiled(NativeBattleModulePoolTask task, NativeBattleChoco99CameraYProgram program, uint callback)
    {
        Require(task, callback, 0x40);
        if (program.Address == 0 || program.Values.IsDefaultOrEmpty || task.ReadDword(0x34) != program.Address)
        {
            throw new InvalidDataException("Native Choco view translation requires its retained bounded WORD program.");
        }
        short? y = null;
        switch (Stage(task))
        {
            case 0:
                task = task.WriteWord(0x38, unchecked((ushort)(task.ReadWord(0x38) - 1)));
                if (Word(task, 0x38) <= 0)
                {
                    task = Next(task.WriteWord(0x38, 0));
                }
                break;
            case 1:
                int index = Word(task, 0x38);
                if ((uint)index >= program.Values.Length)
                {
                    throw new InvalidDataException("Native Choco view translation index leaves its actual WORD program.");
                }
                short value = program.Values[index];
                if (value == 127)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                else
                {
                    y = value;
                }
                task = task.WriteWord(0x38, unchecked((ushort)(task.ReadWord(0x38) + 1)));
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco view translation leaves its three original phases.");
        }
        task = AddCounter(task);
        return new(task, y, Complete(task));
    }

    private static NativeBattleModulePacketBatch? Draw(NativeBattleModulePoolTask task, NativeBattleChoco97SpriteResources sprite, int scale, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        if (task.ReadDword(0x4C) != sprite.Address)
        {
            throw new NotSupportedException("Native Choco99 meteo sprite lost its retained projected resource pointer.");
        }
        NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
        ushort frame = task.ReadWord(0x50);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(frame),
            Phase = frame,
            Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0),
            Scale = scale,
            DepthOffset = Word(task, 0x54),
            SpriteFlags = 0
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
    }
    private static (NativeBattleModulePoolTask Task, bool Complete) AdvanceFrame(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
        if (Word(task, 0x50) <= Word(task, 0x52))
        {
            return (task, false);
        }
        return (task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)), true);
    }
    private static void Require(NativeBattleModulePoolTask task, uint callback, int size)
    {
        if (task.Callback != callback || task.Payload.IsDefault || task.Payload.Length != size - 12)
        {
            throw new InvalidDataException("Native Choco99 auxiliary requires its complete original task and callback.");
        }
    }
    private static int Stage(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask AddCounter(NativeBattleModulePoolTask task) => task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
    private static bool Complete(NativeBattleModulePoolTask task) => (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
