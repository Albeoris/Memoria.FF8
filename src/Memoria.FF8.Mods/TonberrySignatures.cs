using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods;

public sealed record TonberrySignatures(ulong Primary, BigInteger Secondary, uint ObjectTop, uint ObjectBottom)
{
    public const string ReferenceRevision = "04a9ac6f6cb87490d312705161079a55e347a637";
    private static readonly int[] SecondaryX = [44, 0, 17, 25, 15, 111, 35, 25, 3, 46, 112, 34, 21, 1, 72, 80, 25, 32, 15, 4, 123, 16, 47, 14, 110, 78, 3, 66, 0, 86, 58, 27, 39, 4, 6, 49, 7, 71, 121, 17, 22, 16, 84, 115, 118, 119, 126, 59, 96, 88, 64, 1, 21, 31, 107, 92, 73, 116, 118, 58, 47, 18, 93, 78, 97, 106, 107, 77, 99, 13, 100, 125, 12, 33, 53, 61];
    private static readonly int[] SecondaryY = [243, 0, 2, 19, 35, 24, 0, 12, 23, 7, 5, 0, 4, 0, 2, 218, 30, 2, 20, 23, 4, 4, 2, 8, 7, 7, 25, 0, 1, 0, 11, 15, 2, 0, 0, 1, 15, 15, 16, 7, 7, 0, 244, 245, 245, 245, 253, 203, 135, 184, 9, 15, 80, 81, 244, 245, 249, 255, 238, 237, 216, 218, 240, 216, 116, 164, 244, 247, 236, 245, 21, 59, 25, 8, 16, 108];

    public static TonberrySignatures Calculate(PixelImage original)
    {
        ArgumentNullException.ThrowIfNull(original);
        int[] primary = new int[64];
        int[] objects = new int[64];
        int position = 0;
        int top = 0;
        int bottom = 0;
        void Sample(int x, int y)
        {
            int value = Mean(original, x, y);
            primary[position++] = value;
            if (x >= original.Width || y >= original.Height)
            {
                return;
            }
            if (y < 128 && top < 32)
            {
                objects[top++] = value;
            }
            else if (y >= 128 && bottom < 32)
            {
                objects[32 + bottom++] = value;
            }
        }
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                Sample(x * 16, y * 16);
            }
        }
        for (int x = 0; x < 2; x++)
        {
            for (int y = 4; y < 16; y++)
            {
                Sample(x * 16, y * 16);
            }
        }
        for (int x = 3; x < 7; x += 3)
        {
            for (int y = 5; y < 15; y += 3)
            {
                Sample(x * 16, y * 16);
            }
        }
        int[] secondary = new int[98];
        for (int i = 0; i < SecondaryX.Length; i++)
        {
            secondary[i] = Mean(original, SecondaryX[i], SecondaryY[i]);
        }
        position = 76;
        for (int x = 0; x < 44; x += 4)
        {
            for (int y = 7; y < 16; y += 8)
            {
                secondary[position++] = Mean(original, x, y);
            }
        }
        return new TonberrySignatures((ulong)Compare(primary, primary[^1]), Compare(secondary, secondary[^1]), (uint)Compare(objects.AsSpan(0, 32), objects[^1]), (uint)Compare(objects.AsSpan(32, 32), objects[31]));
    }

    private static BigInteger Compare(ReadOnlySpan<int> values, int previous)
    {
        BigInteger result = 0;
        foreach (int value in values)
        {
            result = result * 2 + (value >= previous ? 1 : 0);
            previous = value;
        }
        return result;
    }

    private static int Mean(PixelImage image, int x, int y)
    {
        if (x >= image.Width || y >= image.Height)
        {
            return 0;
        }
        Rgba32 color = image[x, y];
        return (color.R + color.G + color.B) / 3;
    }
}
