using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7ChocoboCameraSnapshot(Ff7ChocoboRacerState Tracker, int TrackedRacer, int TargetRacer, int TerrainHeight, Ff7ChocoboPoint TargetEye, Ff7ChocoboPoint TargetLook, Ff7ChocoboPoint CurrentEye, Ff7ChocoboPoint CurrentLook, bool Reset, bool FinishOrbit, int CullStart, int CullEnd, float[] StaticView, float[] AnimatedView, int[] LastQuarterDepth, bool[] Visible)
{
    public Ff7ChocoboPoint[]? DrawPositions { get; init; }
    public short[]? DrawYaws { get; init; }
    internal void Validate(int length)
    {
        if (Tracker is null || Tracker.Path < 0 || Tracker.Path >= length || Tracker.NextPath < 0 || Tracker.NextPath >= length || Tracker.Name is not { Length: 8 } || TrackedRacer is < 0 or > 5 || TargetRacer is < 0 or > 5 || StaticView is not { Length: 16 } || AnimatedView is not { Length: 16 } || StaticView.Any(value => !float.IsFinite(value)) || AnimatedView.Any(value => !float.IsFinite(value)) || LastQuarterDepth is not { Length: 6 } || Visible is not { Length: 6 })
        {
            throw new InvalidDataException("The FFVII Chocobo camera has invalid native state.");
        }
        if (DrawPositions is not null && DrawPositions.Length != 6 || DrawYaws is not null && DrawYaws.Length != 6)
        {
            throw new InvalidDataException("The FFVII Chocobo draw pose owners are corrupt.");
        }
    }
}

public sealed class Ff7ChocoboCamera
{
    private readonly Ff7ChocoboSimulation simulation;
    private readonly Ff7ChocoboNativeData native;
    private Ff7ChocoboRacerState tracker;
    private int trackedRacer;
    private int height;
    private Ff7ChocoboPoint targetEye;
    private Ff7ChocoboPoint targetLook;
    private bool reset = true;
    private bool finishOrbit;
    private readonly int[] lastQuarterDepth = new int[6];
    private readonly bool[] visible = new bool[6];
    private readonly Ff7ChocoboPoint[] drawPositions;
    private readonly short[] drawYaws;

    internal Ff7ChocoboCamera(Ff7ChocoboSimulation simulation, Ff7ChocoboNativeData native, Ff7ChocoboCameraSnapshot? saved = null, Ff7ChocoboCameraSnapshot? carry = null)
    {
        this.simulation = simulation;
        this.native = native;
        drawPositions = simulation.Racers.Select(racer => racer.Position).ToArray();
        drawYaws = simulation.Racers.Select(racer => racer.Yaw).ToArray();
        tracker = simulation.Racers[0] with { Name = simulation.Racers[0].Name.ToArray(), Lane = 127, Path = 0, NextPath = 1, Speed = 0 };
        tracker.Position = simulation.Sample(0, 127);
        tracker.Target = simulation.Sample(1, 127);
        tracker.TargetYaw = simulation.Direction(tracker.Target.X - tracker.Position.X, 0, tracker.Target.Z - tracker.Position.Z);
        StaticView = new(-1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0, 320, 240, 900, 1);
        if (carry is not null)
        {
            targetEye = carry.TargetEye;
            targetLook = carry.TargetLook;
            height = carry.TerrainHeight;
            StaticView = Matrix(carry.StaticView);
        }
        AnimatedView = StaticView;
        if (saved is not null)
        {
            saved.Validate(simulation.Course.Track.Count);
            tracker = saved.Tracker with { Name = saved.Tracker.Name.ToArray() };
            trackedRacer = saved.TrackedRacer;
            TargetRacer = saved.TargetRacer;
            height = saved.TerrainHeight;
            targetEye = saved.TargetEye;
            targetLook = saved.TargetLook;
            Eye = saved.CurrentEye;
            Look = saved.CurrentLook;
            reset = saved.Reset;
            finishOrbit = saved.FinishOrbit;
            CullStart = saved.CullStart;
            CullEnd = saved.CullEnd;
            StaticView = Matrix(saved.StaticView);
            AnimatedView = Matrix(saved.AnimatedView);
            saved.LastQuarterDepth.CopyTo(lastQuarterDepth, 0);
            saved.Visible.CopyTo(visible, 0);
            saved.DrawPositions?.CopyTo(drawPositions, 0);
            saved.DrawYaws?.CopyTo(drawYaws, 0);
        }
    }

