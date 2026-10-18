using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleGilgameshSwordClipVertex(NativeBattleVector Position, ushort Uv);

/// <summary>58F650/595B40's signed low-DWORD intersections and asymmetric six-mask dispatch.</summary>
public static class NativeBattleGilgameshSwordClip
{
    private static readonly int[][] Rows = [[0, 1, 2, 0, 0, 1, 2, 0], [0, 3, 2, 3, 3, 1, 2, 0], [0, 1, 4, 4, 0, 4, 2, 0], [3, 1, 4, 4, 0, 3, 2, 4], [5, 1, 2, 5, 0, 1, 5, 0], [0, 3, 5, 3, 5, 3, 2, 1], [5, 4, 2, 5, 0, 1, 5, 4]];

    public static NativeBattleGilgameshSwordClipVertex Intersection58F650(NativeBattleGilgameshSwordClipVertex a, NativeBattleGilgameshSwordClipVertex b, NativeBattleVector normal, short planeD)
    {
        int dx = b.Position.X - a.Position.X;
        int dy = b.Position.Y - a.Position.Y;
        int dz = b.Position.Z - a.Position.Z;
        int denominator = unchecked(normal.X * dx + normal.Y * dy + normal.Z * dz) >> 12;
        if (denominator == 0)
        {
            denominator = 1;
        }
        int numerator = unchecked((planeD << 12) + normal.X * a.Position.X + normal.Y * a.Position.Y + normal.Z * a.Position.Z);
        int t = unchecked(-(numerator / denominator));
        NativeBattleVector point = new(unchecked((short)(a.Position.X + (unchecked(t * dx) >> 12))), unchecked((short)(a.Position.Y + (unchecked(t * dy) >> 12))), unchecked((short)(a.Position.Z + (unchecked(t * dz) >> 12))));
        int ua = a.Uv & 0xFF;
        int ub = b.Uv & 0xFF;
        ushort u = unchecked((ushort)(ua + (unchecked(t * (ub - ua)) >> 12)));
        int va = a.Uv & 0xFF00;
        int vb = b.Uv & 0xFF00;
        int v = unchecked(va + (unchecked(t * (vb - va)) >> 12)) & 0xFF00;
        return new(point, unchecked((ushort)(u | v)));
    }

    public static int Distance(NativeBattleGilgameshSwordClipVertex point) => Math.Clamp(-(int)point.Position.Y, short.MinValue, short.MaxValue);

    public static ImmutableArray<NativeBattleGilgameshSwordClipVertex> Clip595B40(NativeBattleGilgameshSwordClipVertex first, NativeBattleGilgameshSwordClipVertex second, NativeBattleGilgameshSwordClipVertex third)
    {
        int[] distances = [Distance(first), Distance(second), Distance(third)];
        int mask = Edge(distances[0], distances[1], 1) | Edge(distances[1], distances[2], 2) | Edge(distances[2], distances[0], 4);
        NativeBattleGilgameshSwordClipVertex[] points = [first, second, third, default, default, default];
        NativeBattleVector normal = new(0, -4096, 0);
        if ((mask & 1) != 0)
        {
            points[3] = Intersection58F650(first, second, normal, 0);
        }
        if ((mask & 2) != 0)
        {
            points[4] = Intersection58F650(second, third, normal, 0);
        }
        if ((mask & 4) != 0)
        {
            points[5] = Intersection58F650(third, first, normal, 0);
        }
        return Select595B40(mask, points.ToImmutableArray(), distances.ToImmutableArray());
    }

    public static ImmutableArray<NativeBattleGilgameshSwordClipVertex> Select595B40(int mask, ImmutableArray<NativeBattleGilgameshSwordClipVertex> points, ImmutableArray<int> distances)
    {
        if ((uint)mask > 7 || points.IsDefault || points.Length != 6 || distances.IsDefault || distances.Length != 3)
        {
            throw new InvalidDataException("Native595B40 requires its six physical points, three signed distances and actual edge mask.");
        }
        if (mask == 0)
        {
            return (distances[0] | distances[1] | distances[2]) > 0 ? points.Slice(0, 3) : [];
        }
        if (mask == 7)
        {
            return [];
        }
        int[] row = Rows[mask];
        bool first = distances[row[3] - 3] > 0;
        int start = first ? 0 : 4;
        int count = !first && mask is 3 or 5 or 6 ? 4 : 3;
        return Enumerable.Range(start, count).Select(index => points[row[index]]).ToImmutableArray();
    }

    private static int Edge(int first, int second, int flag) => first != 0 && (first ^ second) < 0 ? flag : 0;
}
