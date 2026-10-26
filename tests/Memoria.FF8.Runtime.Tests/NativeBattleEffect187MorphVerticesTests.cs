using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187MorphVerticesTests
{
    [Theory]
    [InlineData(0, -300)]
    [InlineData(6, 265)]
    [InlineData(12, 500)]
    public void SignedInterpolationUsesTheNativeSineClockAndRetainsEveryPackedW(int age, short expected)
    {
        byte[] first = Bank(-300);
        byte[] second = Bank(500);
        NativeBattleEffectArena destination = NativeBattleEffectArena.Clear().MarkUnknown(0, 1008);
        destination = destination.Write(6, BitConverter.GetBytes((ushort)0xABCD));
        NativeBattleEffectArena current = NativeBattleEffect187MorphVertices.Interpolate64B300(first, second, destination, age);
        for (int row = 0; row < 126; row++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(current.Read(row * 8 + axis * 2, 2)));
            }
        }
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(current.Read(6, 2)));
        Assert.Throws<NotSupportedException>(() => current.Read(14, 2));
        Assert.Equal((short)-300, BinaryPrimitives.ReadInt16LittleEndian(first));
        Assert.Equal((short)500, BinaryPrimitives.ReadInt16LittleEndian(second));
    }

    private static byte[] Bank(short coordinate)
    {
        byte[] bytes = new byte[1008];
        for (int row = 0; row < 126; row++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(row * 8 + axis * 2), coordinate);
            }
        }
        return bytes;
    }
}
