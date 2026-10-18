namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22PrimaryInputs(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> UpdateTargetAndBounds, Func<NativeBattleModulePoolTask, short> CenterY, Func<NativeBattleModulePoolTask, short> HalfHeight, Func<NativeBattleModulePoolTask, uint, short?, NativeBattleModulePoolTask> Append, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> Contact, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> Sound);
public sealed record NativeBattleBio22PrimaryStep(NativeBattleModulePoolTask Task, uint Result);

/// <summary>The primary Bio timeline with immediate hierarchy, contact and sound services.</summary>
public static class NativeBattleBio22Primary
{
    public static NativeBattleBio22PrimaryStep Advance89A8D0(NativeBattleModulePoolTask task, NativeBattleBio22PrimaryInputs inputs)
    {
        task = inputs.UpdateTargetAndBounds(task);
        int counter = S(task, 0x24);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0:
                task = task.WriteDword(0x78, task.ReadDword(0x38)).WriteDword(0x7C, task.ReadDword(0x3C));
                task = task.WriteWord(0x7A, W(inputs.CenterY(task)));
                int distance = Math.Min((int)inputs.HalfHeight(task), 0xA00);
                task = task.WriteWord(0x84, W(distance)).WriteWord(0x86, W(unchecked(distance << 12) / 432));
                int scale = Math.Clamp((int)S(task, 0x86), 0x1000, 0x4000);
                task = task.WriteWord(0x86, W(unchecked(scale * 11 * 256) / 4096));
                if (S(task, 0x7A) + distance > -0x200)
                {
                    task = task.WriteWord(0x88, 1).WriteWord(0x7A, W(unchecked(S(task, 0x7A) * 3 * 1024) / 4096));
                }
                task = inputs.Append(task, 0x89B8A0, null);
                task = inputs.Append(task, 0x89AAE0, 0);
                task = Next(task);
                break;
            case 1:
                if (counter >= 2)
                {
                    task = inputs.Append(task, 0x89BA80, null);
                    if (S(task, 0x24) >= 2)
                    {
                        task = inputs.Append(task, 0x89AAE0, 1);
                        task = Next(task);
                    }
                }
                break;
            case 2:
            case 3:
            case 4:
            case 5:
                if (counter >= phase * 2)
                {
                    task = inputs.Append(task, 0x89AAE0, (short)phase);
                    task = Next(task);
                }
                break;
            case 6:
                if (counter >= 0x1E)
                {
                    task = inputs.Append(task, 0x89CA70, null);
                    task = inputs.Append(task, 0x89C270, null);
                    task = Next(task);
                }
                break;
            case 7:
                if (counter >= 0x20)
                {
                    task = inputs.Append(task, 0x89CD60, null);
                    task = inputs.Append(task, 0x89D020, null);
                    task = Next(task);
                }
                break;
            case 8:
                if (counter >= 0x22)
                {
                    task = inputs.Append(task, 0x89D3D0, null);
                    task = Next(task);
                }
                break;
            case 9:
                if (task.ReadWord(0x80) == 1)
                {
                    task = inputs.Contact(task);
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                break;
            case 10:
                break;
            default:
                throw new NotSupportedException("Native Bio primary phase leaves its eleven callbacks.");
        }
        task = task.WriteWord(0x82, (ushort)((task.ReadWord(0x82) - 0x40) & 0xFFF));
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), S(task, 0x82));
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x58 + index * 2, W(matrix.Rotation[index]));
        }
        task = task.WriteDword(0x6C, unchecked((uint)matrix.X)).WriteDword(0x70, unchecked((uint)matrix.Y)).WriteDword(0x74, unchecked((uint)matrix.Z));
        if (task.ReadWord(0x24) == 0)
        {
            task = inputs.Sound(task);
        }
        byte flags = task.ReadByte(0x26);
        task = task.WriteWord(0x24, W(task.ReadWord(0x24) + 1));
        return new(task, (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u);
    }

    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static short S(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort W(int value) => unchecked((ushort)value);
}
