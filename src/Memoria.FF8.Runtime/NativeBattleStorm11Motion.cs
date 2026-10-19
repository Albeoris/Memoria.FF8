using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Storm Breath's native WORD stores and draw-before-update child motion.</summary>
public static class NativeBattleStorm11Motion
{
    public static NativeBattleModulePoolTask MoveSprite(NativeBattleModulePoolTask task, int lifetime, out bool ended)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x10 + axis * 2, unchecked((ushort)(Word(task, 0x10 + axis * 2) + Word(task, 0x18 + axis * 2))));
        }
        task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
        ended = Word(task, 12) >= lifetime;
        return task;
    }

    public static NativeBattleModulePoolTask MoveDebris6EB790(NativeBattleModulePoolTask task, out bool ended)
    {
        task = task.WriteWord(0x1A, unchecked((ushort)(Word(task, 0x1A) + 20)));
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x10 + axis * 2, unchecked((ushort)(Word(task, 0x10 + axis * 2) + Word(task, 0x18 + axis * 2))));
        }
        if (Word(task, 0x12) >= 0 && Word(task, 0x1A) > 0)
        {
            task = task.WriteWord(0x1A, unchecked((ushort)(-20 - Word(task, 0x1A))));
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(0x28 + axis * 2, unchecked((ushort)-Word(task, 0x28 + axis * 2)));
            }
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x20 + axis * 2, unchecked((ushort)(Word(task, 0x20 + axis * 2) + Word(task, 0x28 + axis * 2))));
        }
        task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
        ended = Word(task, 12) >= 30;
        return task;
    }

    public static ImmutableArray<byte> Wobble(ImmutableArray<byte> original, ImmutableArray<byte> current, short angle, short phase)
    {
        if (original.Length != 308 * 8 || current.Length != original.Length)
        {
            throw new InvalidDataException("Native Storm Breath requires every current and retained stream vertex.");
        }
        byte[] result = current.ToArray();
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.Identity8DD770(), angle);
        for (int index = 0; index < 308; index++)
        {
            ReadOnlySpan<byte> source = original.AsSpan().Slice(index * 8, 8);
            NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(source), BinaryPrimitives.ReadInt16LittleEndian(source[2..]), BinaryPrimitives.ReadInt16LittleEndian(source[4..]));
            NativeBattleVector rotated = NativeBattleGfMatrices.Transform460840(rotation, vertex);
            short zPhase = BinaryPrimitives.ReadInt16LittleEndian(source[6..]);
            short sway = unchecked((short)(phase + zPhase));
            int radius = NativeBattleCameraMath.Sine(zPhase >> 1) >> 3;
            short x = unchecked((short)(rotated.X + (short)(NativeBattleCameraMath.Cosine(sway) * radius >> 12)));
            short y = unchecked((short)(rotated.Y - (short)(NativeBattleCameraMath.Sine(sway) * radius >> 12)));
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8), x);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 8 + 4), rotated.Z);
        }
        return result.ToImmutableArray();
    }

    public static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    public static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4));
    public static NativeBattleModulePoolTask Store(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
}
