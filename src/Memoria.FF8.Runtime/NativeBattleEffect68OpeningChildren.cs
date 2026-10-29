using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    private static NativeBattleEffect68Frame Pair(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(task, 5))
        {
            case 0:
                task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x20, 0x480).WriteDword(0x38, 4096).WriteWord(0x48, 0xA00));
                break;
            case 1:
                if (S(task, 0x24) >= 3)
                {
                    task = Next(task);
                }
                break;
            case 2:
                uint index = checked((uint)S(task, 0x24));
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteWord(0x1C, unchecked((ushort)(Table(resources, 0x1595440 + index * 4) - 60))).WriteWord(0x1E, unchecked((ushort)(Table(resources, 0x1595442 + index * 4) + 60))).WriteDword(0x30, unchecked((uint)(int)Table(resources, 0x1595488 + index * 4))).WriteDword(0x34, unchecked((uint)(int)Table(resources, 0x159548A + index * 4))).WriteWord(0x5C, unchecked((ushort)Table(resources, 0x15953F8 + index * 2))).WriteWord(0x5E, unchecked((ushort)Table(resources, 0x159541C + index * 2)));
                if (S(task, 0x24) >= 17)
                {
                    task = Next(task);
                }
                break;
            case 3:
                task = Angle(task, 0x68, 512);
                task = task.WriteWord(0x5E, unchecked((ushort)(unchecked((short)(NativeBattleCameraMath.Sine(task.ReadWord(0x68)) + 4096)) / 4)));
                if (W(frame, Highlight) == 0)
                {
                    task = Done(task);
                }
                break;
        }
        frame = Write(frame, address, task.WriteWord(0x50, task.ReadWord(0x5C)).WriteDword(0x4C, 0x1592794));
        frame = DrawMesh(frame, address, resources, inputs);
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x50, task.ReadWord(0x5E)).WriteDword(0x4C, 0x1592810));
        return DrawMesh(frame, address, resources, inputs);
    }

    private static NativeBattleEffect68Frame MorphTask(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(task, 7))
        {
            case 0:
                task = Next(Scale(task, 0x3FF8).WriteWord(0x1C, 0).WriteWord(0x1E, 0).WriteWord(0x20, 0x480).WriteWord(0x44, 0x5DC).WriteWord(0x46, 0x2A0).WriteWord(0x48, 0xF808).WriteDword(0x4C, 0x1591C44).WriteDword(0x54, 0x159390C).WriteDword(0x58, 0x1593A94).WriteWord(0x50, 4096));
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 341)));
                if (S(task, 0x50) < 0)
                {
                    task = task.WriteWord(0x50, 0);
                }
                task = Angle(Angle(Angle(task, 0x44, -125), 0x46, -56), 0x48, 170);
                int scale = unchecked((int)task.ReadDword(0x30) - 1194);
                if (scale <= 2048)
                {
                    scale = 2048;
                    task = Next(task.WriteWord(0x66, 12));
                }
                task = Scale(task, scale);
                break;
            case 2:
                task = Angle(task, 0x48, 170).WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (S(task, 0x66) <= 0)
                {
                    task = Next(task);
                }
                break;
            case 3:
                task = Angle(task, 0x48, 170).WriteWord(0x6A, unchecked((ushort)(task.ReadWord(0x6A) + 512)));
                if (S(task, 0x6A) >= 4096)
                {
                    task = Next(task.WriteWord(0x6A, 0).WriteDword(0x4C, 0x159202C).WriteDword(0x54, 0x1593C1C).WriteDword(0x58, 0x1594124));
                }
                break;
            case 4:
                task = task.WriteWord(0x6A, unchecked((ushort)(task.ReadWord(0x6A) + 1024)));
                if (S(task, 0x6A) >= 4096)
                {
                    task = Next(task.WriteWord(0x6A, 4096));
                }
                break;
            case 5:
                if ((task.ReadByte(0x24) & 7) < 4)
                {
                    task = Angle(task, 0x48, 42);
                }
                if (W(frame, Highlight) == 0)
                {
                    task = Done(task);
                }
                break;
        }
        frame = Write(frame, address, task);
        frame = MorphVertices(frame, task.ReadDword(0x54), task.ReadDword(0x58), task.ReadDword(0x4C), S(task, 0x6A), resources, inputs);
        return DrawMesh(frame, address, resources, inputs);
    }

    private static NativeBattleEffect68Frame SpriteTask(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(task, 4))
        {
            case 0:
                task = Next(task.WriteWord(0x1C, 400).WriteWord(0x1E, 192).WriteWord(0x20, 2304).WriteDword(0x4C, 0x159462C).WriteWord(0x52, 31));
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (S(task, 0x50) > S(task, 0x52))
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x50, task.ReadWord(0x52));
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)).WriteDword(0x4C, 0x1594950).WriteWord(0x50, 0).WriteWord(0x52, 3));
                }
                break;
            case 2:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (S(task, 0x50) > S(task, 0x52))
                {
                    task = task.WriteWord(0x50, 0);
                }
                if (W(frame, Highlight) == 0)
                {
                    task = Done(task);
                }
                break;
        }
        frame = Write(frame, address, task);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleCuragaMatrices.Identity8DD770(), 1024, 1024, 1024) with { X = S(task, 0x1C), Y = S(task, 0x1E), Z = S(task, 0x20) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, 0));
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(0x4C))).Write(4, BitConverter.GetBytes(task.ReadWord(0x50))).Write(0x24, BitConverter.GetBytes((ushort)0));
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), 0, 17, 2, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(68, task) with { ResourceGeneration = task.ReadDword(0x4C) });
        frame = frame with { Memory = drawn.Memory };
        frame = PublishContext(frame, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect68DrawBatch(drawn.Batch, Cursor));
        return Release(frame, 180);
    }
}
