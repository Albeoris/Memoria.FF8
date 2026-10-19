using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTonberry90Creature
{
    public static NativeBattleTonberry90TaskStep AdvanceScreenSprite(NativeBattleTonberry90Frame frame, uint address, Func<NativeBattleTonberry90Frame, uint, NativeBattleTonberry90Frame> draw)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x766FB0;
        if (task.Callback is not (0x766FB0 or 0x7667B0))
        {
            throw new InvalidDataException("Native Tonberry screen sprite requires its actual selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                uint camera = State(frame).CameraAddress1547510 ?? throw new NotSupportedException("Native Tonberry screen sprite requires its current camera programme owner.");
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x1C, first ? unchecked((ushort)-56) : (ushort)64).WriteWord(0x1E, first ? unchecked((ushort)-88) : unchecked((ushort)-28)).WriteWord(0x20, State(frame).ReadWord(camera + 0xE0)).WriteDword(0x4C, first ? 0x1547258u : 0x1547174u).WriteWord(0x56, 0xFF00).WriteWord(0x52, first ? (ushort)0 : (ushort)5));
                break;
            case 1:
                if (Word(task, 0x24) >= (first ? 0 : 20))
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)));
                    task = first ? task.WriteWord(0x6E, 24) : task.WriteWord(0x74, 4);
                }
                break;
            case 2:
                if (first)
                {
                    task = task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + Word(task, 0x6E) / 16)));
                    uint director = State(frame).DirectorAddress1547168 ?? throw new NotSupportedException("Native Tonberry screen sprite requires its current director owner.");
                    if (unchecked((short)State(frame).ReadWord(director + 0x40)) >= 7)
                    {
                        task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                }
                else
                {
                    AdvanceFrame();
                    if (Word(task, 0x50) > 4)
                    {
                        task = task.WriteWord(0x74, unchecked((ushort)(task.ReadWord(0x74) - 1))).WriteWord(0x50, 3);
                        if (Word(task, 0x74) <= 0)
                        {
                            task = Next(task);
                        }
                    }
                }
                break;
            case 3:
                if (!first && AdvanceFrame())
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                break;
            case 4 when !first:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry screen sprite signed phase {unchecked((sbyte)task.ReadByte(0x29))} has no selected state.");
        }
        frame = draw(Write(frame, address, task), address);
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        bool AdvanceFrame()
        {
            task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
            if (Word(task, 0x50) <= Word(task, 0x52))
            {
                return false;
            }
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
            return true;
        }
    }
}

public static class NativeBattleTonberry90ScreenSprites
{
    public static NativeBattleActionMatrix Matrix766830(NativeBattleModulePoolTask task, short scale = 256)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        if (scale != 4096)
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
        }
        return matrix with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
    }

    public static NativeBattleGfGeometryBatch Draw766830(NativeBattleModulePoolTask task, ImmutableArray<byte> resource, NativeBattleCameraProjection projection, short cameraRoll, short scale = 256)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        return NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, task.ReadWord(0x50), Matrix766830(task, scale), projection, cameraRoll, 17, 2, unchecked((short)task.ReadWord(0x56)));
    }
}
