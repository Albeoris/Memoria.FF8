namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBoneScaleTaskStep(NativeBattleModulePoolTask Task, int Result);

public static class NativeBattleBoneScaleTask65F2B0
{
    public static NativeBattleBoneScaleTaskStep Advance(NativeBattleModulePoolTask task)
    {
        short delay = unchecked((short)(Signed(task, 16) - 1));
        task = task.WriteWord(16, unchecked((ushort)delay));
        if (delay <= 0)
        {
            short z = unchecked((short)(Signed(task, 18) + Signed(task, 20)));
            short xy = unchecked((short)(Signed(task, 22) + Signed(task, 24)));
            z = Math.Min(z, (short)4096);
            xy = Math.Min(xy, (short)4096);
            task = task.WriteWord(16, 0).WriteWord(18, unchecked((ushort)z)).WriteWord(22, unchecked((ushort)xy));
            if (xy >= 4096 && z >= 4096)
            {
                return new(task, 2);
            }
        }
        return new(task, 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