    public Matrix4x4 StaticView { get; private set; }
    public Matrix4x4 AnimatedView { get; private set; }
    public int CullStart { get; private set; }
    public int CullEnd { get; private set; }
    public int TargetRacer { get; private set; }
    public Ff7ChocoboPoint Eye { get; private set; }
    public Ff7ChocoboPoint Look { get; private set; }
    public IReadOnlyList<bool> Visible => Array.AsReadOnly(visible);
    public IReadOnlyList<Ff7ChocoboPoint> DrawPositions => Array.AsReadOnly(drawPositions);
    public IReadOnlyList<short> DrawYaws => Array.AsReadOnly(drawYaws);
    internal event Action<int>? AnimationEvent;
    internal event Action<int>? SoundEvent;

    internal void NativeEvent()
    {
        finishOrbit = false;
    }

    internal Ff7ChocoboCameraSnapshot Capture()
    {
        return new(tracker with { Name = tracker.Name.ToArray() }, trackedRacer, TargetRacer, height, targetEye, targetLook, Eye, Look, reset, finishOrbit, CullStart, CullEnd, Values(StaticView), Values(AnimatedView), lastQuarterDepth.ToArray(), visible.ToArray()) { DrawPositions = drawPositions.ToArray(), DrawYaws = drawYaws.ToArray() };
    }

    internal int CollisionVolume(int index) => 127 - (lastQuarterDepth[index] >> 5);
    internal bool IsVisible(int index) => visible[index];

    internal void BeforeMotion(int frame)
    {
        PcChocoboCourse course = simulation.Course;
        if (trackedRacer != TargetRacer)
        {
            trackedRacer = TargetRacer;
            tracker = simulation.Racers[TargetRacer] with { Name = simulation.Racers[TargetRacer].Name.ToArray(), Lane = 127 };
            tracker.Position = simulation.Sample(tracker.Path, 127);
            tracker.Target = simulation.Sample(tracker.NextPath, 127);
            tracker.TargetYaw = simulation.Direction(tracker.Target.X - tracker.Position.X, 0, tracker.Target.Z - tracker.Position.Z);
        }
        if (simulation.Racers[0].Finished != 0)
        {
            TargetRacer = simulation.FirstFinisher;
            finishOrbit = true;
            reset = true;
        }
        Ff7ChocoboRacerState target = simulation.Racers[TargetRacer];
        if (finishOrbit)
        {
            CullStart = course.Track.Count - 20;
            CullEnd = 70;
            targetEye = (S(target.Position.X - native.Trigonometry.Cosine(frame * 16) / 6), S(target.Position.Y + 800), S(target.Position.Z - native.Trigonometry.Sine(frame * 16) / 6));
            targetLook = (target.Position.X, S(target.Position.Y + 200), target.Position.Z);
        }
        else
        {
            CullStart = (tracker.Path + course.Track.Count - 10) % course.Track.Count;
            CullEnd = (CullStart + course.Track[tracker.Path].CullDistance) % course.Track.Count;
            targetEye = (S((targetEye.X + tracker.Position.X - (native.Trigonometry.Sine(tracker.Yaw) >> 3)) / 2), S(height + 250), S((targetEye.Z + tracker.Position.Z - (-native.Trigonometry.Cosine(tracker.Yaw) >> 3)) / 2));
            targetLook = (S((target.Position.X * 7 + tracker.Position.X) / 8), S((targetLook.Y * 3 + target.Position.Y + 300) / 4), S((target.Position.Z * 7 + tracker.Position.Z) / 8));
        }
        if (reset)
        {
            Eye = targetEye;
            Look = targetLook;
            reset = false;
        }
        Eye = Filter(Eye, targetEye);
        Look = Filter(Look, targetLook);
        AnimatedView = StaticView;
        StaticView = View(Eye, Look);
    }

