namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97StageFadeStep(NativeBattleModulePoolTask Task, short? Depth, bool ClearRgb, bool ReadyToComplete);

public static class NativeBattleChoco97StageFade
{
    public static NativeBattleChoco97StageFadeStep Advance(NativeBattleModulePoolTask task)
    {
        if (task.PayloadLength != 0x70 - 12 || task.Callback is not (0x731630 or 0x731A50 or 0x731B40 or 0x7292D0 or 0x7296D0 or 0x7297C0 or 0x716A60 or 0x716B50))
        {
            throw new InvalidDataException("Native Choco stage fade requires its actual auxiliary callback and task.");
        }
        short? depth = null;
        bool clearRgb = false;
        bool fadeOut = task.Callback is 0x731630 or 0x7292D0 or 0x716B50;
        bool firstRise = task.Callback is 0x731A50 or 0x7296D0 or 0x716A60;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                ushort start = task.Callback switch
                {
                    0x731630 or 0x7292D0 => 0xC00,
                    0x731A50 or 0x7296D0 or 0x716A60 => 0,
                    0x716B50 => 0x600,
                    _ => 0x600
                };
                task = task.WriteWord(0x1C, start).WriteByte(0x29, 1);
                if (!fadeOut)
                {
                    depth = unchecked((short)start);
                    clearRgb = true;
                }
                break;
            case 1:
                int delta = task.Callback == 0x7292D0 ? -0xC0 : fadeOut ? -0x80 : firstRise ? 0x80 : 0x100;
                short value = unchecked((short)(task.ReadWord(0x1C) + delta));
                int limit = firstRise ? 0x600 : 0xC00;
                bool complete = fadeOut ? value <= 0 : value >= limit;
                if (complete)
                {
                    value = fadeOut ? (short)0 : (short)limit;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                depth = value;
                task = task.WriteWord(0x1C, unchecked((ushort)value));
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native Choco stage fade phase leaves its three original callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(task, depth, clearRgb, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }
}
