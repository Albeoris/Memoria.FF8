using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public static class NativeWorldParticleMath
{
    public static short[] Rotation(short pitch, short yaw, short roll, bool camera = false)
    {
        int sx = NativeBattleCameraMath.Sine(pitch);
        int cx = NativeBattleCameraMath.Cosine(pitch);
        int sy = NativeBattleCameraMath.Sine(yaw);
        int cy = NativeBattleCameraMath.Cosine(yaw);
        int sz = NativeBattleCameraMath.Sine(roll);
        int cz = NativeBattleCameraMath.Cosine(roll);
        short[] x = [4096, 0, 0, 0, (short)cx, (short)-sx, 0, (short)sx, (short)cx];
        short[] y = [(short)cy, 0, (short)sy, 0, 4096, 0, (short)-sy, 0, (short)cy];
        short[] z = [(short)cz, (short)-sz, 0, (short)sz, (short)cz, 0, 0, 0, 4096];
        return camera ? Multiply(y, Multiply(x, z)) : Multiply(x, Multiply(y, z));
    }

    private static short[] Multiply(short[] left, short[] right)
    {
        short[] result = new short[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                long sum = (long)left[row * 3] * right[column] + (long)left[row * 3 + 1] * right[column + 3] + (long)left[row * 3 + 2] * right[column + 6];
                result[row * 3 + column] = unchecked((short)(int)Math.Round(sum / 4096d));
            }
        }
        return result;
    }

    public static Vector3 Transform(short[] matrix, Vector3 vector)
    {
        if (matrix.Length != 9)
        {
            throw new ArgumentException("A native rotation requires nine WORDs.", nameof(matrix));
        }
        int Component(int row) => unchecked((int)Math.Round((matrix[row] * (double)vector.X + matrix[row + 1] * (double)vector.Y + matrix[row + 2] * (double)vector.Z) / 4096));
        return new Vector3(Component(0), Component(3), Component(6));
    }

    public static (int X, int Y, int Depth) Project(Vector3 cameraPoint, int focalLength)
    {
        int z = Math.Clamp((int)cameraPoint.Z, 0, ushort.MaxValue);
        int divisor = Math.Max(z, focalLength >> 1);
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(focalLength));
        }
        double ratio = focalLength / (double)divisor;
        int x = Math.Clamp(unchecked((int)Math.Round(8 * cameraPoint.X * ratio) + 1280), -8192, 8184);
        int y = Math.Clamp(unchecked((int)Math.Round(8 * cameraPoint.Y * ratio) + 896), -8192, 8184);
        return (x, y, z);
    }

    public static float OrderingReciprocal(int proxyDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(proxyDepth);
        // Represent the zero-depth endpoint finitely: the GPU positive-depth map
        // maps this value to exactly1 without producing infinity/infinity.
        return proxyDepth == 0 ? float.MaxValue : 2f / proxyDepth;
    }
    public static int OrderingDepth(int a, int b, int c, int d, byte drawMode)
    {
        int quarterDepth = unchecked((a + b + c + d) * 256) >>> 12;
        if (drawMode == 0)
        {
            quarterDepth = unchecked(quarterDepth - 4);
        }
        return (uint)quarterDepth > 8191 ? -1 : quarterDepth >> 1;
    }

    public static (int X, int Y, int Depth)[] ProjectQuad(short[] view, Vector3 translation, Vector3 anchor, short[] rotation, int halfSize, byte drawMode, int focalLength)
    {
        static Vector3 Word(Vector3 value) => new(unchecked((short)(int)value.X), unchecked((short)(int)value.Y), unchecked((short)(int)value.Z));
        var center = Project(Transform(view, anchor) + translation, focalLength);
        if (drawMode == 1)
        {
            short size = unchecked((short)(2 * halfSize));
            if (size == 0)
            {
                size = 8;
            }
            return [(center.X - halfSize, center.Y - halfSize, center.Depth), (center.X - halfSize + size, center.Y - halfSize, center.Depth), (center.X - halfSize, center.Y - halfSize + size, center.Depth), (center.X - halfSize + size, center.Y - halfSize + size, center.Depth)];
        }
        if (drawMode == 2)
        {
            var first = Project(anchor + translation, focalLength);
            var second = Project(Word(anchor + new Vector3(halfSize, 0, 0)) + translation, focalLength);
            short extent = unchecked((short)(second.X - first.X));
            return [(center.X - extent, center.Y - extent, center.Depth), (center.X + extent, center.Y - extent, center.Depth), (center.X - extent, center.Y + extent, center.Depth), (center.X + extent, center.Y + extent, center.Depth)];
        }
        Vector3[] corners = drawMode switch
        {
            0 => [new(-halfSize, 0, halfSize), new(halfSize, 0, halfSize), new(-halfSize, 0, -halfSize), new(halfSize, 0, -halfSize)],
            3 => [new(-halfSize, 0, 2 * halfSize), new(halfSize, 0, 2 * halfSize), new(-halfSize, 0, -2 * halfSize), new(halfSize, 0, -2 * halfSize)],
            4 => [new(-halfSize, -halfSize, 0), new(halfSize, -halfSize, 0), new(-halfSize, halfSize, 0), new(halfSize, halfSize, 0)],
            _ => throw new NotSupportedException("Unsupported native world particle plane mode.")
        };
        return corners.Select(corner => Project(Transform(view, Word(Vector3.Clamp(Transform(rotation, Word(corner)), new Vector3(short.MinValue), new Vector3(short.MaxValue)) + anchor)) + translation, focalLength)).ToArray();
    }
}
