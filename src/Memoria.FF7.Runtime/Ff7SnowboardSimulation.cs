using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

internal sealed class Ff7SnowboardSimulation
{
    private readonly Ff7SnowboardNativeData native;
    private readonly Ff7SnowboardCollisionResponse collisionResponse;
    private Vector3 movementNormal;
    private int previousLogicalDistance;
    private bool hasNormalBasis;

    internal Ff7SnowboardSimulation(Ff7SnowboardNativeData native, uint seed)
    {
        this.native = native;
        Navigation = new(native);
        collisionResponse = new(native);
        Vector3 position = Ff7SnowboardSurface.InitialPosition(native);
        Ff7SnowboardCourseReconciliation initial = Navigation.Reconcile(position, position);
        if (!initial.Accepted || initial.CourseEnded)
        {
            throw new InvalidDataException("The native snowboard initial placement is outside its initial course surface.");
        }
        Rider = new(native, initial.PositionQ12, seed);
        Surface = Ff7SnowboardSurface.Sample(native, Navigation);
        Rider.Motion.FloorHeight = Surface.FloorHeightQ12;
        LogicalDistance = Navigation.LogicalDistance;
    }

    internal Ff7SnowboardCourseNavigation Navigation { get; }
    internal Ff7SnowboardRiderController Rider { get; }
    internal Ff7SnowboardSurfaceSample Surface { get; private set; }
    internal bool CourseEnded { get; private set; }
    internal int LogicalDistance { get; private set; }
    internal Vector3 PreviousPosition { get; private set; }
    internal int PreviousSegment { get; private set; }
    internal int PreviousTriangle { get; private set; }
    internal event Action<Ff7SnowboardCollisionSound>? Sound;

    internal IReadOnlyList<Ff7SnowboardActionEffect> AdvanceIntro(ushort held, ushort pressed, long tick)
    {
        PrepareIntro();
        return AdvanceIntroRider(held, pressed, tick);
    }

    internal void PrepareIntro()
    {
        Vector3 position = Rider.Motion.Position;
        Ff7SnowboardCourseReconciliation result = Navigation.Reconcile(position, position);
        if (!result.Accepted || result.CourseEnded)
        {
            throw new InvalidDataException("The native snowboard intro lost its stationary course placement.");
        }
        Rider.Motion.Position = result.PositionQ12;
        Surface = Ff7SnowboardSurface.Sample(native, Navigation);
    }

    internal IReadOnlyList<Ff7SnowboardActionEffect> AdvanceIntroRider(ushort held, ushort pressed, long tick)
    {
        return Rider.Advance(held, pressed, tick, Navigation.SurfaceFlag, Course.Attributes[Navigation.CurrentSegment], Surface.CourseDirection);
    }

    internal void AdvanceMotion(bool ending)
    {
        Ff7SnowboardRiderMotionState motion = Rider.Motion;
        Vector3 previous = motion.Position;
        PreviousPosition = previous;
        PreviousSegment = Navigation.CurrentSegment;
        PreviousTriangle = Navigation.CurrentTriangle;
        previousLogicalDistance = LogicalDistance;
        Vector3 normal = hasNormalBasis ? Surface.Normal : Vector3.Zero;
        movementNormal = normal;
        motion.Force = normal;
        Ff7SnowboardRiderMotion.AdvanceHorizontal(motion, Navigation.SurfaceFlag);
        if (!CourseEnded)
        {
            Ff7SnowboardCourseReconciliation result = Navigation.Reconcile(motion.Position, previous);
            motion.Position = result.PositionQ12;
            if (result.Collision is { } collision)
            {
                Ff7SnowboardCollisionResult response = collisionResponse.Apply(Navigation, collision, motion.Velocity, unchecked((short)Rider.Script.ActionWord), motion.Speed, Surface.CourseDirection);
                motion.Velocity = response.Velocity;
                if (response.Action != unchecked((short)Rider.Script.ActionWord))
                {
                    Rider.SetAction(unchecked((ushort)response.Action));
                }
                if (response.Sound is { } sound)
                {
                    Sound?.Invoke(sound);
                }
            }
            CourseEnded = result.CourseEnded;
            if (!CourseEnded)
            {
                LogicalDistance = Navigation.LogicalDistance;
            }
        }
        Surface = Ff7SnowboardSurface.Sample(native, Navigation);
        hasNormalBasis = true;
        motion.FloorHeight = ending && CourseEnded ? 2147483648f : Surface.FloorHeightQ12;
        if (Ff7SnowboardRiderMotion.AdvanceVertical(motion))
        {
            Rider.SignalLanding();
        }
    }

    internal IReadOnlyList<Ff7SnowboardActionEffect> AdvanceRider(ushort held, ushort pressed, long tick)
    {
        Rider.UpdateVisualSlope(movementNormal);
        return Rider.Advance(held, pressed, tick, Navigation.SurfaceFlag, Course.Attributes[Navigation.CurrentSegment], Surface.CourseDirection);
    }

    internal void RestorePreviousMotion()
    {
        Rider.Motion.Position = PreviousPosition;
        Navigation.RestorePreviousSegment(PreviousSegment, PreviousTriangle);
        LogicalDistance = previousLogicalDistance;
    }

    private Ff7SnowboardCourseData Course => native.Courses[Navigation.CurrentCourse];
}
