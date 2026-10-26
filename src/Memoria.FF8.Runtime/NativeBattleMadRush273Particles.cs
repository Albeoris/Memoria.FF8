namespace Memoria.FF8.Runtime;

public static class NativeBattleMadRush273Particles
{
    public static int BirthCount(int frame)
    {
        int relative = unchecked(frame - 20);
        if ((uint)relative >= 10)
        {
            return 0;
        }
        return (NativeBattleCameraMath.Sine((relative << 11) / 10) >> 11) + 1;
    }

    public static (NativeCrtRandomState Random, NativeBattleModulePoolTask Task) Initialize(NativeCrtRandomState random, NativeBattleModulePoolTask task, NativeBattleVector anchor)
    {
        random = random.Draw(out ushort scale);
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort z);
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort velocity);
        task = task.WriteWord(12, 0).WriteWord(14, (ushort)((scale & 0x400) + 0x400)).WriteWord(16, unchecked((ushort)(anchor.X + (x & 0x1FF) - 256))).WriteWord(18, unchecked((ushort)(anchor.Y - (y & 0x3FF)))).WriteWord(20, unchecked((ushort)(anchor.Z + (z & 0x1FF) - 256))).WriteWord(22, unchecked((ushort)(-16 - (velocity & 63))));
        return (random, task);
    }

    public static (NativeBattleModulePoolTask Task, int Result) Advance(NativeBattleModulePoolTask task)
    {
        short phase = unchecked((short)(task.ReadWord(12) + 1));
        task = task.WriteWord(12, unchecked((ushort)phase));
        if (phase >= 13)
        {
            return (task, 2);
        }
        task = task.WriteWord(18, unchecked((ushort)(task.ReadWord(18) + task.ReadWord(22))));
        return (task, 0);
    }
}