    internal void AfterMotion(bool started)
    {
        PcChocoboCourse course = simulation.Course;
        PcChocoboTrackElement next = course.Track[tracker.NextPath];
        if (Ff7ChocoboSimulation.Cross(next.Right.X - next.Left.X, 0, next.Right.Z - next.Left.Z, tracker.Position.X - next.Left.X, 0, tracker.Position.Z - next.Left.Z).Y >= 0)
        {
            tracker.Stagnation = 0;
            int path = 0;
            for (int probe = tracker.Path; probe < tracker.Path + 60; probe++)
            {
                path = (probe + course.Track.Count) % course.Track.Count;
                PcChocoboTrackElement section = course.Track[path];
                if (Ff7ChocoboSimulation.Cross(section.Right.X - section.Left.X, 0, section.Right.Z - section.Left.Z, tracker.Position.X - section.Left.X, 0, tracker.Position.Z - section.Left.Z).Y < 0)
                {
                    break;
                }
            }
            tracker.Path = tracker.NextPath;
            tracker.NextPath = S((path + course.Track.Count) % course.Track.Count);
            tracker.Target = simulation.Sample(tracker.NextPath, tracker.Lane);
            Ff7ChocoboPoint lookahead = simulation.Sample((tracker.NextPath + 1) % course.Track.Count, tracker.Lane);
            tracker.TargetYaw = simulation.Direction(lookahead.X - tracker.Position.X, 0, lookahead.Z - tracker.Position.Z);
            int animation = S(course.Track[tracker.Path].Right.Attributes);
            if (animation != 0)
            {
                AnimationEvent?.Invoke(animation);
            }
            int sound = S(course.Track[tracker.Path].Left.Attributes);
            if (sound != 0)
            {
                SoundEvent?.Invoke(sound - 1);
            }
        }
        if (tracker.Yaw - tracker.TargetYaw > 3000)
        {
            tracker.Yaw = S(tracker.Yaw - 4096);
        }
        if (tracker.Yaw - tracker.TargetYaw < -3000)
        {
            tracker.Yaw = S(tracker.Yaw + 4096);
        }
        tracker.Yaw = S((tracker.Yaw * 7 + tracker.TargetYaw) / 8);
        tracker.RemainderX = unchecked(tracker.RemainderX + native.Trigonometry.Sine(tracker.Yaw) * tracker.Speed / 64);
        tracker.RemainderZ = unchecked(tracker.RemainderZ - native.Trigonometry.Cosine(tracker.Yaw) * tracker.Speed / 64);
        tracker.Position = (S(tracker.Position.X + tracker.RemainderX / 4096), tracker.Position.Y, S(tracker.Position.Z + tracker.RemainderZ / 4096));
        tracker.RemainderX &= unchecked((int)0x80000FFF);
        tracker.RemainderZ &= unchecked((int)0x80000FFF);
        next = course.Track[tracker.NextPath];
        PcChocoboTrackElement current = course.Track[tracker.Path];
        var normal = Ff7ChocoboSimulation.Cross(next.Left.X - current.Left.X, next.Left.Y - current.Left.Y, next.Left.Z - current.Left.Z, next.Right.X - next.Left.X, next.Right.Y - next.Left.Y, next.Right.Z - next.Left.Z);
        int plane = tracker.Position.Y;
        if (normal.Y != 0)
        {
            Ff7ChocoboRacerState target = simulation.Racers[TargetRacer];
            PcChocoboVector anchor = course.Track[target.Path].Left;
            int targetHeight = unchecked((anchor.X - tracker.Position.X) * normal.X + (anchor.Z - tracker.Position.Z) * normal.Z + normal.Y * anchor.Y) / normal.Y;
            height = (height * 7 + targetHeight) / 8;
            plane = unchecked((current.Left.X - tracker.Position.X) * normal.X + (current.Left.Z - tracker.Position.Z) * normal.Z + normal.Y * current.Left.Y) / normal.Y;
        }
        tracker.PreviousY = S((tracker.PreviousY + plane) / 2);
        tracker.Position = (tracker.Position.X, tracker.PreviousY, tracker.Position.Z);
        if (started)
        {
            Ff7ChocoboRacerState target = simulation.Racers[TargetRacer];
            int distance = target.Path - tracker.Path;
            tracker.Speed = target.Speed;
            if (target.Finished == 0)
            {
                if (distance < 1)
                {
                    tracker.Speed = 0;
                }
                else if (distance > 2)
                {
                    tracker.Speed = S(tracker.Speed + distance * 150);
                }
            }
        }
    }

