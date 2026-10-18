using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>571620/571690's low-DWORD fixed-point Bernstein coefficients and ordered powers.</summary>
public static class NativeBattleGilgameshSwordTrailCurve
{
    public static NativeBattleEffectArena Prepare571620(int count, NativeBattleEffectArena points, int pointOffset, NativeBattleEffectArena work)
    {
        RequireCount(count);
        int degree = count - 1;
        int choose = 1;
        for (int index = 0; index < count; index++)
        {
            if (index == 1)
            {
                choose = degree;
            }
            else if (index >= 2)
            {
                choose = unchecked((degree - index + 1) * choose) / index;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(points.Read(checked(pointOffset + index * 8 + axis * 2), 2));
                byte[] bytes = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, unchecked(value * choose));
                work = work.Write(index * 16 + axis * 4, bytes);
            }
        }
        return work;
    }

    public static NativeBattleEffectArena Evaluate571690(int count, NativeBattleEffectArena work, int fraction, NativeBattleEffectArena output, int outputOffset)
    {
        RequireCount(count);
        int degree = count - 1;
        int q = unchecked(4096 - fraction);
        byte[] point = new byte[6];
        for (int axis = 0; axis < 3; axis++)
        {
            int[] weighted = new int[count];
            weighted[0] = Coefficient(0);
            int power = fraction;
            for (int index = 1; index <= degree; index++)
            {
                weighted[index] = unchecked(Coefficient(index) * power) >> 12;
                power = unchecked(power * fraction) >> 12;
            }
            int accumulation = weighted[degree];
            int powerQ = q;
            for (int index = degree - 1; index >= 0; index--)
            {
                accumulation = unchecked(accumulation + (unchecked(weighted[index] * powerQ) >> 12));
                powerQ = unchecked(powerQ * q) >> 12;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(point.AsSpan(axis * 2), unchecked((ushort)accumulation));

            int Coefficient(int index) => BinaryPrimitives.ReadInt32LittleEndian(work.Read(index * 16 + axis * 4, 4));
        }
        return output.Write(outputOffset, point);
    }

    private static void RequireCount(int count)
    {
        if (count is < 1 or > 10)
        {
            throw new InvalidDataException("Native sword trail curve requires one through ten gathered control points.");
        }
    }
}
