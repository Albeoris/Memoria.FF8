namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97StageFadeStep(NativeBattleModulePoolTask Task, short? Depth, bool ClearRgb, bool ReadyToComplete);

public static class NativeBattleChoco97StageFade
{
    public static NativeBattleChoco97StageFadeStep Advance(NativeBattleModulePoolTask task)
    {
        if (task.Payload.IsDefault || task.Payload.Length != 0x70 - 12 || task.Callback is not (0x731630 or 0x731A50 or 0x731B40))
        {
            throw new InvalidDataException("Native Choco stage fade requires its actual auxiliary callback and task.");
        }
        short? depth = null;
        bool clearRgb = false;
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                ushort start = task.Callback switch
                {
                    0x731630 => 0xC00,
                    0x731A50 => 0,
                    _ => 0x600
                };
                task = task.WriteWord(0x1C, start).WriteByte(0x29, 1);
                if (task.Callback != 0x731630)
                {
                    depth = unchecked((short)start);
                    clearRgb = true;
                }
                break;
            case 1:
                int delta = task.Callback == 0x731630 ? -0x80 : task.Callback == 0x731A50 ? 0x80 : 0x100;
                short value = unchecked((short)(task.ReadWord(0x1C) + delta));
                int limit = task.Callback == 0x731A50 ? 0x600 : 0xC00;
                bool complete = task.Callback == 0x731630 ? value <= 0 : value >= limit;
                if (complete)
                {
                    value = task.Callback == 0x731630 ? (short)0 : (short)limit;
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
