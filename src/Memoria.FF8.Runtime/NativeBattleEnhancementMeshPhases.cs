namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnhancementMeshPhase(NativeBattleModulePoolTask Task, bool PublishTargetFlash);

/// <summary>The normalized first-mesh phases and second-mesh phases0..4; each family retains its distinct terminal producer.</summary>
public static class NativeBattleEnhancementMeshPhases
{
    public static NativeBattleModulePoolTask First(NativeBattleModulePoolTask task)
    {
        switch (Phase(task))
        {
            case 0:
                task = Add(task, 0x50, -512).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 2));
                if (Word(task, 0x50) <= 0)
                {
                    task = Increment(task.WriteWord(0x50, 0));
                }
                break;
            case 1:
                task = AddScale(Add(task, 0x50, 512), -512, -512, -512);
                if (Word(task, 0x50) >= 4096)
                {
                    task = Increment(Scale(task.WriteWord(0x50, 4096).WriteWord(0x26, (ushort)((task.ReadWord(0x26) & ~2) | 5)), 16));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native enhancement first-mesh signed phase exceeds its three callbacks.");
        }
        return task;
    }

    public static NativeBattleEnhancementMeshPhase SecondPrefix(NativeBattleModulePoolTask task, bool triple, Func<uint, NativeBattleModulePoolTask> readTask)
    {
        int modeOffset = triple ? 0x6A : 0x5E;
        int pulseOffset = triple ? 0x6C : 0x60;
        bool publish = false;
        switch (Phase(task))
        {
            case 0:
                task = AddScale(Add(task, 0x50, -1024), 512, 0, 512);
                if (Word(task, 0x50) <= 0)
                {
                    task = Increment(task.WriteWord(0x50, 0).WriteDword(0x30, 2048).WriteDword(0x38, 2048));
                }
                break;
            case 1:
                task = AddScale(task, 0, 1024, 0);
                if (unchecked((int)task.ReadDword(0x34)) >= 4096)
                {
                    task = Increment(task);
                }
                break;
            case 2:
                task = AddScale(task, 0, 1024, 0);
                if (unchecked((int)task.ReadDword(0x34)) >= 6144)
                {
                    task = Increment(task.WriteDword(0x34, 6144));
                }
                break;
            case 3:
                task = AddScale(task, 1024, -1024, 1024);
                if (unchecked((int)task.ReadDword(0x34)) <= 4096)
                {
                    task = Scale(task, 4096);
                    if (task.ReadWord(modeOffset) == 0)
                    {
                        task = task.WriteWord(pulseOffset, 1);
                    }
                    else
                    {
                        publish = true;
                    }
                    task = Increment(task);
                }
                break;
            case 4:
                if (readTask(task.ReadDword(0x14)).ReadWord(0x58) == 2)
                {
                    task = Increment(task.WriteWord(pulseOffset, 3));
                }
                break;
            default:
                throw new InvalidDataException("Native shared enhancement second-mesh prefix admits only phases0..4.");
        }
        return new(task, publish);
    }

    public static NativeBattleModulePoolTask Overlay(NativeBattleModulePoolTask task)
    {
        switch (Phase(task))
        {
            case 0:
                return Increment(task.WriteWord(0x32, unchecked((ushort)-144)));
            case 1:
                task = Add(task, 0x32, 40);
                task = Add(task, 0x1E, Word(task, 0x32));
                if (Word(task, 0x1E) >= Word(task, 0x3A))
                {
                    task = Increment(task.WriteWord(0x1E, task.ReadWord(0x3A)));
                }
                return task;
            case 2:
                return Word(task, 0x24) >= 15 ? Increment(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))) : task;
            default:
                //The original switch falls through and still draws for every other signed BYTE phase.
                return task;
        }
    }

    private static int Phase(NativeBattleModulePoolTask task) => unchecked((sbyte)task.ReadByte(0x29));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    private static NativeBattleModulePoolTask Increment(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static NativeBattleModulePoolTask Scale(NativeBattleModulePoolTask task, int scale) => task.WriteDword(0x30, unchecked((uint)scale)).WriteDword(0x34, unchecked((uint)scale)).WriteDword(0x38, unchecked((uint)scale));
    private static NativeBattleModulePoolTask AddScale(NativeBattleModulePoolTask task, int x, int y, int z) => task.WriteDword(0x30, unchecked(task.ReadDword(0x30) + (uint)x)).WriteDword(0x34, unchecked(task.ReadDword(0x34) + (uint)y)).WriteDword(0x38, unchecked(task.ReadDword(0x38) + (uint)z));
}
