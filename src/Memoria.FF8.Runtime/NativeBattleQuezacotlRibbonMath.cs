using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlRibbonSample(NativeBattleVector Position, NativeBattleVector Normal);

/// <summary>6C9100 control triangles and 6C8F70 interpolation, including original lookup-table normalization.</summary>
public static class NativeBattleQuezacotlRibbonMath
{
    public static NativeBattleQuezacotlRibbonSample Sample6C8F70(ImmutableArray<byte> triangles, int firstTriangle, short frames, short progress, Func<int, NativeBattleActionMatrix> readBoneMatrix, ImmutableArray<ushort> sqrtWords256)
    {
        int phase = unchecked((frames - 1) * progress);
        int selected = checked(firstTriangle + (phase >> 12));
        NativeBattleQuezacotlRibbonSample first = Triangle6C9100(triangles, selected, readBoneMatrix, sqrtWords256);
        int ratio = phase & 0xFFF;
        if (ratio == 0)
        {
            return first;
        }
        NativeBattleQuezacotlRibbonSample second = Triangle6C9100(triangles, checked(selected + 1), readBoneMatrix, sqrtWords256);
        NativeBattleVector normal = Blend(first.Normal, second.Normal, 4096 - ratio, ratio);
        return new(Blend(first.Position, second.Position, 4096 - ratio, ratio), Normalize(normal.X, normal.Y, normal.Z, sqrtWords256, false));
    }

    public static NativeBattleQuezacotlRibbonSample Triangle6C9100(ImmutableArray<byte> triangles, int index, Func<int, NativeBattleActionMatrix> readBoneMatrix, ImmutableArray<ushort> sqrtWords256)
    {
        if (triangles.IsDefault || index < 0 || index > (triangles.Length - 24) / 24 || triangles.Length < 24)
        {
            throw new InvalidDataException("Native6C9100 control triangle exceeds its actual source bank.");
        }
        NativeBattleVector[] points = new NativeBattleVector[3];
        for (int corner = 0; corner < 3; corner++)
        {
            ReadOnlySpan<byte> source = triangles.AsSpan().Slice(checked(index * 24 + corner * 8), 8);
            NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(source), BinaryPrimitives.ReadInt16LittleEndian(source[2..]), BinaryPrimitives.ReadInt16LittleEndian(source[4..]));
            NativeBattleActionMatrix bone = readBoneMatrix(BinaryPrimitives.ReadInt16LittleEndian(source[6..]));
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(bone, local);
            points[corner] = new(Saturate(unchecked(x + bone.X)), Saturate(unchecked(y + bone.Y)), Saturate(unchecked(z + bone.Z)));
        }
        int ax = unchecked((short)(points[0].X - points[1].X));
        int ay = unchecked((short)(points[0].Y - points[1].Y));
        int az = unchecked((short)(points[0].Z - points[1].Z));
        int bx = unchecked((short)(points[2].X - points[0].X));
        int by = unchecked((short)(points[2].Y - points[0].Y));
        int bz = unchecked((short)(points[2].Z - points[0].Z));
        NativeBattleVector normal = Normalize(unchecked(ay * bz - az * by), unchecked(az * bx - ax * bz), unchecked(ax * by - ay * bx), sqrtWords256, true);
        short Centre(int component)
        {
            int value = component == 0 ? points.Sum(point => point.X) : component == 1 ? points.Sum(point => point.Y) : points.Sum(point => point.Z);
            return unchecked((short)(unchecked(value * 21845) >> 16));
        }
        return new(new(Centre(0), Centre(1), Centre(2)), normal);
    }

    public static int SqrtQ12(int value, ImmutableArray<ushort> sqrtWords256)
    {
        if (sqrtWords256.IsDefault || sqrtWords256.Length != 256)
        {
            throw new InvalidDataException("Native ribbon normalization requires the original256-WORD12B4570 lookup table.");
        }
        int even = LeadingSignCount(value) & ~1;
        int index = even <= 24 ? value >> (24 - even) : unchecked(value << (even - 24));
        if ((uint)index >= 256)
        {
            throw new NotSupportedException("Native ribbon sqrt index requires source bytes outside its retained256-WORD table.");
        }
        return unchecked(sqrtWords256[index] << ((31 - even) >> 1));
    }

    public static NativeBattleVector Normalize(int x, int y, int z, ImmutableArray<ushort> sqrtWords256, bool scaleCrossProduct)
    {
        if (scaleCrossProduct)
        {
            int Abs(int value)
            {
                return value < 0 ? unchecked(-value) : value;
            }
            int leading = LeadingSignCount(Math.Max(Abs(x), Math.Max(Abs(y), Abs(z))));
            if (leading < 18)
            {
                int shift = 18 - leading;
                x >>= shift;
                y >>= shift;
                z >>= shift;
            }
        }
        int length = SqrtQ12(SquaredLength(x, y, z), sqrtWords256) >> 12;
        if (length == 0)
        {
            throw new InvalidDataException("Native ribbon normalization reaches an original zero-divisor IDIV.");
        }
        return new(unchecked((short)(unchecked(x << 12) / length)), unchecked((short)(unchecked(y << 12) / length)), unchecked((short)(unchecked(z << 12) / length)));
    }

    internal static int SquaredLength(int x, int y, int z)
    {
        x = unchecked((short)x);
        y = unchecked((short)y);
        z = unchecked((short)z);
        return unchecked(x * x + y * y + z * z);
    }

    internal static NativeBattleVector Scale(NativeBattleVector vector, int weight)
    {
        return Blend(vector, new(0, 0, 0), weight, 0);
    }

    internal static NativeBattleVector Blend(NativeBattleVector first, NativeBattleVector second, int firstWeight, int secondWeight)
    {
        short Component(short a, short b)
        {
            // 45E9D0 MAC then 45EBF0: separately rounded products, followed by final signed IR saturation.
            int mac = unchecked(NativePrecision53.RoundInteger(a * (unchecked((short)firstWeight) / 4096d)) + NativePrecision53.RoundInteger(b * (unchecked((short)secondWeight) / 4096d)));
            return Saturate(mac);
        }
        return new(Component(first.X, second.X), Component(first.Y, second.Y), Component(first.Z, second.Z));
    }

    private static int LeadingSignCount(int value)
    {
        return BitOperations.LeadingZeroCount(value < 0 ? ~(uint)value : (uint)value);
    }

    private static short Saturate(int value)
    {
        return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    }
}
