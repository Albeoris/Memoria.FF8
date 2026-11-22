using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

internal static class Ff7CondorGeometry
{
    internal static (short X, short Y) Normalize(int fromX, int fromY, int toX, int toY)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;
        int x = Math.Abs(dx);
        int y = Math.Abs(dy);
        (int vx, int vy) = x >= y ? (256, Math.Max(1, x == 0 ? 1 : (y << 8) / x)) : (Math.Max(1, (x << 8) / y), 256);
        return (checked((short)(dx < 0 ? -vx : vx)), checked((short)(dy < 0 ? -vy : vy)));
    }

    internal static int Direction(short vx, short vy)
    {
        int x = Math.Abs((int)vx);
        int y = Math.Abs((int)vy);
        return (vx < 0, vy < 0) switch
        {
            (false, false) => x == 256 ? 0xC00 + y * 2 : 0x1000 - x * 2,
            (true, false) => y == 256 ? x * 2 : 0x400 - y * 2,
            (false, true) => y == 256 ? 0x800 + x * 2 : 0xC00 - y * 2,
            (true, true) => x == 256 ? 0x400 + y * 2 : 0x800 - x * 2
        };
    }

    internal static bool Contains(ReadOnlySpan<byte> cell, short x, short y)
    {
        if (cell.Length != 0x4C)
        {
            throw new InvalidDataException("The FFVII Condor terrain cell is truncated.");
        }

        int px = unchecked((short)(x + 0x4000));
        int py = unchecked((short)(y + 0x4000));
        if (px < Word(cell, 0x40) || px > Word(cell, 0x42) || py < Word(cell, 0x44) || py > Word(cell, 0x46))
        {
            return false;
        }
        int first = Angle(cell, 0x28, x, y);
        int firstEdge = Angle(cell, 0x28, Word(cell, 0x30), Word(cell, 0x32));
        int secondEdge = Angle(cell, 0x28, Word(cell, 0x38), Word(cell, 0x3A)) + 8;
        if (Outside(first, firstEdge, secondEdge))
        {
            return false;
        }
        return !Outside(Angle(cell, 0x30, x, y), Angle(cell, 0x30, Word(cell, 0x38), Word(cell, 0x3A)), Angle(cell, 0x30, Word(cell, 0x28), Word(cell, 0x2A)));
    }

    private static short Word(ReadOnlySpan<byte> cell, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(cell[offset..]);
    }

    private static int Angle(ReadOnlySpan<byte> cell, int from, int toX, int toY)
    {
        (short vx, short vy) = Normalize(Word(cell, from), Word(cell, from + 2), toX, toY);
        return Direction(vx, vy);
    }

    private static bool Outside(int angle, int first, int second)
    {
        int maximum = Math.Max(first, second);
        int minimum = Math.Min(first, second);
        return maximum - minimum < 0x800 ? angle > maximum || angle < minimum : angle < maximum && angle > minimum;
    }

    internal static (short Position, byte Fraction) Integrate(short position, byte fraction, short velocity)
    {
        ushort accumulated = unchecked((ushort)(fraction + velocity));
        return (unchecked((short)(position + unchecked((sbyte)(accumulated >> 8)))), (byte)accumulated);
    }
}
