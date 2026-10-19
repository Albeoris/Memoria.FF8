using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel112Geometry
{
    public static NativeBattleEffectArena Deform6CD7F0(ReadOnlySpan<byte> source, NativeBattleEffectArena arena, int destination, NativeBattleActionMatrix matrix, short maximumY, short rangeY, short amplitude)
    {
        if (source.Length < 8)
        {
            throw new InvalidDataException("Native Duel deformation requires its actual source vertex owner.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
        if (count <= 0 || count > (source.Length - 8) / 8)
        {
            throw new InvalidDataException("Native Duel deformation requires its complete positive vertex count.");
        }
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> vertex = source.Slice(8 + index * 8, 6);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(vertex.Slice(4)));
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(matrix, point);
            int angle = unchecked((maximumY - transformed.Y) << 10) / rangeY;
            angle = unchecked(angle - 1024);
            int first = NativeBattleCameraMath.Sine(angle);
            int second = NativeBattleCameraMath.Sine(first >> 2);
            int shift = unchecked((second + 4096) * amplitude) >> 12;
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, transformed.X);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), transformed.Y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), unchecked((short)(transformed.Z - shift)));
            arena = arena.Write(checked(destination + 8 * index), xyz);
        }
        return arena;
    }

    public static NativeBattleVector Midpoint6CD8B0(uint actor, Func<uint, byte, short, NativeBattleVector> readBone)
    {
        NativeBattleVector first = readBone(actor, 21, 0);
        NativeBattleVector second = readBone(actor, 22, 0);
        return new(unchecked((short)((first.X + second.X) >> 1)), unchecked((short)((first.Y + second.Y) >> 1)), unchecked((short)((first.Z + second.Z) >> 1)));
    }
}
