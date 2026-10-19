namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112Sprite(uint ResourceAddress, ushort Phase, NativeBattleVector Position, ushort Scale, int DepthOffset);
public sealed record NativeBattleDuel112ParticleStep(NativeBattleModulePoolTask Task, int Result);

public static class NativeBattleDuel112Particles
{
    public static NativeBattleDuel112ParticleStep Advance6CD5C0(NativeBattleModulePoolTask task, Action<NativeBattleDuel112Sprite> draw)
    {
        draw(new(task.ReadDword(24), task.ReadWord(12), Point(task, 16), task.ReadWord(22), -(Signed(task, 22) >> 2)));
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(task, Signed(task, 12) < Signed(task, 14) ? 0 : 2);
    }

    public static NativeBattleDuel112ParticleStep Advance6CD750(NativeBattleModulePoolTask task, Action<NativeBattleDuel112Sprite> draw)
    {
        draw(new(task.ReadDword(16), task.ReadWord(12), Point(task, 20), task.ReadWord(26), -(Signed(task, 34) >> 2)));
        task = Add(task, 20, Signed(task, 28));
        task = Add(task, 22, Signed(task, 30));
        task = Add(task, 24, Signed(task, 32));
        task = Add(task, 12, 1);
        return new(task, Signed(task, 12) < Signed(task, 14) ? 0 : 2);
    }

    public static NativeBattleDuel112ParticleStep Advance6CD640(NativeBattleModulePoolTask task, Action<NativeBattleModulePoolTask> draw)
    {
        draw(task);
        task = Add(task, 32, Signed(task, 40));
        task = Add(task, 36, Signed(task, 44));
        task = Add(task, 26, 10);
        task = Add(task, 34, Signed(task, 42));
        task = Add(task, 16, Signed(task, 24));
        task = Add(task, 18, Signed(task, 26));
        task = Add(task, 20, Signed(task, 28));
        if (Signed(task, 18) > 0)
        {
            short velocity = Signed(task, 26);
            task = task.WriteWord(18, 0).WriteWord(26, unchecked((ushort)((velocity >> 2) - velocity)));
        }
        task = Add(task, 12, 1);
        return new(task, Signed(task, 12) < 30 ? 0 : 2);
    }

    private static NativeBattleVector Point(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
}
