using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCameraPoint(short X, short Y, short Z);
public sealed record NativeBattleCameraPose(NativeBattleCameraPoint Eye, NativeBattleCameraPoint Target, short? FocalLength, short Roll);
public sealed record NativeBattleCameraKnot(short Time, byte EyeReference, NativeBattleCameraPoint Eye, byte TargetReference, NativeBattleCameraPoint Target);
public sealed record NativeBattleCameraTrackPose(ushort Flags, ImmutableArray<NativeBattleCameraKnot> Knots, ImmutableArray<ImmutableArray<float>> Coefficients, short? FocalStart, short? FocalEnd, short RollStart, short RollEnd, NativeBattleCameraPoint? Eye, NativeBattleCameraPoint? Target);
public sealed record NativeBattleCameraPoseInputs
{
    public Func<byte, NativeBattleCameraPoint, NativeBattleCameraPoint>? TransformReference { get; init; }
}

public sealed partial record NativeBattleCameraTask
{
    public NativeBattleCameraTrackPose? Pose { get; init; }
}

public sealed partial record NativeBattleCameraState
{
    public ImmutableArray<NativeBattleCameraTrackPose?> RetainedPoses { get; init; } = [null, null];
    public int? SelectedSlot { get; init; }
    public NativeBattleCameraPose? CurrentPose { get; init; }
    public NativeBattleCameraPose? CapturedPose { get; init; }
    public short? FocalLength { get; init; }
    public short Roll { get; init; }

    private (int Next, ushort? Duration, NativeBattleCameraTrackPose? Pose) ReadPoseSegment(NativeBattleCameraTask task, NativeBattleCameraPoseInputs inputs, uint graphicsFlags)
    {
        int offset = task.ProgramOffset;
        short ReadWord()
        {
            if (offset < 0 || offset > task.Bank.Data.Length - 2)
            {
                throw new InvalidDataException($"Native camera channel {task.Channel} segment at byte {task.ProgramOffset} reads byte {offset} beyond its original {task.Bank.Data.Length}-byte track resource.");
            }
            short value = BinaryPrimitives.ReadInt16LittleEndian(task.Bank.Data.AsSpan().Slice(offset));
            offset += 2;
            return value;
        }
        ushort flags = unchecked((ushort)ReadWord());
        if (flags == ushort.MaxValue)
        {
            return (offset, null, task.Pose);
        }
        (short? focalStart, short? focalEnd) = (flags >> 6 & 3) switch
        {
            0 => (task.Pose?.FocalStart, task.Pose?.FocalEnd),
            1 => ((short)512, (short)512),
            2 => ReadConstant(),
            _ => (ReadWord(), ReadWord())
        };
        (short? start, short? end) = (flags >> 8 & 3) switch
        {
            0 => (Roll, Roll),
            1 => ((short)0, (short)0),
            2 => ReadConstant(),
            _ => (ReadWord(), ReadWord())
        };
        ImmutableArray<NativeBattleCameraKnot>.Builder knots = ImmutableArray.CreateBuilder<NativeBattleCameraKnot>();
        ushort duration = 0;
        for (short frames = ReadWord(); frames >= 0; frames = ReadWord())
        {
            if (knots.Count >= 32)
            {
                throw new InvalidDataException("Native camera segment exceeds its thirty-two-knot storage.");
            }
            byte eyeReference = unchecked((byte)ReadWord());
            NativeBattleCameraPoint eye = new(ReadWord(), ReadWord(), ReadWord());
            byte targetReference = unchecked((byte)ReadWord());
            NativeBattleCameraPoint target = new(ReadWord(), ReadWord(), ReadWord());
            if ((flags & 1) != 0)
            {
                eye = TransformCameraPoint(eyeReference, eye, inputs, graphicsFlags);
                target = TransformCameraPoint(targetReference, target, inputs, graphicsFlags);
                eyeReference = 0xFB;
                targetReference = 0xFB;
            }
            knots.Add(new(unchecked((short)duration), eyeReference, eye, targetReference, target));
            duration = unchecked((ushort)(duration + 16 * frames));
        }
        ImmutableArray<NativeBattleCameraKnot> points = knots.ToImmutable();
        ImmutableArray<ImmutableArray<float>> coefficients = points.Length > 2 ? Enumerable.Range(0, 6).Select(axis => NativeBattleCameraMath.SplineCoefficients(points.Select(knot => knot.Time).ToArray(), points.Select(knot => CameraCoordinate(axis < 3 ? knot.Eye : knot.Target, axis % 3)).ToArray())).ToImmutableArray() : [];
        NativeBattleCameraTrackPose pose = new(flags, points, coefficients, focalStart, focalEnd, start!.Value, end!.Value, task.Pose?.Eye, task.Pose?.Target);
        return (offset, duration, pose);

        (short?, short?) ReadConstant()
        {
            short value = ReadWord();
            return (value, value);
        }
    }