    internal void UpdateVisibility(int index)
    {
        Ff7ChocoboPoint position = simulation.Racers[index].Position;
        Vector3 camera = new(
            (float)((float)((double)position.X * StaticView.M11 + (double)position.Y * StaticView.M21 + (double)position.Z * StaticView.M31) + (double)StaticView.M41),
            (float)((float)((double)position.X * StaticView.M12 + (double)position.Y * StaticView.M22 + (double)position.Z * StaticView.M32) + (double)StaticView.M42),
            (float)((float)((double)position.X * StaticView.M13 + (double)position.Y * StaticView.M23 + (double)position.Z * StaticView.M33) + (double)StaticView.M43));
        int depth = (int)camera.Z / 4;
        int x = camera.Z != 0 ? S((int)((double)camera.X * 270 / camera.Z + 160)) : 0;
        int y = camera.Z != 0 ? S((int)((double)camera.Y * 270 / camera.Z + 120)) : 0;
        visible[index] = depth < 2200 && x > -20 && x < 350 && y > 0 && y < 320;
        if (visible[index])
        {
            lastQuarterDepth[index] = depth;
            drawPositions[index] = position;
            drawYaws[index] = simulation.Racers[index].Yaw;
        }
    }

    private Matrix4x4 View(Ff7ChocoboPoint eye, Ff7ChocoboPoint look)
    {
        int dx = eye.X - look.X;
        int dy = look.Y - eye.Y;
        int dz = look.Z - eye.Z;
        float pitch = (float)((float)Math.Atan2(dy, Math.Sqrt((float)unchecked(dx * dx + dz * dz))) * native.Trigonometry.DegreesPerRadian);
        float yaw = (float)((float)Math.Atan2(dx, dz) * native.Trigonometry.DegreesPerRadian);
        (float sx, float cx) = Trig(pitch);
        (float sy, float cy) = Trig(yaw);
        (float sz, float cz) = Trig(180);
        float[] rotationX = [1, 0, 0, 0, cx, sx, 0, -sx, cx];
        float[] rotationY = [cy, 0, -sy, 0, 1, 0, sy, 0, cy];
        float[] rotationZ = [cz, sz, 0, -sz, cz, 0, 0, 0, 1];
        float[] rotation = Multiply(Multiply(rotationY, rotationX), rotationZ);
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = S((int)((double)rotation[index] * 4096)) / 4096f;
        }
        int tx = -(int)(float)((double)eye.X * rotation[0] + (double)eye.Y * rotation[3] + (double)eye.Z * rotation[6]);
        int ty = -(int)(float)((double)eye.X * rotation[1] + (double)eye.Y * rotation[4] + (double)eye.Z * rotation[7]);
        int tz = -(int)(float)((double)eye.X * rotation[2] + (double)eye.Y * rotation[5] + (double)eye.Z * rotation[8]);
        return new(rotation[0], rotation[1], rotation[2], 0, rotation[3], rotation[4], rotation[5], 0, rotation[6], rotation[7], rotation[8], 0, tx, ty, tz, 1);
    }

    private (float Sine, float Cosine) Trig(float degrees)
    {
        double radians = degrees * native.Trigonometry.RadiansPerDegree;
        return ((float)Math.Sin((float)radians), (float)Math.Cos(radians));
    }

    private static float[] Multiply(float[] first, float[] second)
    {
        float[] result = new float[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                result[row * 3 + column] = (float)((double)first[row * 3] * second[column] + (double)first[row * 3 + 1] * second[3 + column] + (double)first[row * 3 + 2] * second[6 + column]);
            }
        }
        return result;
    }

    private static Ff7ChocoboPoint Filter(Ff7ChocoboPoint current, Ff7ChocoboPoint target) => (S((current.X * 3 + target.X) / 4), S((current.Y * 7 + target.Y) / 8), S((current.Z * 3 + target.Z) / 4));
    private static short S(int value) => unchecked((short)value);
    private static float[] Values(Matrix4x4 value) => [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];
    private static Matrix4x4 Matrix(float[] value) => new(value[0], value[1], value[2], value[3], value[4], value[5], value[6], value[7], value[8], value[9], value[10], value[11], value[12], value[13], value[14], value[15]);
}
