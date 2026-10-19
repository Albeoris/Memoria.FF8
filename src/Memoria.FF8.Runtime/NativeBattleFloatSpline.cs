using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFloatSplineResult(NativeBattleEffectArena Output, NativeBattleEffectArena Work400, NativeBattleEffectArena Work68);

/// <summary>Native float spline interpolation with retained caller scratch and ordered single-precision stores.</summary>
public static class NativeBattleFloatSpline
{
    public static NativeBattleFloatSplineResult Interpolate5EA210(NativeBattleEffectArena points, int pointsOffset, int count, int samples, NativeBattleEffectArena output, int outputOffset, NativeBattleEffectArena work400, NativeBattleEffectArena work68)
    {
        if (count is < 2 or > 8 || samples < 0)
        {
            throw new NotSupportedException("Native float spline requires its bounded original point count and nonnegative sample count.");
        }
        int stride = samples / (count - 1);
        int evaluated = checked((count - 2) * stride + stride + 1);
        if (stride + 1 > short.MaxValue || evaluated > 49)
        {
            throw new NotSupportedException("Native float spline samples leave their original400-byte temporary allocation.");
        }
        for (int index = 0; index < count; index++)
        {
            work400 = WriteFloat(work400, index * 4, index);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            for (int index = 0; index < count; index++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(points.Read(checked(pointsOffset + index * 8 + axis * 2), 2));
                work400 = WriteFloat(work400, 68 + index * 4, value);
            }
            (work400, work68) = Prepare5EA4B0(count, work400, work68);
            for (int interval = 0; interval < count - 1; interval++)
            {
                work400 = Evaluate5EA630(work400, interval, checked((short)(stride + 1)), checked(204 + interval * stride * 4));
            }
            for (int index = 0; index < samples; index++)
            {
                double value = ReadFloat(work400, 204 + index * 4);
                long integer = !double.IsFinite(value) || value < -9223372036854775808d || value >= 9223372036854775808d ? long.MinValue : (long)Math.Truncate(value);
                byte[] word = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(word, unchecked((ushort)integer));
                output = output.Write(checked(outputOffset + index * 8 + axis * 2), word);
            }
        }
        return new(output, work400, work68);
    }

    public static (NativeBattleEffectArena Work400, NativeBattleEffectArena Work68) Prepare5EA4B0(int count, NativeBattleEffectArena work400, NativeBattleEffectArena work68)
    {
        if (count is < 2 or > 8)
        {
            throw new NotSupportedException("Native float spline coefficients leave their original68-byte temporary allocation.");
        }
        work400 = WriteFloat(WriteFloat(work400, 136, 0), 136 + (count - 1) * 4, 0);
        for (int index = 0; index < count - 1; index++)
        {
            double width = (double)ReadFloat(work400, (index + 1) * 4) - ReadFloat(work400, index * 4);
            work68 = WriteFloat(work68, index * 4, width);
            double rise = (double)ReadFloat(work400, 68 + (index + 1) * 4) - ReadFloat(work400, 68 + index * 4);
            work68 = WriteFloat(work68, count * 4 + (index + 1) * 4, rise / width);
        }
        work400 = WriteFloat(work400, 140, (double)ReadFloat(work68, count * 4 + 8) - ReadFloat(work68, count * 4 + 4));
        double diagonal = (double)ReadFloat(work400, 8) - ReadFloat(work400, 0);
        work68 = WriteFloat(work68, count * 4 + 4, diagonal + diagonal);
        for (int index = 2; index < count - 1; index++)
        {
            double factor = (double)ReadFloat(work68, (index - 1) * 4) / ReadFloat(work68, count * 4 + (index - 1) * 4);
            double right = (double)ReadFloat(work68, count * 4 + (index + 1) * 4) - ReadFloat(work68, count * 4 + index * 4);
            right -= factor * ReadFloat(work400, 136 + (index - 1) * 4);
            work400 = WriteFloat(work400, 136 + index * 4, right);
            double span = (double)ReadFloat(work400, (index + 1) * 4) - ReadFloat(work400, (index - 1) * 4);
            work68 = WriteFloat(work68, count * 4 + index * 4, span + span - factor * ReadFloat(work68, (index - 1) * 4));
        }
        int last = count - 2;
        work400 = WriteFloat(work400, 136 + last * 4, (double)ReadFloat(work400, 136 + last * 4) - (double)ReadFloat(work68, last * 4) * ReadFloat(work400, 136 + (last + 1) * 4));
        for (int index = last; index > 0; index--)
        {
            double value = (double)ReadFloat(work400, 136 + index * 4) - (double)ReadFloat(work68, index * 4) * ReadFloat(work400, 136 + (index + 1) * 4);
            work400 = WriteFloat(work400, 136 + index * 4, value / ReadFloat(work68, count * 4 + index * 4));
        }
        return (work400, work68);
    }

    public static NativeBattleEffectArena Evaluate5EA630(NativeBattleEffectArena work400, int interval, short count, int destination)
    {
        if (interval is < 0 or >= 7 || count < 1 || destination < 0 || destination > 400 - count * 4)
        {
            throw new NotSupportedException("Native float spline interval leaves its retained temporary allocation.");
        }
        int steps = count - 1;
        float first = ReadFloat(work400, 68 + interval * 4);
        float next = ReadFloat(work400, 68 + (interval + 1) * 4);
        work400 = WriteFloat(WriteFloat(work400, destination + steps * 4, next), destination, first);
        if (steps <= 1)
        {
            return work400;
        }
        double width = (double)ReadFloat(work400, (interval + 1) * 4) - ReadFloat(work400, interval * 4);
        double square = width * width;
        float coefficient = ReadFloat(work400, 136 + interval * 4);
        float nextCoefficient = ReadFloat(work400, 136 + (interval + 1) * 4);
        float left = (float)((double)first + first + next - ((double)coefficient + coefficient + nextCoefficient) * square);
        double right = (double)next + next + first - ((double)nextCoefficient + nextCoefficient + coefficient) * square;
        float denominator = steps;
        float cubic = (float)(((double)left + next - right - first) / denominator / denominator / denominator);
        float threeFirst = (float)((double)first * 3d);
        float quadratic = (float)((right - ((double)left + left) + threeFirst) / denominator / denominator);
        double delta = ((double)left - threeFirst) / denominator - ((double)quadratic - cubic);
        double third = (double)cubic * 6d;
        float storedThird = (float)third;
        double second = quadratic - third + quadratic;
        double value = first;
        for (int index = 1; index <= steps; index++)
        {
            second += storedThird;
            delta += second;
            value += delta;
            work400 = WriteFloat(work400, destination + index * 4, value);
        }
        return work400;
    }

    private static float ReadFloat(NativeBattleEffectArena memory, int offset) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(memory.Read(offset, 4)));

    private static NativeBattleEffectArena WriteFloat(NativeBattleEffectArena memory, int offset, double value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits((float)value));
        return memory.Write(offset, bytes);
    }
}
