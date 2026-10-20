namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38ParticleBirth(NativeBattleModulePoolTask Task, NativeCrtRandomState Random);
public sealed record NativeBattleQuake38DebrisStep(NativeBattleModulePoolTask Task, NativeBattleQuake38State State);

public static class NativeBattleQuake38ParticleMotion
{
    public static NativeBattleQuake38ParticleBirth Initialize8DAD50(NativeBattleModulePoolTask task, NativeCrtRandomState random)
    {
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort z);
        task = Word(task, 86, -128 - (y & 0xFF));
        task = Word(task, 140, (x & 0x7F) - 64);
        task = Word(task, 142, (z & 0x7F) - 64);
        return new(Next(task), random);
    }

    public static NativeBattleQuake38DebrisStep Advance8DADA0(NativeBattleModulePoolTask task, NativeBattleQuake38State state)
    {
        task = Word(task, 68, (Read(task, 68) + Read(task, 140)) & 0xFFF);
        task = Word(task, 70, (Read(task, 70) + Read(task, 142)) & 0xFFF);
        task = Word(task, 86, Read(task, 86) + 64);
        task = Word(task, 30, Read(task, 30) + Read(task, 86));
        if (Read(task, 30) >= 0)
        {
            state = state.SetWord(0x279299C, unchecked((short)(state.Word(0x279299C) - 1)));
            task = Next(task.WriteByte(38, (byte)(task.ReadByte(38) | 1)));
        }
        return new(task, state);
    }

    public static NativeBattleQuake38ParticleBirth Initialize8DB160(NativeBattleModulePoolTask task, NativeCrtRandomState random)
    {
        random = random.Draw(out ushort gravity);
        task = task.WriteDword(76, 0x16434A0).WriteWord(82, 15);
        return new(Next(Word(task, 166, -8 - (gravity & 0xF))), random);
    }

    public static NativeBattleQuake38ParticleBirth Initialize8DB4D0(NativeBattleModulePoolTask task, NativeCrtRandomState random)
    {
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort gravity);
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort z);
        task = task.WriteDword(76, 0x164335C).WriteWord(82, 11);
        task = Word(task, 162, -100 - (y & 0x7F));
        task = Word(task, 166, (gravity & 7) + 6);
        task = Word(task, 160, (x & 0x7F) - 64);
        task = Word(task, 164, (z & 0x7F) - 64);
        return new(Next(task), random);
    }

    public static NativeBattleQuake38ParticleBirth Initialize8DB690(NativeBattleModulePoolTask task, NativeCrtRandomState random)
    {
        random = random.Draw(out ushort y);
        random = random.Draw(out ushort x);
        random = random.Draw(out ushort z);
        task = task.WriteDword(76, 0x1643218).WriteWord(82, 11);
        task = Word(task, 162, -48 - (y & 0xFF));
        task = Word(task, 160, (x & 0x7F) - 64);
        task = Word(task, 164, (z & 0x7F) - 64);
        return new(Next(task), random);
    }

    public static NativeBattleModulePoolTask RockVelocity8DB030(NativeBattleModulePoolTask task)
    {
        short velocity = unchecked((short)(Read(task, 162) + Read(task, 166)));
        return Word(task, 162, velocity - velocity / 8);
    }

    public static NativeBattleModulePoolTask DustVelocity8DB410(NativeBattleModulePoolTask task) => Word(task, 162, Read(task, 162) + Read(task, 166));

    public static NativeBattleModulePoolTask ChipPosition8DB6F0(NativeBattleModulePoolTask task)
    {
        int velocity = Read(task, 162);
        velocity -= velocity / 16;
        task = Word(task, 162, velocity);
        task = Word(task, 28, Read(task, 28) + Read(task, 160));
        task = Word(task, 30, Read(task, 30) + velocity);
        return Word(task, 32, Read(task, 32) + Read(task, 164));
    }

    private static short Read(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Word(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
}
