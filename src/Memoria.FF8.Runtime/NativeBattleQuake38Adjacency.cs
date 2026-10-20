using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38Adjacency
{
    public static ImmutableArray<byte> Classify8D95E0(ImmutableArray<byte> entries, ImmutableArray<byte> vertexZones, short primitiveCount, short waveTick, short primitive, int edge, int first, int second, int centroid)
    {
        if (entries.IsDefault || entries.Length != 0xB600 || vertexZones.IsDefault || vertexZones.Length != 0xCC0 || (uint)edge >= 4)
        {
            throw new InvalidDataException("Native Quake adjacency requires its exact split-entry and vertex-zone banks.");
        }
        if (waveTick != primitive % 16)
        {
            return entries;
        }
        byte[] bytes = entries.ToArray();
        Classify(second, first, 0);
        Classify(centroid, second, 1);
        Classify(first, centroid, 2);
        return bytes.ToImmutableArray();

        void Classify(int left, int right, int side)
        {
            int current = Entry(primitive);
            short a = Word(current + 52 + left * 2);
            short b = Word(current + 52 + right * 2);
            if (ZoneCount(a) <= 1 || ZoneCount(b) <= 1 || (bytes[current + 96 + edge] & (1 << side)) != 0)
            {
                return;
            }
            if (a > b)
            {
                (a, b) = (b, a);
            }
            byte zone = bytes[current + 88 + edge];
            if (bytes[current + 110] == 1 && b == 3 || bytes[current + 110] == 2 && b == 4)
            {
                Compare(primitive, zone, a, b, side);
                return;
            }
            // The native scan uses0..count-1 although split entries are built from1.
            for (short candidate = 0; candidate < primitiveCount; candidate++)
            {
                if (Compare(candidate, zone, a, b, side))
                {
                    break;
                }
            }
        }
        bool Compare(short candidate, byte zone, short low, short high, int side)
        {
            int current = Entry(primitive);
            int other = Entry(candidate);
            int count = bytes[other + 110] == 1 ? 3 : bytes[other + 110] == 2 ? 4 : 0;
            bool found = false;
            for (int otherEdge = 0; otherEdge < count; otherEdge++)
            {
                if (bytes[other + 88 + otherEdge] == zone)
                {
                    continue;
                }
                int[] perimeter = count == 3 ? [0, 1, 2] : [0, 1, 3, 2];
                int a = perimeter[otherEdge];
                int b = perimeter[(otherEdge + 1) % count];
                Match(a, b, 1);
                Match(b, count, 2);
                Match(count, a, 4);
                void Match(int left, int right, int bit)
                {
                    short x = Word(other + 52 + left * 2);
                    short y = Word(other + 52 + right * 2);
                    if (x > y)
                    {
                        (x, y) = (y, x);
                    }
                    if (x == low && y == high)
                    {
                        found = true;
                        bytes[current + 96 + edge] |= (byte)(1 << side);
                        bytes[other + 96 + otherEdge] |= (byte)bit;
                    }
                }
            }
            return found;
        }
        int Entry(short id)
        {
            int offset = checked(id * 112);
            if (offset < 0 || offset + 112 > bytes.Length)
            {
                throw new InvalidDataException("Native Quake adjacency leaves its original entry bank.");
            }
            return offset;
        }
        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
        short ZoneCount(short index)
        {
            int offset = checked(index * 4 + 2);
            if (offset < 0 || offset + 2 > vertexZones.Length)
            {
                throw new InvalidDataException("Native Quake adjacency leaves its vertex-zone count bank.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(vertexZones.AsSpan().Slice(offset));
        }
    }
}
