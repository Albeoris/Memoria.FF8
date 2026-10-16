using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeRagnarokTransferKind
{
    Landing,
    Takeoff
}

/// <summary>Executed native camera globals, offsetZ WORD203ECFC, and the five/four signed projection words at C767C4/C767F8. OffsetX/Y remain separately owned.</summary>
public sealed record NativeRagnarokTransferCamera(int ProjectionDistance, int CurveThreshold, short Pitch, short Yaw, short OffsetZ, ImmutableArray<short> ProjectionWords, ImmutableArray<short> PerspectiveWords)
{
    public void Validate()
    {
        if (ProjectionWords.IsDefault || ProjectionWords.Length != 5 || PerspectiveWords.IsDefault || PerspectiveWords.Length != 4)
        {
            throw new InvalidDataException("Ragnarok transfer camera requires all nine actual native projection words.");
        }
    }
}

public sealed record NativeRagnarokTransferStep(NativeRagnarokTransferState State, NativeWorldPathPosition Position, NativeRagnarokTransferCamera Camera, int TransitionPhase, int AnimationCounter, int InitialVerticalScale, bool? CameraProjectionMode, bool Completed);

/// <summary>Pure54CD90/54D1A0 progression. Admission, allocation, terrain and final54B460 destination publication remain caller-owned.</summary>
public sealed record NativeRagnarokTransferState(NativeRagnarokTransferKind Kind, NativeWorldPathPosition InitialPosition, int GroundHeight, short BodyYaw, NativeRagnarokTransferCamera InitialCamera, NativeRagnarokTransferCamera TargetCamera, int Counter = 0)
{
    public void Validate()
    {
        InitialCamera.Validate();
        TargetCamera.Validate();
        if (Kind is not (NativeRagnarokTransferKind.Landing or NativeRagnarokTransferKind.Takeoff) || Counter is < 0 or > 61)
        {
            throw new InvalidDataException("Ragnarok transfer requires its exact native direction and callback counter0..61.");
        }
        if (Kind == NativeRagnarokTransferKind.Takeoff && (TargetCamera.ProjectionDistance != 1024 || TargetCamera.CurveThreshold != 6144))
        {
            throw new InvalidDataException("Native Ragnarok takeoff camera uses558BB0(50)=1024 and558C00(50)=6144.");
        }
        if (Kind == NativeRagnarokTransferKind.Landing && (TargetCamera.ProjectionDistance is not (1024 or 640) || TargetCamera.CurveThreshold != 5216))
        {
            throw new NotSupportedException("Native Ragnarok landing camera requires the actual128 view0/1 target.");
        }
    }

    public NativeRagnarokTransferStep Advance()
    {
        Validate();
        if (Counter == 61)
        {
            throw new InvalidOperationException("Completed Ragnarok transfer must publish54B460's destination before another callback.");
        }
        bool takeoff = Kind == NativeRagnarokTransferKind.Takeoff;
        int count = Counter + 1;
        int heightTarget = takeoff ? unchecked(GroundHeight + ((-3584 - GroundHeight) >> 1)) : GroundHeight;
        short heightStep = unchecked((short)(unchecked(heightTarget - InitialPosition.Y) / 60));
        NativeWorldPathPosition position = InitialPosition with { Y = unchecked(InitialPosition.Y + count * heightStep) };
        int cameraCount = takeoff ? Math.Max(0, count - 41) : count;
        int cameraDivisor = takeoff ? 20 : 60;
        int focalDifference = unchecked(TargetCamera.ProjectionDistance - InitialCamera.ProjectionDistance);
        // Original instructions divide full DWORD differences and only then
        //storeDX. Decompiled short casts before division are not authoritative.
        short focalStep = unchecked((short)(focalDifference / 60));
        short curveStep = unchecked((short)(unchecked(TargetCamera.CurveThreshold - InitialCamera.CurveThreshold) / cameraDivisor));
        short pitchStep = unchecked((short)(Delta(TargetCamera.Pitch, InitialCamera.Pitch) / cameraDivisor));
        int offsetDifference = TargetCamera.OffsetZ - InitialCamera.OffsetZ;
        short offsetStep = unchecked((short)(offsetDifference / cameraDivisor));
        short yawStep = unchecked((short)(Delta(BodyYaw, InitialCamera.Yaw) / (takeoff ? 40 : 60)));
        short yaw = takeoff && count > 41 ? BodyYaw : unchecked((short)(InitialCamera.Yaw + count * yawStep));
        NativeRagnarokTransferCamera camera = new(unchecked(InitialCamera.ProjectionDistance + count * focalStep), unchecked(InitialCamera.CurveThreshold + cameraCount * curveStep), unchecked((short)(InitialCamera.Pitch + cameraCount * pitchStep)), yaw, unchecked((short)(InitialCamera.OffsetZ + cameraCount * offsetStep)), AdvanceWords(InitialCamera.ProjectionWords, TargetCamera.ProjectionWords, cameraCount, cameraDivisor), AdvanceWords(InitialCamera.PerspectiveWords, TargetCamera.PerspectiveWords, cameraCount, cameraDivisor));
        bool completed = count > 60;
        if (completed && takeoff)
        {
            //54CD90 alone snaps these globals. Pitch is recomputed from the
            //final incremented body height; landing deliberately has no snap.
            camera = camera with { ProjectionDistance = 1024, CurveThreshold = 6144, Pitch = unchecked((short)(-256 - position.Y / 24)), OffsetZ = TargetCamera.OffsetZ };
        }
        int animation = completed && !takeoff ? 0 : takeoff ? Math.Clamp(319 - 8 * count, 0, 319) : Math.Clamp(8 * count, 0, 319);
        return new NativeRagnarokTransferStep(this with { Counter = count }, position, camera, completed ? 0 : 307, animation, takeoff ? 120 : 16, takeoff ? count <= 41 : null, completed);
    }

    private static ImmutableArray<short> AdvanceWords(ImmutableArray<short> initial, ImmutableArray<short> target, int count, int divisor)
    {
        return initial.Select((value, index) => unchecked((short)(value + count * ((target[index] - value) / divisor)))).ToImmutableArray();
    }

    private static int Delta(int first, int second)
    {
        int normalizedFirst = (first % 4096 + 4096) % 4096;
        int normalizedSecond = (second % 4096 + 4096) % 4096;
        return NativeGardenMotor.Delta(normalizedFirst, normalizedSecond);
    }
}
