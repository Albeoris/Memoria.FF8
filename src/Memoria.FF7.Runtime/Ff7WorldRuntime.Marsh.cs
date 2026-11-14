using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private static Ff7WorldMarsh CopyMarshState(Ff7WorldMarsh state)
    {
        Ff7WorldMarshPoint[]? points = state.Points?.ToArray();
        if (points is null && state.Initialized)
        {
            points = Enumerable.Repeat(new Ff7WorldMarshPoint(state.X, state.Z), 48).ToArray();
        }
        return state with { Points = points, TerrainCache = state.TerrainCache?.ToArray() };
    }

    private static void ValidateMarshState(Ff7WorldMarsh state)
    {
        if (state.Points is { } points && (points.Length != 48 || points.Any(point => point is null || point.X is < 0 or > 65535 || point.Z is < 0 or > 65535 || point.Direction is < 0 or > 65535 || point.Height is < 0 or > 65535)) || state.Head is < 0 or >= 48 || state.TurnTimer is < short.MinValue or > short.MaxValue || state.TurnDelta is < short.MinValue or > short.MaxValue || state.PreviousTerrain is < 0 or > 31 || state.PreviousModel is < 0 or > 255 || state.TerrainCache is { } cache && (cache.Length > 6 || cache.Any(entry => entry is null || entry.ChunkX is < 0 or >= 36 || entry.ChunkZ is < 0 or >= 28 || entry.Triangle < 0)))
        {
            throw new InvalidDataException("FFVII world marsh simulation state is corrupt.");
        }
    }

    private void ResetWorldMarsh()
    {
        if (marsh.Initialized)
        {
            return;
        }
        int z = controlledModel < 0 ? 0 : RequireEntity(controlledModel).Z;
        int x = z < 0x23A98 ? 0x2008 : 0x1D4C;
        int localZ = z < 0x23A98 ? 0x6338 : 0x11F8;
        marsh = marsh with { Initialized = true, X = x, Z = localZ, Direction = 1024, Points = Enumerable.Repeat(new Ff7WorldMarshPoint(x, localZ), 48).ToArray(), Head = 0 };
    }

    private void AdvanceWorldMarsh()
    {
        if (Resources.World != 0 || controlledModel < 0)
        {
            return;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        if (marsh.Initialized && player.X is >= 0x30000 and < 0x40000 && player.Z is >= 0x1C000 and < 0x2C000)
        {
            marsh = CopyMarshState(marsh);
            Ff7WorldMarshPoint[] points = marsh.Points!;
            Ff7WorldMarshPoint current = points[marsh.Head];
            Ff7WorldInputState savedInput = input;
            (int Height, int Terrain)? terrain;
            try
            {
                input = input with { TerrainCache = marsh.TerrainCache };
                terrain = QueryTerrain(current.X + 0x34000, current.Z + 0x20000, unchecked((short)current.Height), 7, 100);
                marsh = marsh with { TerrainCache = input.TerrainCache?.ToArray() };
            }
            finally
            {
                input = savedInput;
            }
            int direction = marsh.Direction;
            if (terrain is { } found)
            {
                current = current with { Height = unchecked((ushort)found.Height) };
            }
            else
            {
                current = current with { X = (current.X * 63 + 10000) >> 6, Z = (current.Z * 63 + 15000) >> 6, Height = 0 };
                direction = unchecked((short)(current.Direction + 2048));
            }
            points[marsh.Head] = current;
            bool vulnerable = player.Model is 0 or 1 or 2 || IsWorldChocobo(player.Model);
            int distance = Math.Abs(player.X - current.X - 0x34000) + Math.Abs(player.Z - current.Z - 0x20000);
            if (distance <= 200 && vulnerable)
            {
                Machine.StartSystem(7);
                marsh = marsh with { Initialized = false, Points = points };
            }
            else
            {
                AdvanceMarshHead(player, points, current, direction, vulnerable);
            }
        }
        int currentTerrain = player.Terrain & 31;
        if (player.Model != marsh.PreviousModel)
        {
            currentTerrain = marsh.PreviousTerrain;
        }
        bool inMarsh = marsh.InMarsh;
        if (currentTerrain == 7 && marsh.PreviousTerrain == 0)
        {
            inMarsh = player.X > 0x36000;
        }
        marsh = marsh with { PreviousTerrain = currentTerrain, PreviousModel = player.Model, InMarsh = inMarsh };
    }

    private void AdvanceMarshHead(Ff7WorldEntity player, Ff7WorldMarshPoint[] points, Ff7WorldMarshPoint current, int direction, bool vulnerable)
    {
        Ff7WorldMarshPoint Step(int angle)
        {
            Vector3 displacement = inputMath.TransformRotation(new(0, 0, 100), 0, unchecked((short)angle), 0);
            return new(unchecked((ushort)(current.X + (int)displacement.X)), unchecked((ushort)(current.Z + (int)displacement.Z)), unchecked((ushort)angle), points[(marsh.Head + 1) % 48].Height);
        }
        Ff7WorldMarshPoint next = Step(direction);
        int timer = marsh.TurnTimer;
        int turn = marsh.TurnDelta;
        if (vulnerable && (player.Terrain & 31) == 7)
        {
            int x = player.X - 0x34000;
            int z = player.Z - 0x20000;
            int distance = unchecked((short)(Math.Abs(next.X - x) + Math.Abs(next.Z - z)));
            int angle = distance > 300 ? unchecked((short)(300000 / distance)) : 0;
            Ff7WorldMarshPoint left = Step(direction + angle);
            if (unchecked((short)(Math.Abs(current.X + WrappedDifference(left.X - current.X, 65536) - x) + Math.Abs(current.Z + WrappedDifference(left.Z - current.Z, 65536) - z))) < distance)
            {
                next = left;
            }
            Ff7WorldMarshPoint right = Step(direction - angle);
            if (unchecked((short)(Math.Abs(current.X + WrappedDifference(right.X - current.X, 65536) - x) + Math.Abs(current.Z + WrappedDifference(right.Z - current.Z, 65536) - z))) < distance)
            {
                next = right;
            }
            direction = unchecked((short)next.Direction);
        }
        else if (timer-- <= 0)
        {
            timer = (WorldRandom.Next() + 64) >> 2;
            turn = (WorldRandom.Next() - 128) >> 2;
        }
        int head = (marsh.Head + 1) % 48;
        points[head] = next;
        direction = unchecked((short)(direction + turn));
        if (direction < 0)
        {
            direction += 4096;
        }
        else if (direction >= 4096)
        {
            direction -= 4096;
        }
        marsh = marsh with { X = next.X, Z = next.Z, Direction = direction, Head = head, Points = points, TurnTimer = unchecked((short)timer), TurnDelta = unchecked((short)turn) };
    }
}
