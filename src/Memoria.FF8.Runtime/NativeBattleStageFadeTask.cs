namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageFadeWrite(short Fade, bool ClearRgb);
public sealed record NativeBattleStageFadeStep(NativeBattleModulePoolTask Task, NativeBattleStageFadeWrite? Write);

/// <summary>Identical87DE50/88D290/883430 and882FC0/892ED0/888480 WORD fades; wrappers own counters and parent completion.</summary>
public static class NativeBattleStageFadeTask
{
    public static NativeBattleStageFadeStep Advance(NativeBattleModulePoolTask task, bool fadeIn)
    {
        NativeBattleStageFadeWrite? write = null;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x1C, fadeIn ? (ushort)0 : (ushort)1024);
                Next();
                if (fadeIn)
                {
                    write = new(0, true);
                }
                break;
            case 1:
                short fade = unchecked((short)(task.ReadWord(0x1C) + (fadeIn ? 256 : -256)));
                if (fadeIn ? fade >= 1024 : fade <= 0)
                {
                    fade = fadeIn ? (short)1024 : (short)0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                task = task.WriteWord(0x1C, unchecked((ushort)fade));
                write = new(fade, false);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native shared stage fade signed phase exceeds its three original callbacks.");
        }
        return new(task, write);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    }
}
