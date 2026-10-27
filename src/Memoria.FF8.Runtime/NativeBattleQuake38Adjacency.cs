namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38Adjacency
{
    public static NativeBattleEffectArena Classify8D95E0(NativeBattleEffectArena arena, int entriesOffset, int zonesOffset, short primitiveCount, short waveTick, short primitive, int edge, int first, int second, int centroid)
    {
        if (entriesOffset < 0 || entriesOffset > NativeBattleEffectArena.Length - 0xB600 || zonesOffset < 0 || zonesOffset > NativeBattleEffectArena.Length - 0xCC0 || (uint)edge >= 4 || (uint)first >= 5 || (uint)second >= 5 || (uint)centroid >= 5)
        {
            throw new InvalidDataException("Native Quake adjacency requires its exact split-entry and vertex-zone banks.");
        }
        if (waveTick != primitive % 16)
        {
            return arena;
        }
        // Three sides, four other edges, three matches per edge, two byte stores per match.
        Span<NativeBattleQuake38MemoryWrite> writes = stackalloc NativeBattleQuake38MemoryWrite[3 * 4 * 3 * 2];
        Classification classification = new(arena, writes, entriesOffset, zonesOffset, primitiveCount, primitive, edge);
        return classification.Run(first, second, centroid);
    }

    private ref struct Classification(NativeBattleEffectArena arena, Span<NativeBattleQuake38MemoryWrite> writes, int entriesOffset, int zonesOffset, short primitiveCount, short primitive, int edge)
    {
        private NativeBattleQuake38MemoryPlan plan = new(arena, writes);
        private readonly int entriesOffset = entriesOffset;
        private readonly int zonesOffset = zonesOffset;
        private readonly short primitiveCount = primitiveCount;
        private readonly short primitive = primitive;
        private readonly int edge = edge;

        public NativeBattleEffectArena Run(int first, int second, int centroid)
        {
            Classify(second, first, 0);
            Classify(centroid, second, 1);
            Classify(first, centroid, 2);
            return plan.Commit();
        }

        private void Classify(int left, int right, int side)
        {
            int current = Entry(primitive);
            short a = plan.ReadInt16(current + 52 + left * 2);
            short b = plan.ReadInt16(current + 52 + right * 2);
            if (ZoneCount(a) <= 1 || ZoneCount(b) <= 1 || (plan.ReadByte(current + 96 + edge) & (1 << side)) != 0)
            {
                return;
            }
            if (a > b)
            {
                (a, b) = (b, a);
            }
            byte zone = plan.ReadByte(current + 88 + edge);
            if (plan.ReadByte(current + 110) == 1 && b == 3 || plan.ReadByte(current + 110) == 2 && b == 4)
            {
                Compare(primitive, zone, a, b, side);
                return;
            }
            // The native scan uses 0..count-1 although split entries are built from 1.
            for (short candidate = 0; candidate < primitiveCount; candidate++)
            {
                if (Compare(candidate, zone, a, b, side))
                {
                    break;
                }
            }
        }

        private bool Compare(short candidate, byte zone, short low, short high, int side)
        {
            int current = Entry(primitive);
            int other = Entry(candidate);
            byte type = plan.ReadByte(other + 110);
            int count = type == 1 ? 3 : type == 2 ? 4 : 0;
            bool found = false;
            ReadOnlySpan<int> perimeter = count == 3 ? [0, 1, 2] : [0, 1, 3, 2];
            for (int otherEdge = 0; otherEdge < count; otherEdge++)
            {
                if (plan.ReadByte(other + 88 + otherEdge) == zone)
                {
                    continue;
                }
                int a = perimeter[otherEdge];
                int b = perimeter[(otherEdge + 1) % count];
                found |= Match(current, other, otherEdge, a, b, low, high, side, 1);
                found |= Match(current, other, otherEdge, b, count, low, high, side, 2);
                found |= Match(current, other, otherEdge, count, a, low, high, side, 4);
            }
            return found;
        }

        private bool Match(int current, int other, int otherEdge, int left, int right, short low, short high, int side, int bit)
        {
            short x = plan.ReadInt16(other + 52 + left * 2);
            short y = plan.ReadInt16(other + 52 + right * 2);
            if (x > y)
            {
                (x, y) = (y, x);
            }
            if (x != low || y != high)
            {
                return false;
            }
            plan.WriteByte(current + 96 + edge, (byte)(plan.ReadByte(current + 96 + edge) | 1 << side));
            plan.WriteByte(other + 96 + otherEdge, (byte)(plan.ReadByte(other + 96 + otherEdge) | bit));
            return true;
        }

        private int Entry(short id)
        {
            int offset = checked(id * 112);
            if (offset < 0 || offset + 112 > 0xB600)
            {
                throw new InvalidDataException("Native Quake adjacency leaves its original entry bank.");
            }
            return entriesOffset + offset;
        }

        private short ZoneCount(short index)
        {
            int offset = checked(index * 4 + 2);
            if (offset < 0 || offset + 2 > 0xCC0)
            {
                throw new InvalidDataException("Native Quake adjacency leaves its vertex-zone count bank.");
            }
            return plan.ReadInt16(zonesOffset + offset);
        }
    }
}
