namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDarkMist13ParticleStep(NativeBattleModulePoolTask Task, NativeCrtRandomState Random);

public static partial class NativeBattleDarkMist13
{
    public static NativeBattleDarkMist13ParticleStep AdvanceParticle(NativeBattleModulePoolTask task, NativeBattleVector anchor, NativeBattleDarkMist13Resources resources, NativeCrtRandomState random)
    {
        int phase = task.ReadByte(0x29);
        int counter = Word(task, 0x24);
        if (phase == 0)
        {
            switch (task.Callback)
            {
                case 0x8BEC80:
                case 0x8BEF30:
                    bool colored = task.Callback == 0x8BEF30;
                    task = task.WriteDword(0x4C, colored ? 0x1630E94u : 0x16309FCu).WriteWord(0x52, colored ? (ushort)1 : (ushort)23).WriteWord(0x54, 0);
                    NativeBattleVector point = Vector(task, 0x1C);
                    short x = anchor.X;
                    short y = anchor.Y;
                    short z = anchor.Z;
                    if (colored)
                    {
                        task = task.WriteDword(0x48, 0x808080);
                        y = unchecked((short)(y - 192 - (Roll() & 255)));
                        x = unchecked((short)(x + 256 - (Roll() & 511)));
                        y = unchecked((short)(y + 256 - (Roll() & 511)));
                    }
                    short vx = (short)(unchecked((short)(x - point.X)) / 12);
                    short vy = colored ? (short)(unchecked((short)(y - point.Y)) / 12) : (short)(unchecked((short)(y - (Roll() & 255) - point.Y - 192)) / 12);
                    short vz = colored ? (short)(unchecked((short)(z - (Roll() & 511) - point.Z + 256)) / 12) : (short)(unchecked((short)(z - point.Z)) / 12);
                    task = Store(task, 0x58, new(vx, vy, vz)).WriteWord(0x64, unchecked((ushort)point.Y)).WriteWord(0x66, 0);
                    if (Word(task, 0x62) != 0)
                    {
                        task = Store(task, 0x1C, new(unchecked((short)(point.X + vx / 2)), unchecked((short)(point.Y + vy / 2)), unchecked((short)(point.Z + vz / 2))));
                        task = task.WriteWord(0x64, task.ReadWord(0x1E)).WriteWord(0x66, colored ? (ushort)0x15 : (ushort)0x155);
                    }
                    task = Phase(task);
                    break;
                case 0x8BF300:
                case 0x8BF4F0:
                    bool large = task.Callback == 0x8BF300;
                    int span = large ? 768 : 512;
                    short px = unchecked((short)(anchor.X + Roll() % span - span / 2));
                    short pz = unchecked((short)(anchor.Z + Roll() % span - span / 2));
                    short py = unchecked((short)(anchor.Y + Roll() % span - (large ? 896 : 768)));
                    task = Phase(Store(task, 0x1C, new(px, py, pz)).WriteDword(0x4C, large ? 0x1630C78u : 0x1630D84u).WriteWord(0x52, large ? (ushort)8 : (ushort)9).WriteWord(0x54, 0));
                    break;
                case 0x8BF700:
                    int variant = Word(task, 0x62);
                    int life = 20 - variant;
                    NativeBattleVector velocity = resources.StreamVectors[variant];
                    task = Phase(Store(Store(task, 0x1C, anchor), 0x58, new((short)(velocity.X / life), (short)(velocity.Y / life), (short)(velocity.Z / life))).WriteDword(0x4C, 0x16309FC).WriteWord(0x60, (ushort)life).WriteWord(0x50, (ushort)(23 - life)).WriteWord(0x52, 23).WriteWord(0x54, 0xFE00));
                    break;
                default:
                    throw new NotSupportedException("Native Dark Mist selected an unrelated particle initializer.");
            }
        }
        else if (phase == 1)
        {
            switch (task.Callback)
            {
                case 0x8BEC80:
                case 0x8BEF30:
                    bool colored = task.Callback == 0x8BEF30;
                    short baseY = unchecked((short)(Word(task, 0x64) + Word(task, 0x5A)));
                    int sine = NativeBattleCameraMath.Sine(Word(task, 0x66));
                    task = task.WriteWord(0x1C, unchecked((ushort)(Word(task, 0x1C) + Word(task, 0x58)))).WriteWord(0x20, unchecked((ushort)(Word(task, 0x20) + Word(task, 0x5C)))).WriteWord(0x1E, unchecked((ushort)(baseY + sine / 16))).WriteWord(0x64, unchecked((ushort)baseY)).WriteWord(0x54, unchecked((ushort)(Word(task, 0x54) - (colored ? 48 : 32)))).WriteWord(0x66, (ushort)((Word(task, 0x66) + 0x155) & 4095));
                    task = NextFrame(task, colored);
                    if (colored && counter >= 4)
                    {
                        byte red = unchecked((byte)(task.ReadByte(0x48) - 16));
                        byte green = unchecked((byte)(task.ReadByte(0x49) - 16));
                        byte blue = unchecked((byte)(task.ReadByte(0x4A) - 16));
                        if (red == 0)
                        {
                            red = green = blue = 16;
                        }
                        task = task.WriteByte(0x48, red).WriteByte(0x49, green).WriteByte(0x4A, blue);
                    }
                    if (counter >= 12)
                    {
                        task = Done(task);
                    }
                    break;
                case 0x8BF300:
                case 0x8BF4F0:
                    task = NextFrame(task, false);
                    if ((task.ReadByte(0x26) & 4) != 0)
                    {
                        task = Done(task);
                    }
                    break;
                case 0x8BF700:
                    for (int axis = 0; axis < 3; axis++)
                    {
                        task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)(Word(task, 0x1C + axis * 2) + Word(task, 0x58 + axis * 2))));
                    }
                    task = NextFrame(task, false);
                    if (counter > Word(task, 0x60))
                    {
                        task = Done(task);
                    }
                    break;
            }
        }
        return new(task, random);

        int Roll()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleModulePoolTask NextFrame(NativeBattleModulePoolTask task, bool loop)
    {
        int frame = Word(task, 0x50) + 1;
        if (frame > Word(task, 0x52))
        {
            frame = loop ? 0 : Word(task, 0x52);
            if (!loop)
            {
                task = task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 4));
            }
        }
        return task.WriteWord(0x50, unchecked((ushort)frame));
    }
}
