using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>552EB0/544490/5470E0 sector-relative scripted node projection under the native PC precision53 arithmetic contract.</summary>
public sealed partial class NativeWorldPathProjectionCertificate
{
    private readonly record struct Range(int Low, int High)
    {
        public bool Unique => Low == High;
        public static Range Exact(int value) => new(value, value);
    }

    // Original B6B980 float bits3AC90FDB, B695F8=1/4096, B69540=4096.
    private const double RadiansPerUnit = 0.0015339808305725455;
    private readonly int focal;
    private const int CurveThreshold = 5216;
    private readonly int curveThreshold;
    private readonly Range[,] rotation;
    private readonly Range[,] view;
    private readonly Range[] translation;
    private readonly Range[] offset;
    private readonly Range[] proximity;
    private readonly int originSector;
    private readonly NativeWorldPathPosition sectorBase;

    private NativeWorldPathProjectionCertificate(NativeWorldPathCamera camera, NativeWorldPathPosition origin, int curveThreshold = 5216)
    {
        this.curveThreshold = curveThreshold;
        focal = camera.FootState?.ProjectionDistance ?? 1024;
        Camera = camera;
        Origin = origin;
        originSector = Sector(origin);
        NativeWorldPathWords localOrigin = Local(origin);
        sectorBase = new NativeWorldPathPosition(origin.X - localOrigin.X, 0, origin.Z - localOrigin.Z);
        short pitch;
        short yaw;
        short offsetY;
        short offsetZ;
        if (camera.FootState is { } foot)
        {
            foot.Validate();
            if (camera.Eye is not null)
            {
                throw new InvalidDataException("Native projection camera cannot have both scripted eye and explicit foot words.");
            }
            pitch = foot.Pitch;
            yaw = checked((short)foot.Yaw);
            offsetY = foot.OffsetY;
            offsetZ = foot.OffsetZ;
        }
        else
        {
            NativeTrainCameraGeometry geometry = NativeTrainCameraGeometry.Evaluate(camera);
            pitch = geometry.Pitch;
            yaw = geometry.Yaw;
            offsetY = 0;
            offsetZ = geometry.OffsetZ;
        }
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
        Range[] eyeLocal = Rotate(rotation, [Range.Exact(0), Range.Exact(offsetY), Range.Exact(offsetZ)]);
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

    public static NativeWorldPathProjectionCertificate CreateFoot(WorldState state) => CreateFoot(NativeWorldFootCameraState.Create(state));

    public static NativeWorldPathProjectionCertificate CreateFoot(NativeWorldFootCameraState state)
    {
        state.Validate();
        NativeWorldPathCamera camera = new(null, new NativeWorldPathPosition(state.TargetX, state.TargetNativeHeight, -state.TargetRawZ)) { FootState = state };
        return new NativeWorldPathProjectionCertificate(camera, new NativeWorldPathPosition(state.X, state.NativeHeight, -state.RawZ));
    }

    public static NativeWorldPathProjectionCertificate CreateRagnarok(NativeWorldFootCameraState state, int curveThreshold = 6144)
    {
        state.Validate();
        NativeWorldPathCamera camera = new(null, new NativeWorldPathPosition(state.TargetX, state.TargetNativeHeight, -state.TargetRawZ)) { FootState = state };
        //557250 entity50 sets the same1024 focal but its own6144 world curve.
        return new NativeWorldPathProjectionCertificate(camera, state.Origin, curveThreshold: curveThreshold);
    }

    internal static NativeWorldPathProjectionCertificate CreateTerrainFoot(NativeWorldFootCameraState state, int curveThreshold = 5216)
    {
        state.Validate();
        NativeWorldPathCamera camera = new(null, new NativeWorldPathPosition(state.TargetX, state.TargetNativeHeight, -state.TargetRawZ)) { FootState = state };
        return new NativeWorldPathProjectionCertificate(camera, new NativeWorldPathPosition(state.X, state.NativeHeight, -state.RawZ), curveThreshold: curveThreshold);
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
            int curve = (value >> 2) - curveThreshold;
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

    public NativeWorldPathWords RelativeWords(NativeWorldPathPosition position) => Relative(position);

    public bool IsNear(NativeWorldPathPosition position)
    {
        (bool allNear, bool allFar) = Proximity(position);
        if (!allNear && !allFar)
        {
            throw new NotSupportedException("Native camera rounding interval crosses its object proximity boundary.");
        }
        return allNear;
    }

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

    private Range Trig(short angle, bool cosine) => Round((cosine ? Math.Cos(angle * RadiansPerUnit) : Math.Sin(angle * RadiansPerUnit)) * 4096);
    private static Range Negate(Range value) => new(-value.High, -value.Low);
    private static Range Add(Range first, Range second) => new(checked(first.Low + second.Low), checked(first.High + second.High));
    private Range Product(Range first, Range second)
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
    private Range[] Rotate(Range[,] matrix, Range[] point)
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
                long a = (long)coefficient.Low * coordinate.Low;
                long b = (long)coefficient.Low * coordinate.High;
                long c = (long)coefficient.High * coordinate.Low;
                long d = (long)coefficient.High * coordinate.High;
                low += Math.Min(Math.Min(a, b), Math.Min(c, d));
                high += Math.Max(Math.Max(a, b), Math.Max(c, d));
            }
            result[row] = Round(low / 4096d, high / 4096d);
        }
        return result;
    }
    private Range Round(double value) => Round(value, value);
    private Range Round(double low, double high)
    {
        // Each endpoint uses the original magic-double conversion. Matrix row dots
        // are exact Q12 binary64 values; do not invent neighboring integer results.
        return new Range(NativePrecision53.RoundInteger(low), NativePrecision53.RoundInteger(high));
    }
    private Range Project(Range coordinate, Range rawDepth, int center)
    {
        int low = int.MaxValue;
        int high = int.MinValue;
        foreach (int value in new[] { coordinate.Low, coordinate.High })
        {
            foreach (int z in new[] { rawDepth.Low, rawDepth.High })
            {
                int depth = Math.Max(focal / 2, Math.Clamp(z, 0, ushort.MaxValue));
                int screen = unchecked(NativePrecision53.RoundInteger(unchecked(8 * value) * (focal / (double)depth)) + 8 * center);
                int clamped = Math.Clamp(screen, -8192, 8184);
                low = Math.Min(low, clamped);
                high = Math.Max(high, clamped);
            }
        }
        return new Range(low, high);
    }
    private static NativeWorldPathPosition? UniquePosition(Range[] values) => values.All(value => value.Unique) ? new NativeWorldPathPosition(values[0].Low, values[1].Low, values[2].Low) : null;
}
