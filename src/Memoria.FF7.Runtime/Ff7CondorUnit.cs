using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7CondorUnitState(int Slot, byte Type, short X, short Y, byte FractionX, byte FractionY, int Path, int PathIndex, short TargetX, short TargetY, short VelocityX, short VelocityY, ushort Timer, ushort Interval, int Cell, int State, ushort Tint);

internal static class Ff7CondorUnit
{
    internal static Ff7CondorUnitState Spawn(Ff7CondorResources resources, Ff7CondorEnemyWave row, int slot, ushort tint)
    {
        Ff7CondorUnitType type = resources.ReadUnitType(row.RowType);
        if (row.RowType is < 16 or > 19 || row.Extra > 3 || type.Behavior != 7 || type.Interval == 0 || slot is < 20 or >= 40)
        {
            throw new NotSupportedException("The FFVII Condor enemy requires its native unit behavior.");
        }
        short x = checked((short)(row.X * 16));
        short y = checked((short)(1024 - row.Y * 16));
        Ff7CondorUnitState unit = new(slot, row.RowType, x, y, 0, 0, row.Path, 0, 0, 0, 0, 0, checked((ushort)(type.Interval + slot + 1)), type.Interval, -1, 0, tint);
        unit = NextWaypoint(resources, unit);
        if (unit.TargetX == x && unit.TargetY == y)
        {
            unit = NextWaypoint(resources, unit);
        }
        return unit with { Cell = FindCell(resources, unit) };
    }

    internal static Ff7CondorUnitState Advance(Ff7CondorResources resources, Ff7CondorUnitState unit)
    {
        ushort timer = unchecked((ushort)(unit.Timer - 2));
        if (timer != 0 && timer < 0xFF00)
        {
            return unit with { Timer = timer };
        }
        unit = unit with { Timer = unit.Interval, State = 1 };
        if ((unit.TargetX != 0 || unit.TargetY != 0) && (unit.VelocityX != 0 || unit.VelocityY != 0))
        {
            (short x, byte fractionX) = Ff7CondorGeometry.Integrate(unit.X, unit.FractionX, unit.VelocityX);
            (short y, byte fractionY) = Ff7CondorGeometry.Integrate(unit.Y, unit.FractionY, unit.VelocityY);
            unit = unit with { X = x, Y = y, FractionX = fractionX, FractionY = fractionY };
            int cell = FindCell(resources, unit);
            if (cell < 0)
            {
                throw new NotSupportedException("The FFVII Condor unit left its admitted native terrain cells.");
            }
            unit = unit with { Cell = cell };
            short vx = unit.X == unit.TargetX ? (short)0 : unit.VelocityX;
            short vy = unit.Y == unit.TargetY ? (short)0 : unit.VelocityY;
            unit = unit with { VelocityX = vx, VelocityY = vy };
            if (vx == 0 && vy == 0)
            {
                unit = NextWaypoint(resources, unit with { TargetX = 0, TargetY = 0 });
            }
        }
        return unit;
    }

    internal static bool ReachedShed(Ff7CondorUnitState unit)
    {
        return unit.Cell is >= 204 and <= 207 || unit.X - 256 == 48 && unit.Y - 512 == -96;
    }

    private static Ff7CondorUnitState NextWaypoint(Ff7CondorResources resources, Ff7CondorUnitState unit)
    {
        IReadOnlyList<Ff7CondorPathPoint> path = resources.ReadPath(unit.Path);
        if (unit.PathIndex == path.Count)
        {
            return unit with { VelocityX = 0, VelocityY = 0 };
        }
        if ((uint)unit.PathIndex > path.Count)
        {
            throw new InvalidDataException("The FFVII Condor path cursor lies outside its native resource.");
        }
        Ff7CondorPathPoint point = path[unit.PathIndex];
        short x = checked((short)(point.X * 16));
        short y = checked((short)(1024 - point.Y * 16));
        (short vx, short vy) = Ff7CondorGeometry.Normalize(unit.X, unit.Y, x, y);
        return unit with { TargetX = x, TargetY = y, VelocityX = vx, VelocityY = vy, FractionX = 0, FractionY = 0, PathIndex = unit.PathIndex + 1 };
    }

    private static int FindCell(Ff7CondorResources resources, Ff7CondorUnitState unit)
    {
        short x = unchecked((short)(unit.X - 256));
        short y = unchecked((short)(unit.Y - 512));
        if ((uint)unit.Cell < resources.TerrainCells.Count && Ff7CondorGeometry.Contains(resources.TerrainCells[unit.Cell].Span, x, y))
        {
            return unit.Cell;
        }
        for (int index = 0; index < resources.TerrainCells.Count; index++)
        {
            if (Ff7CondorGeometry.Contains(resources.TerrainCells[index].Span, x, y))
            {
                return index;
            }
        }
        return -1;
    }
}