    private NativeBattleCameraTrackPose SampleCameraTrack(NativeBattleCameraTask task, NativeBattleCameraPoseInputs inputs, uint graphicsFlags)
    {
        NativeBattleCameraTrackPose pose = task.Pose ?? throw new InvalidDataException("Native camera task lacks its parsed knot owner.");
        ImmutableArray<NativeBattleCameraKnot> knots = pose.Knots;
        if (knots.IsEmpty)
        {
            return pose;
        }
        if (knots.Length == 1)
        {
            if ((pose.Flags & 1) != 0 && task.Elapsed != 0)
            {
                return pose;
            }
            return ResolveKnot(knots[0]);
        }
        int elapsed = unchecked((short)task.Elapsed);
        int time;
        int fraction;
        NativeBattleCameraPoint eye;
        NativeBattleCameraPoint target;
        int upper;
        if (knots.Length == 2)
        {
            if (elapsed >= knots[1].Time)
            {
                return ResolveKnot(knots[1]);
            }
            fraction = NativeBattleCameraMath.Ease((elapsed << 12) / knots[1].Time, pose.Flags);
            eye = InterpolateCameraPoint(knots[0].Eye, knots[1].Eye, fraction);
            target = InterpolateCameraPoint(knots[0].Target, knots[1].Target, fraction);
            upper = 1;
        }
        else
        {
            time = unchecked(NativeBattleCameraMath.Ease((elapsed << 12) / unchecked((short)task.Duration), pose.Flags) * unchecked((short)task.Duration)) >> 12;
            if (time >= knots[^1].Time)
            {
                return ResolveKnot(knots[^1]);
            }
            short[] times = knots.Select(knot => knot.Time).ToArray();
            short[] coordinates = new short[6];
            for (int axis = 0; axis < 6; axis++)
            {
                short[] values = knots.Select(knot => CameraCoordinate(axis < 3 ? knot.Eye : knot.Target, axis % 3)).ToArray();
                coordinates[axis] = NativeBattleCameraMath.SampleSpline(unchecked((short)time), times, values, pose.Coefficients[axis]);
            }
            eye = new(coordinates[0], coordinates[1], coordinates[2]);
            target = new(coordinates[3], coordinates[4], coordinates[5]);
            upper = 0;
            while (upper < knots.Length && time >= knots[upper].Time)
            {
                upper++;
            }
            if (upper is <= 0 || upper >= knots.Length)
            {
                throw new InvalidDataException("Native camera spline references an invalid knot interval.");
            }
            fraction = ((time - knots[upper - 1].Time) << 12) / (knots[upper].Time - knots[upper - 1].Time);
        }
        return pose with
        {
            Eye = ResolveBetween(eye, knots[upper - 1].EyeReference, knots[upper].EyeReference, fraction),
            Target = ResolveBetween(target, knots[upper - 1].TargetReference, knots[upper].TargetReference, fraction)
        };

        NativeBattleCameraTrackPose ResolveKnot(NativeBattleCameraKnot knot)
        {
            return pose with { Eye = TransformCameraPoint(knot.EyeReference, knot.Eye, inputs, graphicsFlags), Target = TransformCameraPoint(knot.TargetReference, knot.Target, inputs, graphicsFlags) };
        }

        NativeBattleCameraPoint ResolveBetween(NativeBattleCameraPoint point, byte from, byte to, int blend)
        {
            NativeBattleCameraPoint first = TransformCameraPoint(from, point, inputs, graphicsFlags);
            return from == to ? first : InterpolateCameraPoint(first, TransformCameraPoint(to, point, inputs, graphicsFlags), blend);
        }
    }

    private NativeBattleCameraPoint TransformCameraPoint(byte reference, NativeBattleCameraPoint point, NativeBattleCameraPoseInputs inputs, uint graphicsFlags)
    {
        if (reference == 0xFB)
        {
            return point;
        }
        if (reference == 0xF1)
        {
            return (graphicsFlags & 0x20) != 0 ? point with { Z = unchecked((short)-point.Z) } : point;
        }
        if (reference is 0xF3 or 0xF4)
        {
            NativeBattleCameraPose pose = CurrentPose ?? throw new NotSupportedException("Native camera F3/F4 requires its actual current eye/target owner.");
            //50D9D0 clears all nine rotation WORDs, so these aliases contribute translation only.
            return reference == 0xF3 ? pose.Eye : pose.Target;
        }
        return (inputs.TransformReference ?? throw new NotSupportedException($"Native camera reference{reference:X2} requires its current transform/bone owner."))(reference, point);
    }

    private static short CameraCoordinate(NativeBattleCameraPoint point, int axis) => axis switch { 0 => point.X, 1 => point.Y, _ => point.Z };

    private static NativeBattleCameraPoint InterpolateCameraPoint(NativeBattleCameraPoint from, NativeBattleCameraPoint to, int fraction)
    {
        short Interpolate(short start, short end) => unchecked((short)(start + (unchecked(fraction * (end - start)) >> 12)));
        return new(Interpolate(from.X, to.X), Interpolate(from.Y, to.Y), Interpolate(from.Z, to.Z));
    }

    private NativeBattleCameraState PublishCameraPose()
    {
        if (SelectedSlot is not int slot || RetainedPoses[slot] is not { Eye: { } eye, Target: { } target } track)
        {
            return this;
        }
        if (BlendProgress >= 4096)
        {
            return CapturedPose is { } captured ? this with { CurrentPose = captured, FocalLength = captured.FocalLength, Roll = captured.Roll } : this;
        }
        if (BlendProgress != 0)
        {
            NativeBattleCameraPose captured = CapturedPose ?? throw new NotSupportedException("Native camera blend requires its503300 captured pose.");
            short? focal = track.FocalEnd is short start && captured.FocalLength is short end ? unchecked((short)(start + (BlendProgress * (end - start) >> 12))) : null;
            return this with { CurrentPose = new(InterpolateCameraPoint(eye, captured.Eye, BlendProgress), InterpolateCameraPoint(target, captured.Target, BlendProgress), focal, Roll), FocalLength = focal };
        }
        return (Mask & 0xDFFF) != 0 ? this with { CurrentPose = new(eye, target, FocalLength, Roll) } : this;
    }
}
