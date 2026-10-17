namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEnhancementSpriteMotionStep(NativeBattleModulePoolTask Task, NativeCrtRandomState Random);

/// <summary>The identical Double/Triple fan phases and explicitly addressed ground pulse/direction fields.</summary>
public static class NativeBattleEnhancementSpriteMotion
{
    public static NativeBattleEnhancementSpriteMotionStep AdvanceFan(NativeBattleModulePoolTask task, NativeCrtRandomState random)
    {
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0:
            case 1:
            case 2:
            case 4:
            case 5:
                byte color = phase switch { 0 or 5 => 24, 1 or 4 => 48, _ => 64 };
                task = SetColor(task, color);
                task = IncrementPhase(task);
                break;
            case 3:
                task = IncrementPhase(task);
                break;
            case 6:
                task = SetColor(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)), 0);
                break;
            case 7:
                break;
            default:
                throw new InvalidDataException("Native8D58A0 signed phase exceeds its eight original callbacks.");
        }
        random = random.Draw(out ushort angle);
        task = task.WriteWord(0x40, (ushort)(angle & 0xFFF));
        random = random.Draw(out ushort frame);
        task = task.WriteWord(0x50, (ushort)((frame % 7 + task.ReadByte(0x50) + 1) & 7));
        return new(task, random);
    }

    public static NativeBattleEnhancementSpriteMotionStep AdvanceGround(NativeBattleModulePoolTask task, NativeCrtRandomState random, Func<uint, NativeBattleModulePoolTask> readTask, bool triple)
    {
        int pulseOffset = triple ? 0x6C : 0x64;
        int directionOffset = triple ? 0x6A : 0x62;
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        switch (phase)
        {
            case 0:
                task = AddColor(task, 4);
                if (task.ReadByte(0x48) >= 32)
                {
                    task = IncrementPhase(task);
                }
                break;
            case 1:
                NativeBattleModulePoolTask first = readTask(task.ReadDword(0x14));
                if (first.ReadWord(0x58) == 2)
                {
                    task = IncrementPhase(task.WriteWord(pulseOffset, 3));
                }
                break;
            case 2:
                if (task.ReadWord(pulseOffset) == 0)
                {
                    task = AddColor(task, -4);
                    if (task.ReadByte(0x48) == 0)
                    {
                        task = IncrementPhase(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                    }
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Native8D6200 signed phase exceeds its four original callbacks.");
        }
        //Original jump table8D6318:1/3 reset64 then advance;2/4 decay until unsigned red<=32.
        switch (unchecked((short)task.ReadWord(pulseOffset)))
        {
            case 1:
            case 3:
                task = SetColor(task, 64).WriteWord(pulseOffset, unchecked((ushort)(task.ReadWord(pulseOffset) + 1)));
                break;
            case 2:
            case 4:
                task = AddColor(task, -4);
                if (task.ReadByte(0x48) <= 32)
                {
                    task = task.WriteWord(pulseOffset, 0);
                }
                break;
        }
        task = task.WriteDword(0x1C, task.ReadDword(0x58)).WriteDword(0x20, task.ReadDword(0x5C));
        short direction = unchecked((short)task.ReadWord(directionOffset));
        task = AddWord(task, 0x1C, NativeBattleCameraMath.Sine(direction) / 4);
        task = AddWord(task, 0x20, NativeBattleCameraMath.Cosine(direction) / 4);
        return new(task, random);
    }

    private static NativeBattleModulePoolTask SetColor(NativeBattleModulePoolTask task, byte color) => task.WriteByte(0x48, color).WriteByte(0x49, color).WriteByte(0x4A, color);

    private static NativeBattleModulePoolTask AddColor(NativeBattleModulePoolTask task, int delta) => task.WriteByte(0x48, unchecked((byte)(task.ReadByte(0x48) + delta))).WriteByte(0x49, unchecked((byte)(task.ReadByte(0x49) + delta))).WriteByte(0x4A, unchecked((byte)(task.ReadByte(0x4A) + delta)));

    private static NativeBattleModulePoolTask AddWord(NativeBattleModulePoolTask task, int offset, int delta) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + delta)));

    private static NativeBattleModulePoolTask IncrementPhase(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
}
