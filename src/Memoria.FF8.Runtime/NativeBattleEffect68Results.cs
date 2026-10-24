using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    private static NativeBattleEffect68Frame Results(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteDword(0x60, task.ReadDword(0x5C)).WriteDword(0x5C, inputs.ReadMappedHeld(frame));
        frame = Write(frame, address, task);
        switch (Phase(task, 5))
        {
            case 0:
                frame = PutWord(frame, 0x26005B2, 0);
                NativeBattleEffect68ResultStep result = inputs.BuildResultsB67EF0(frame, task.ReadByte(0x2D));
                frame = result.Frame;
                task = Next(Read(frame, address).WriteWord(0x6A, result.Count));
                break;
            case 1:
                uint page = inputs.ReadPageB68370(frame, task.ReadByte(0x6C));
                frame = inputs.Execute(frame, new NativeBattleEffect68WindowText(1, page));
                frame = inputs.Execute(frame, new NativeBattleEffect68WindowFlags(1, 0));
                frame = inputs.Execute(frame, new NativeBattleEffect68WindowRect(1, new(16, 180, 296, 28)));
                frame = inputs.Execute(frame, new NativeBattleEffect68WindowValue(1, 0));
                frame = inputs.Execute(frame, new NativeBattleEffect68OpenWindow(1));
                task = Next(Read(frame, address).WriteWord(0x66, 45));
                break;
            case 2:
                if ((task.ReadDword(0x5C) & 0x40) != 0)
                {
                    task = Next(task);
                    break;
                }
                if ((task.ReadDword(0x60) & 0x20) != 0 || (task.ReadDword(0x5C) & 0x20) == 0)
                {
                    task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                    if (S(task, 0x66) > 0)
                    {
                        break;
                    }
                }
                task = task.WriteWord(0x6C, unchecked((ushort)(task.ReadWord(0x6C) + 1)));
                task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + (S(task, 0x6C) < S(task, 0x6A) ? -1 : 1))));
                break;
            case 3:
                frame = inputs.Execute(frame, new NativeBattleEffect68CloseWindow(1));
                frame = inputs.Execute(frame, new NativeBattleEffect68WindowText(1, 0));
                frame = PutWord(frame, 0x26005B2, 1);
                task = Done(Read(frame, address));
                break;
        }
        return Write(frame, address, task);
    }
}
