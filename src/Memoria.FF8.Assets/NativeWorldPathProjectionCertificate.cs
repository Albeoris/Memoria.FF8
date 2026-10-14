using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>552EB0/544490/5470E0 sector-relative scripted node projection with explicit native rounding intervals.</summary>
public sealed class NativeWorldPathProjectionCertificate
{
    private readonly record struct Range(int Low, int High)
    {
        public bool Unique => Low == High;
        public static Range Exact(int value) => new(value, value);
    }

    // Original B6B980 float bits3AC90FDB, B695F8=1/4096, B69540=4096.
    private const double RadiansPerUnit = 0.0015339808305725455;
    private const int Focal = 1024;
    private const int CurveThreshold = 5216;
    private readonly Range[,] rotation;
    private readonly Range[,] view;
    private readonly Range[] translation;
    private readonly Range[] offset;
    private readonly Range[] proximity;
    private readonly int originSector;
    private readonly NativeWorldPathPosition sectorBase;

    private NativeWorldPathProjectionCertificate(NativeWorldPathCamera camera, NativeWorldPathPosition origin)
    {
        Camera = camera;
        Origin = origin;
        originSector = Sector(origin);
        NativeWorldPathWords localOrigin = Local(origin);
        sectorBase = new NativeWorldPathPosition(origin.X - localOrigin.X, 0, origin.Z - localOrigin.Z);
        int dx = unchecked((short)(camera.Target.X - camera.Eye.X));
        int dy = unchecked((short)(camera.Target.Y - camera.Eye.Y));
        int dz = unchecked((short)(camera.Target.Z - camera.Eye.Z));
        long total = (long)dx * dx + (long)dy * dy + (long)dz * dz;
        long horizontal = (long)dx * dx + (long)dz * dz;
        if (total > int.MaxValue || horizontal > int.MaxValue || total == 0)
        {
            throw new NotSupportedException("Native scripted camera square-root overflow or coincident eye/target is outside the projection certificate.");
        }
        int distance = (int)Math.Sqrt(total);
        if (distance > short.MaxValue)
        {
            throw new NotSupportedException("Native scripted camera offset exceeds its supported signed-WORD distance.");
        }
        short pitch = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(-dy, (int)Math.Sqrt(horizontal)));
        short yaw = unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dx, dz));
        Range sp = Trig(pitch, false);
        Range cp = Trig(pitch, true);
        Range sy = Trig(yaw, false);
        Range cy = Trig(yaw, true);
        // 56CE30 starts native identity and left-multiplies Z0, Xpitch, Yyaw.
        rotation = new Range[3, 3]
        {
            { cy, Product(sy, sp), Product(sy, cp) },
            { Range.Exact(0), cp, Negate(sp) },
            { Negate(sy), Product(cy, sp), Product(cy, cp) }
        };
        view = new Range[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                view[row, column] = rotation[column, row];
            }
        }
        NativeWorldPathWords targetLocal = Relative(camera.Target);
        Range[] eyeLocal = Rotate(rotation, [Range.Exact(0), Range.Exact(0), Range.Exact(-distance)]);
        eyeLocal[0] = Add(eyeLocal[0], Range.Exact(targetLocal.X));
        eyeLocal[1] = Add(eyeLocal[1], Range.Exact(targetLocal.Y));
        eyeLocal[2] = Add(eyeLocal[2], Range.Exact(targetLocal.Z));
        translation = Rotate(view, eyeLocal).Select(Negate).ToArray();
        // 552AD0→56C800 rotates the view translation through the transposed view.
        offset = Rotate(rotation, translation);
        // 544490 uses56C600's stored row-dot matrix and packed(0,0,-6144).
        proximity = Rotate(view, [Range.Exact(0), Range.Exact(0), Range.Exact(-6144)]);
    }

    public NativeWorldPathCamera Camera { get; }
    public NativeWorldPathPosition Origin { get; }
    public string? UnsupportedReason { get; private set; }
    public NativeWorldPathPosition? ViewTranslation => UniquePosition(translation);
    public NativeWorldPathPosition? CameraRelativeOffset => UniquePosition(offset);
    public IReadOnlyList<short>? ViewRotation => view.Cast<Range>().All(value => value.Unique) ? view.Cast<Range>().Select(value => checked((short)value.Low)).ToArray() : null;

    public static NativeWorldPathProjectionCertificate Create(NativeWorldPathCamera camera, NativeWorldPathPosition callbackEntityOrigin, byte entityType)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (entityType is not (65 or 66) && entityType is not (>= 16 and <= 22))
        {
            throw new NotSupportedException("Native scripted projection setup requires the executed16..22/65/66 entity branch, not inferred foot camera defaults.");
        }
        if (callbackEntityOrigin.X is < -131072 or >= 131072 || callbackEntityOrigin.Z is <= -98304 or > 98304)
        {
            throw new NotSupportedException("Scripted projection requires its actual wrapped native player origin.");
        }
        return new NativeWorldPathProjectionCertificate(camera, callbackEntityOrigin);
    }

    public NativeWorldPathNodeProjection? ProjectNode(NativeWorldPathPosition node)
    {
        UnsupportedReason = null;
        NativeWorldPathWords relative = Relative(node);
        Range[] point = [Word(Add(Range.Exact(relative.X), offset[0])), Word(Add(Range.Exact(relative.Y), offset[1])), Word(Add(Range.Exact(relative.Z), offset[2]))];
        Range[] transformed = Rotate(view, point);
        // 45FAA0 depth is unsigned0..65535 before its focal/2 denominator clamp.
        Range depth = new(Math.Clamp(transformed[2].Low, 0, ushort.MaxValue), Math.Clamp(transformed[2].High, 0, ushort.MaxValue));
        if (depth.High - depth.Low > 512)
        {
            return Unsupported("Native depth interval exceeds the bounded curve proof.");
        }
        HashSet<short> heights = [];
        for (int value = depth.Low; value <= depth.High; value++)
        {
            int curve = (value >> 2) - CurveThreshold;
            int height = relative.Y;
            if (curve >= 0)
            {
                //5470E0 divides twice toward zero and narrows back to WORD before reprojection.
                height = unchecked(height * (4096 - curve)) / 2 / 2048 + (curve >> 6) * (curve >> 6);
            }
            heights.Add(unchecked((short)height));
        }
        if (heights.Count != 1)
        {
            return Unsupported("Native camera rounding interval reaches different transformed node WORD heights.");
        }
        short outputHeight = heights.Single();
        point[1] = Word(Add(Range.Exact(outputHeight), offset[1]));
        Range[] curved = Rotate(view, point);
        Range x = Project(curved[0], curved[2], 160);
        Range y = Project(curved[1], curved[2], 112);
        bool allScreenVisible = x.Low > -1600 && x.High < 4160 && y.Low > -240;
        bool allScreenHidden = x.High <= -1600 || x.Low >= 4160 || y.High <= -240;
        (bool allNear, bool allFar) = Proximity(node);
        bool visible;
        if (allFar || allScreenHidden)
        {
            visible = false;
        }
        else if (allNear && allScreenVisible)
        {
            visible = true;
        }
        else
        {
            return Unsupported("Native rounding interval reaches different5470E0 visibility results.");
        }
        return new NativeWorldPathNodeProjection(1, NativeWorldPathNodeProjection.NativeRule, node, Camera, Origin, new NativeWorldPathWords(relative.X, outputHeight, relative.Z), visible);
    }

    public NativeWorldPathNodeProjection? Project(NativeWorldPathPosition node) => ProjectNode(node);

    public Vector3 CanonicalModelAnchor(NativeWorldPathNodeProjection projection)
    {
        ValidateProjection(projection);
        return new Vector3(sectorBase.X + projection.Words.X, -projection.Words.Y, -(sectorBase.Z + projection.Words.Z));
    }

    public void ValidateProjection(NativeWorldPathNodeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        projection.Validate(projection.Source, Camera, Origin);
        NativeWorldPathNodeProjection? actual = ProjectNode(projection.Source);
        if (actual is null)
        {
            throw new NotSupportedException(UnsupportedReason ?? "Native projection receipt cannot be independently reconstructed.");
        }
        if (projection != actual)
        {
            throw new InvalidDataException("Native node projection words or visibility differ from the actual source, camera and callback origin transform.");
        }
    }

    private NativeWorldPathNodeProjection? Unsupported(string reason)
    {
        UnsupportedReason = reason;
        return null;
    }

    private NativeWorldPathWords Relative(NativeWorldPathPosition position)
    {
        NativeWorldPathWords local = Local(position);
        int sector = Sector(position);
        return new NativeWorldPathWords(unchecked((short)(local.X + (SectorX(sector, originSector) << 11))), local.Y, unchecked((short)(local.Z - 2048 * SectorZ(sector, originSector))));
    }

    private (bool AllNear, bool AllFar) Proximity(NativeWorldPathPosition node)
    {
        if (proximity[0].High - proximity[0].Low > 32 || proximity[2].High - proximity[2].Low > 32)
        {
            return (false, false);
        }
        int sector = Sector(node);
        bool near = true;
        bool far = true;
        for (int x = proximity[0].Low; x <= proximity[0].High; x++)
        {
            for (int z = proximity[2].Low; z <= proximity[2].High; z++)
            {
                //544490 adds its Z component to RAW playerZ, not graphicsZ.
                int reference = Sector(new NativeWorldPathPosition(Origin.X + x, 0, Origin.Z - z));
                bool current = Math.Abs(SectorX(sector, reference)) <= 5 && Math.Abs(SectorZ(sector, reference)) <= 5;
                near &= current;
                far &= !current;
            }
        }
        return (near, far);
    }

    private static int Sector(NativeWorldPathPosition position) => ((position.X + 393216) % 262144 / 2048) + 128 * ((294912 - position.Z) % 196608 / 2048);
    private static int SectorX(int first, int second)
    {
        int difference = first % 128 - second % 128;
        return difference > 64 ? difference - 128 : difference < -64 ? difference + 128 : difference;
    }
    private static int SectorZ(int first, int second)
    {
        int difference = first / 128 - second / 128;
        return difference > 48 ? difference - 96 : difference < -48 ? difference + 96 : difference;
    }
    private static NativeWorldPathWords Local(NativeWorldPathPosition position)
    {
        int z = position.Z & 2047;
        return new NativeWorldPathWords((short)(position.X & 2047), unchecked((short)position.Y), (short)(z == 0 ? 0 : z - 2048));
    }

    private static Range Trig(short angle, bool cosine) => Round((cosine ? Math.Cos(angle * RadiansPerUnit) : Math.Sin(angle * RadiansPerUnit)) * 4096);
    private static Range Negate(Range value) => new(-value.High, -value.Low);
    private static Range Add(Range first, Range second) => new(checked(first.Low + second.Low), checked(first.High + second.High));
    private static Range Product(Range first, Range second)
    {
        long[] values = [(long)first.Low * second.Low, (long)first.Low * second.High, (long)first.High * second.Low, (long)first.High * second.High];
        return Round(values.Min() / 4096d, values.Max() / 4096d);
    }
    private static Range Word(Range value)
    {
        short low = unchecked((short)value.Low);
        short high = unchecked((short)value.High);
        return value.High - value.Low <= ushort.MaxValue && high - low == value.High - value.Low ? new Range(low, high) : new Range(short.MinValue, short.MaxValue);
    }
    private static Range[] Rotate(Range[,] matrix, Range[] point)
    {
        Range[] result = new Range[3];
        for (int row = 0; row < 3; row++)
        {
            long low = 0;
            long high = 0;
            for (int column = 0; column < 3; column++)
            {
                Range coefficient = matrix[row, column];
                Range coordinate = point[column];
                long[] products = [(long)coefficient.Low * coordinate.Low, (long)coefficient.Low * coordinate.High, (long)coefficient.High * coordinate.Low, (long)coefficient.High * coordinate.High];
                low += products.Min();
                high += products.Max();
            }
            result[row] = Round(low / 4096d, high / 4096d);
        }
        return result;
    }
    private static Range Round(double value) => Round(value, value);
    private static Range Round(double low, double high)
    {
        const double magicAddError = 1d / 4096;
        return new Range(checked((int)Math.Round(Math.BitDecrement(low) - magicAddError, MidpointRounding.ToEven)), checked((int)Math.Round(Math.BitIncrement(high) + magicAddError, MidpointRounding.ToEven)));
    }
    private static Range Project(Range coordinate, Range rawDepth, int center)
    {
        int low = int.MaxValue;
        int high = int.MinValue;
        foreach (int value in new[] { coordinate.Low, coordinate.High })
        {
            foreach (int z in new[] { rawDepth.Low, rawDepth.High })
            {
                int depth = Math.Max(Focal / 2, Math.Clamp(z, 0, ushort.MaxValue));
                double ratio = Focal / (double)depth;
                int multiplier = checked(8 * value);
                double first = multiplier * (multiplier < 0 ? Math.BitIncrement(ratio) : Math.BitDecrement(ratio));
                double last = multiplier * (multiplier < 0 ? Math.BitDecrement(ratio) : Math.BitIncrement(ratio));
                Range rounded = Round(Math.BitDecrement(first), Math.BitIncrement(last));
                low = Math.Min(low, Math.Clamp(rounded.Low + 8 * center, -8192, 8184));
                high = Math.Max(high, Math.Clamp(rounded.High + 8 * center, -8192, 8184));
            }
        }
        return new Range(low, high);
    }
    private static NativeWorldPathPosition? UniquePosition(Range[] values) => values.All(value => value.Unique) ? new NativeWorldPathPosition(values[0].Low, values[1].Low, values[2].Low) : null;
}
