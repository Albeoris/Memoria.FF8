namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStageFadeWrite(short Fade, bool ClearRgb);
public sealed record NativeBattleStageFadeStep(NativeBattleModulePoolTask Task, NativeBattleStageFadeWrite? Write);

/// <summary>Identical87DE50/88D290/883430 and882FC0/892ED0/888480 WORD fades; wrappers own counters and parent completion.</summary>
public static class NativeBattleStageFadeTask
{
    public static NativeBattleStageFadeStep Advance(NativeBattleModulePoolTask task, bool fadeIn)
    {
        return Advance(task, fadeIn, 256, 1024);
    }

    public static NativeBattleStageFadeStep Advance(NativeBattleModulePoolTask task, bool fadeIn, short step, short maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        NativeBattleStageFadeWrite? write = null;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x1C, fadeIn ? (ushort)0 : unchecked((ushort)maximum));
                Next();
                if (fadeIn)
                {
                    write = new(0, true);
                }
                break;
            case 1:
                short fade = unchecked((short)(task.ReadWord(0x1C) + (fadeIn ? step : -step)));
                if (fadeIn ? fade >= maximum : fade <= 0)
                {
                    fade = fadeIn ? maximum : (short)0;
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
