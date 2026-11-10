using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleCameraOutputInputs(ushort Location, ushort Options, sbyte BattlePhase, short MenuPhase, sbyte PrimaryQueueState);
public readonly record struct Ff7BattleCameraOutput(Ff7BattleCameraScriptState State, SceneCameraPlacement Camera, ushort Options);

public static class Ff7NativeBattleCameraOutput
{
    public static Ff7BattleCameraOutput Select(Ff7BattleCameraScriptState state, Ff7BattleCameraOutputInputs input, IReadOnlyList<SceneCameraPlacement> idle, SceneCameraPlacement displayed)
    {
        if (state.Tracks.Length != 8 || idle.Count != 3 || state.IdleCamera >= 3 || (uint)state.SelectedTrack >= 4)
        {
            throw new InvalidDataException("FFVII camera output requires native track rows, idle placements and a selected track.");
        }
        ushort options = input.Location == 78 ? (ushort)(input.Options & ~0x300) : input.Options;
        bool fixedCamera = (options & 0x300) == 0x100;
        if (input.BattlePhase == 100)
        {
            return fixedCamera ? DirectIdle() : Selected();
        }
        if (input.MenuPhase == 0 && state.ActiveAction == 0)
        {
            return SmoothIdle();
        }
        if (state.CameraId == -1)
        {
            return fixedCamera ? DirectIdle() : SmoothIdle();
        }
        if (state.CameraId == -2)
        {
            return Selected();
        }
        if (fixedCamera)
        {
            if (state.CameraId is 59 or 60 or 69 or 89 or 109 or 119 or 203)
            {
                return input.PrimaryQueueState == 1 ? SmoothIdle() : Selected();
            }
            return DirectIdle();
        }
        return input.PrimaryQueueState == 1 && input.Location != 78 ? SmoothIdle() : Selected();

        Ff7BattleCameraOutput DirectIdle() => new(state, idle[state.IdleCamera], options);

        Ff7BattleCameraOutput Selected()
        {
            SceneCameraPlacement camera = new(state.Tracks[state.SelectedTrack].Point.ToVector(), state.Tracks[state.SelectedTrack + 4].Point.ToVector());
            return new(state with { ReturningToIdle = true }, camera, options);
        }

        Ff7BattleCameraOutput SmoothIdle()
        {
            Ff7BattleCameraTrack[] tracks = (Ff7BattleCameraTrack[])state.Tracks.Clone();
            bool changing = false;
            Ff7BattleCameraPoint position = Smooth(Point(displayed.Position), Point(idle[state.IdleCamera].Position));
            Ff7BattleCameraPoint focus = Smooth(Point(displayed.Focus), Point(idle[state.IdleCamera].Focus));
            tracks[3] = tracks[3] with { Point = position };
            tracks[7] = tracks[7] with { Point = focus };
            return new(state with { Tracks = tracks, ReturningToIdle = state.ReturningToIdle && changing }, new(position.ToVector(), focus.ToVector()), options);

            Ff7BattleCameraPoint Smooth(Ff7BattleCameraPoint current, Ff7BattleCameraPoint target)
            {
                if (!state.ReturningToIdle)
                {
                    return current;
                }
                int x = (target.X - current.X) / 2;
                int y = (target.Y - current.Y) / 2;
                int z = (target.Z - current.Z) / 2;
                changing |= x != 0 || y != 0 || z != 0;
                return new(unchecked((short)(current.X + x)), unchecked((short)(current.Y + y)), unchecked((short)(current.Z + z)));
            }
        }
    }

    private static Ff7BattleCameraPoint Point(System.Numerics.Vector3 point) => new(checked((short)point.X), checked((short)point.Y), checked((short)point.Z));
}
