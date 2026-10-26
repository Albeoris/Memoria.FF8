using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187MorphVertices
{
    public static NativeBattleEffectArena Interpolate64B300(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, NativeBattleEffectArena destination, int age)
    {
        if (first.Length != 1008 || second.Length != 1008)
        {
            throw new InvalidDataException("Effect187 morph requires its two actual 126-row packed vertex banks.");
        }
        int factor = NativeBattleCameraMath.Sine(unchecked(age * 1024) / 12);
        for (int vertex = 0; vertex < 126; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = vertex * 8 + axis * 2;
                int start = BinaryPrimitives.ReadInt16LittleEndian(first[offset..]);
                int end = BinaryPrimitives.ReadInt16LittleEndian(second[offset..]);
                short value = unchecked((short)(start + (unchecked((end - start) * factor) >> 12)));
                destination = destination.Write(offset, BitConverter.GetBytes(value));
            }
        }
        return destination;
    }
}
