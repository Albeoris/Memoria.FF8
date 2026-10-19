using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGuardianCameraProgram
{
    private static NativeBattleEffectArena? TemporaryPoint(NativeBattleEffectArena? temporary, int offset, NativeBattleVector point)
    {
        if (temporary is null)
        {
            return null;
        }
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
        return temporary.Write(offset, bytes);
    }

    private static NativeBattleEffectArena? TemporaryLongVector(NativeBattleEffectArena? temporary, int offset, int x, int y, int z)
    {
        if (temporary is null)
        {
            return null;
        }
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), y);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), z);
        return temporary.Write(offset, bytes);
    }

    private static NativeBattleEffectArena TemporarySlice(NativeBattleEffectArena memory, int offset, int length)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, length);
        return TemporaryWriteKnown(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length), 0, bytes, known);
    }

    private static NativeBattleEffectArena TemporaryPublish(NativeBattleEffectArena destination, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        return TemporaryWriteKnown(destination.MarkUnknown(offset, length), offset, bytes, known);
    }

    private static NativeBattleEffectArena TemporaryWriteKnown(NativeBattleEffectArena memory, int destination, byte[] bytes, bool[] known)
    {
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            if (!known[cursor])
            {
                cursor++;
                continue;
            }
            int first = cursor;
            while (cursor < bytes.Length && known[cursor])
            {
                cursor++;
            }
            memory = memory.Write(destination + first, bytes.AsSpan(first, cursor - first));
        }
        return memory;
    }
}
