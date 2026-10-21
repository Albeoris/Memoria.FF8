using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15Jitter(NativeBattleModulePoolTask Task, NativeCrtRandomState Random, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations);
public sealed record NativeBattleDrawStock15Position(NativeBattleModulePoolTask Task, NativeBattleActionMatrix Matrix, NativeBattleVector Point);

public static partial class NativeBattleDrawStock15Particles
{
    public static NativeBattleDrawStock15Jitter Offset8B5BB0(NativeBattleModulePoolTask task, short radius, NativeCrtRandomState random)
    {
        if (radius == 0)
        {
            radius = 1;
        }
        random = random.Draw(out ushort first);
        random = random.Draw(out ushort second);
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        short yaw = (short)(first & 0xFFF);
        short pitch = (short)(second & 0xFFF);
        NativeBattleActionMatrix y = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, yaw);
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(y, pitch);
        random = random.Draw(out ushort third);
        NativeBattleVector local = new(0, 0, unchecked((short)(third % radius)));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
        task = task.WriteWord(0x1C, unchecked((ushort)(Word(task, 0x1C) + 2 * offset.X))).WriteWord(0x1E, unchecked((ushort)(Word(task, 0x1E) + offset.Y))).WriteWord(0x20, unchecked((ushort)(Word(task, 0x20) + 2 * offset.Z)));
        return new(task, random, [new NativeBattleDrawStock15Rotate(identity, 'Y', yaw), new NativeBattleDrawStock15Rotate(y, 'X', pitch), new NativeBattleDrawStock15Transform(rotation, local)]);
    }

    public static ushort? ReadPadding(NativeBattleModulePoolTask task, int offset)
    {
        return task.UnknownPayloadRanges?.Any(range => range.Offset < offset + 2 && range.Offset + range.Length > offset) == true ? null : task.ReadWord(offset);
    }

    public static NativeBattleActionMatrix LookBasis8DDA50(NativeBattleVector direction)
    {
        short x = unchecked((short)direction.X);
        short y = unchecked((short)direction.Y);
        short z = unchecked((short)direction.Z);
        short crossZ = (short)Math.Clamp(-(int)x, short.MinValue, short.MaxValue);
        return new([z, 0, x, 0, 4096, y, crossZ, 0, z], 0, 0, 0, 4096);
    }

    public static NativeBattleDrawStock15Position Position8B5E00(NativeBattleModulePoolTask task)
    {
        short counter = Word(task, 0x124);
        task = task.WriteDword(0x1C, task.ReadDword(0x88)).WriteDword(0x20, task.ReadDword(0x8C));
        for (int axis = 0; axis < 3; axis++)
        {
            int start = Word(task, 0x88 + axis * 2);
            int delta = Word(task, 0x80 + axis * 2) - start;
            task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)(Word(task, 0x1C + axis * 2) + unchecked(delta * counter) / 10)));
        }
        NativeBattleActionMatrix rotation = ReadRotation(task, 0x60);
        short angle = unchecked((short)(204 * counter));
        NativeBattleVector local = new(0, unchecked((short)(NativeBattleCameraMath.Sine(angle) / 3)), 0);
        NativeBattleVector moved = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
        task = task.WriteWord(0x1C, unchecked((ushort)(Word(task, 0x1C) + moved.X))).WriteWord(0x1E, unchecked((ushort)(Word(task, 0x1E) + moved.Y))).WriteWord(0x20, unchecked((ushort)(Word(task, 0x20) + moved.Z)));
        int row = checked(0x90 + counter * 8);
        task = task.WriteDword(row, task.ReadDword(0x1C)).WriteDword(row + 4, task.ReadDword(0x20));
        return new(task, rotation, local);
    }

    public static NativeBattleModulePoolTask AdvanceTrailCounters(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(0x124, unchecked((ushort)(task.ReadWord(0x124) + 1)));
        if (Word(task, 0x124) > 10)
        {
            task = task.WriteWord(0x124, 10);
        }
        task = task.WriteWord(0x12A, unchecked((ushort)(task.ReadWord(0x12A) + 1)));
        if (Word(task, 0x12A) < 10)
        {
            task = task.WriteWord(0x128, unchecked((ushort)(Word(task, 0x12A) - 6)));
            if (Word(task, 0x128) < 1)
            {
                task = task.WriteWord(0x128, 1);
            }
        }
        else
        {
            task = task.WriteWord(0x128, unchecked((ushort)(task.ReadWord(0x128) + 1))).WriteWord(0x12A, 10);
            if (Word(task, 0x128) > 10)
            {
                task = task.WriteWord(0x128, 10);
            }
        }
        return task;
    }

    public static NativeBattleActionMatrix ReadRotation(NativeBattleModulePoolTask task, int offset)
    {
        return new(Enumerable.Range(0, 9).Select(index => Word(task, offset + index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
    }

    private static NativeBattleModulePoolTask WriteRotation(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }
}
