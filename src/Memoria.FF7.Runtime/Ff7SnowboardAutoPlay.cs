using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal static class Ff7SnowboardAutoPlay
{
    internal static ushort ChooseInput(Ff7SnowboardRuntime snowboard)
    {
        if (snowboard.Frame.Phase != Ff7SnowboardPhase.Playing || snowboard.Paused)
        {
            return 0x1000;
        }
        Ff7SnowboardRiderMotionState rider = snowboard.Simulation.Rider.Motion;
        Vector3 target = ReadTarget(snowboard.NativeData, snowboard.Course, snowboard.Segment, snowboard.Simulation.Navigation.CurrentTriangle);
        Vector3 delta = target - rider.Position;
        return ChooseDirection(new(delta.X, delta.Z), new(rider.Velocity.X, rider.Velocity.Z), snowboard.Simulation.Rider.Heading, rider.Speed, snowboard.Simulation.Rider.Script.ActionWord & 255);
    }

    internal static Vector3 ReadTarget(Ff7SnowboardNativeData native, int course, int segment, int triangle)
    {
        if ((uint)triangle >= Ff7SnowboardTerrain.TriangleCount)
        {
            throw new InvalidDataException("Snowboard input advice requires its actual admitted collision triangle.");
        }
        var selected = native.Courses[course];
        int tile = selected.Tiles[segment] * Ff7SnowboardTerrain.TileSize;
        int face = tile + 0x254 + triangle * 4;
        int row = Math.Max(native.TileGeometry[face] / 7, Math.Max(native.TileGeometry[face + 1] / 7, native.TileGeometry[face + 2] / 7));
        if (row is < 1 or > 6)
        {
            throw new NotSupportedException("The native snowboard triangle has no forward cross-section advice.");
        }
        int right = unchecked((sbyte)(selected.Connections >> 8));
        bool approachingFork = Word(tile) == 3 || segment + 1 < selected.Tiles.Count && Word(selected.Tiles[segment + 1] * Ff7SnowboardTerrain.TileSize) == 3;
        if (approachingFork && (uint)right < native.Courses.Count && right != course && (byte)(native.Courses[right].Descriptor >> 16) == 0)
        {
            selected = native.Courses[right];
            segment = 0;
            tile = selected.Tiles[0] * Ff7SnowboardTerrain.TileSize;
            row = 1;
        }
        int vertex = tile + 8 + (row * 7 + 3) * 8;
        short x = Word(vertex);
        short y = Word(vertex + 2);
        short z = Word(vertex + 4);
        right = unchecked((sbyte)(selected.Connections >> 8));
        if ((uint)right < native.Courses.Count && right != course && (byte)(native.Courses[right].Descriptor >> 16) == 0)
        {
            int edge = tile + 8 + (row * 7 + 6) * 8;
            x = (short)((x + Word(edge)) / 2);
            y = (short)((y + Word(edge + 2)) / 2);
            z = (short)((z + Word(edge + 4)) / 2);
        }
        (x, z) = Ff7SnowboardCourseNavigation.Rotate(x, z, selected.Angles[segment]);
        return new((selected.Positions[segment * 3] + x) * 4096f, (selected.Positions[segment * 3 + 1] + y) * 4096f, (selected.Positions[segment * 3 + 2] + z) * 4096f);

        short Word(int offset) => unchecked((short)(native.TileGeometry[offset] | native.TileGeometry[offset + 1] << 8));
    }

    internal static ushort ChooseDirection(Vector2 delta, Vector2 velocity, float heading, short speed, int action)
    {
        Vector2 forward = speed < 256 || velocity == Vector2.Zero ? new(-(float)Math.Sin(heading), -(float)Math.Cos(heading)) : velocity;
        double cross = (double)forward.X * delta.Y - (double)forward.Y * delta.X;
        double dot = (double)forward.X * delta.X + (double)forward.Y * delta.Y;
        if (speed > 256 && dot < Math.Abs(cross))
        {
            return 0x4000;
        }
        double error = Math.Atan2(cross, dot);
        double quantum = action == 2 ? (32 - (speed >> 4)) * 0.0015339808305725455f : speed <= 32 && action is 1 or 7 ? 0.04908738657832146f : 0.0015339808305725455f;
        quantum *= Ff7SnowboardRuntime.NativeSubstepsPerTick;
        double best = Math.Abs(error);
        ushort direction = 0;
        double left = Math.Abs(Wrap(error - quantum));
        if (left < best)
        {
            best = left;
            direction = 0x8000;
        }
        double right = Math.Abs(Wrap(error + quantum));
        if (right < best)
        {
            direction = 0x2000;
        }
        return direction == 0 ? (ushort)0x1000 : direction;

        static double Wrap(double angle) => Math.Atan2(Math.Sin(angle), Math.Cos(angle));
    }
}
