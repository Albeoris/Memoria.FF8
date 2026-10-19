using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleControl110Motion
{
    public static ImmutableArray<byte> Normalize(ImmutableArray<byte> vertices)
    {
        if (vertices.Length != 308 * 8)
        {
            throw new InvalidDataException("Native control requires all308 retained stream vertices.");
        }
        int minimum = short.MaxValue;
        int maximum = short.MinValue;
        for (int offset = 4; offset < vertices.Length; offset += 8)
        {
            int z = BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(offset));
            minimum = Math.Min(minimum, z);
            maximum = Math.Max(maximum, z);
        }
        int range = maximum - minimum;
        if (range == 0)
        {
            throw new DivideByZeroException("Native control stream has zero signed Z extent.");
        }
        byte[] result = vertices.ToArray();
        for (int offset = 4; offset < vertices.Length; offset += 8)
        {
            int z = BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(offset));
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset + 2), unchecked((short)((z << 12) / range)));
        }
        return result.ToImmutableArray();
    }

    public static NativeBattleActionMatrix StreamMatrix(NativeBattleActionMatrix basis, int distance, int phase, NativeBattleVector position)
    {
        short scale = unchecked((short)distance);
        if (phase < 8)
        {
            scale = unchecked((short)(scale * NativeBattleCameraMath.Sine(phase << 7) >> 12));
        }
        return NativeBattleGfMatrices.Multiply56C090(basis, new([4096, 0, 0, 0, 4096, 0, 0, 0, scale], 0, 0, 0, 4096)) with { X = position.X, Y = position.Y, Z = position.Z };
    }
}
