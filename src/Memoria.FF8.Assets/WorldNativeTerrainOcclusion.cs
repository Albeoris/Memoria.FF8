using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldNativeTerrainOcclusionResult(bool? Occluded, int Blocks, int Polygons, string? Reason);

internal readonly record struct NativeTerrainScreenBounds(int MinimumX, int MaximumX, int MinimumY, int MaximumY, int MinimumDepth, int MaximumDepth);

public sealed partial class NativeWorldPathProjectionCertificate
{
    internal NativeTerrainScreenBounds? ProjectTerrainOcclusion(NativeWorldPathPosition position, bool groundReference = false, bool fast = false)
    {
        NativeWorldPathWords relative = Relative(position);
        Range[] point = [Word(Add(Range.Exact(relative.X), offset[0])), Word(Add(Range.Exact(relative.Y), offset[1])), Word(Add(Range.Exact(relative.Z), offset[2]))];
        Range initialDepth = Rotate(view, point)[2];
        HashSet<short> heights = [];
        if (groundReference)
        {
            NativeWorldPathNodeProjection? reference = ProjectNode(position);
            if (reference is null)
            {
                return null;
            }
            heights.Add(reference.Words.Y);
        }
        else
        {
            int low = Math.Clamp(initialDepth.Low, 0, ushort.MaxValue);
            int high = Math.Clamp(initialDepth.High, 0, ushort.MaxValue);
            if (high - low > 512)
            {
                return null;
            }
            for (int depth = low; depth <= high; depth++)
            {
                int curve = (depth >> 2) - curveThreshold;
                //4022D0/5513A0 use signed >>12 here, unlike5470E0's two divisions.
                int height = curve < 0 ? relative.Y : unchecked(((4096 - curve) * relative.Y) >> 12) + (curve >> 6) * (curve >> 6);
                heights.Add(unchecked((short)height));
            }
        }
        point[1] = Word(Add(new Range(heights.Min(), heights.Max()), offset[1]));
        if (fast)
        {
            return ProjectFastTerrain(point);
        }
        Range[] curved = Rotate(view, point);
        Range x = Project(curved[0], curved[2], 160);
        Range y = Project(curved[1], curved[2], 112);
        return new NativeTerrainScreenBounds(x.Low, x.High, y.Low, y.High, Math.Clamp(curved[2].Low, 0, ushort.MaxValue), Math.Clamp(curved[2].High, 0, ushort.MaxValue));
    }

    private NativeTerrainScreenBounds? ProjectFastTerrain(Range[] point)
    {
        if (!precision53 || point.Any(value => !value.Unique) || view.Cast<Range>().Any(value => !value.Unique))
        {
            return null;
        }
        double Dot(int row) => ((double)view[row, 2].Low * point[2].Low + (double)view[row, 1].Low * point[1].Low + (double)view[row, 0].Low * point[0].Low) / 4096;
        //4023D0 stores its X/Y accumulations as FLOAT before perspective.
        //The depth accumulation stays PC53 double; B693E0=0.125 cancels
        //the input*8 while B693E4=8 scales the zero renderer translation.
        float x = (float)(8 * Dot(0));
        float y = (float)(8 * Dot(1));
        double depth = Dot(2);
        int roundedDepth = NativePrecision53.RoundInteger(depth);
        int screenX;
        int screenY;
        if (roundedDepth > Focal / 2)
        {
            double ratio = Focal / depth;
            screenX = unchecked(8 * 160 + NativePrecision53.RoundInteger(ratio * x));
            screenY = unchecked(8 * 112 + NativePrecision53.RoundInteger(ratio * y));
        }
        else
        {
            screenX = unchecked(8 * 160 + 2 * NativePrecision53.RoundInteger(x));
            screenY = unchecked(8 * 112 + 2 * NativePrecision53.RoundInteger(y));
        }
        //Unlike45FE10,4023D0 does not clamp screen coordinates.
        //Keep the full DWORD for unsigned admission. The later signed depth
        //WORD can be consumed only after 0<depth<0x8000 has been admitted.
        return new(screenX, screenX, screenY, screenY, roundedDepth, roundedDepth);
    }

    private bool? GeneratesTerrainCell(int cell)
    {
        int bodyCell = Sector(Origin);
        int dx = SectorX(cell, bodyCell);
        int dz = SectorZ(cell, bodyCell);
        int x = bodyCell % 128 + dx;
        int z = bodyCell / 128 + dz;
        //53DDA0 generates only this proximity-centered inclusive rectangle.
        //Outside even its widest rounding envelope is a proved exclusion;
        //inside the narrower envelope is a proved generated cell.
        if (x < (int)Math.Floor((Origin.X + 131072d + proximity[0].Low - 10239) / 2048)
            || x > (int)Math.Floor((Origin.X + 131072d + proximity[0].High + 10239) / 2048)
            || z < (int)Math.Floor((-Origin.Z + 98304d + proximity[2].Low - 10239) / 2048)
            || z > (int)Math.Floor((-Origin.Z + 98304d + proximity[2].High + 10239) / 2048))
        {
            return false;
        }
        bool generated = x >= (int)Math.Floor((Origin.X + 131072d + proximity[0].High - 10239) / 2048)
            && x <= (int)Math.Floor((Origin.X + 131072d + proximity[0].Low + 10239) / 2048)
            && z >= (int)Math.Floor((-Origin.Z + 98304d + proximity[2].High - 10239) / 2048)
            && z <= (int)Math.Floor((-Origin.Z + 98304d + proximity[2].Low + 10239) / 2048);
        return generated ? true : null;
    }

    internal bool DefinitelyExcludesTerrainCell(int cell) => GeneratesTerrainCell(cell) == false;

    internal bool DefinitelyRendersTerrainCell(int cell, int entityType)
    {
        if (GeneratesTerrainCell(cell) != true)
        {
            return false;
        }
        int bodyCell = Sector(Origin);
        int dx = SectorX(cell, bodyCell);
        int dz = SectorZ(cell, bodyCell);
        int radius = entityType == 50 || entityType is >= 64 and <= 66 ? 3 : 1;
        if (Math.Abs(dx) <= radius && Math.Abs(dz) <= radius)
        {
            return true;
        }
        //53D3C0 retains a farther generated cell when either of its original
        //flat/vertical bound triangles has an admitted X and SZ3>>2 depth.
        //The projected buffer is pipelined; the second triangle does not
        //belong to the next list cell even though its coordinates are prepared early.
        (int X, int Y, int Z)[][] triangles =
        [
            [(0, 0, 0), (2048, 0, 0), (2048, 0, -2048)],
            [(0, 0, -2048), (1024, 0, -1024), (1024, -2048, -1024)]
        ];
        foreach ((int X, int Y, int Z)[] triangle in triangles)
        {
            NativeTerrainScreenBounds[] points = triangle.Select(point => ProjectTerrainCellBound(unchecked((short)(dx * 2048 + point.X)), checked((short)point.Y), unchecked((short)(-dz * 2048 + point.Z)))).ToArray();
            int maximumDepth = entityType == 50 ? 8191 : 6144;
            if (points.Any(point => point.MinimumX >= 0 && point.MaximumX <= 0xA00) && points[2].MaximumDepth >> 2 <= maximumDepth)
            {
                return true;
            }
        }
        return false;
    }

    private NativeTerrainScreenBounds ProjectTerrainCellBound(short x, short y, short z)
    {
        Range[] transformed = Rotate(view, [Range.Exact(x), Range.Exact(y), Range.Exact(z)]);
        for (int index = 0; index < transformed.Length; index++)
        {
            transformed[index] = Add(transformed[index], translation[index]);
        }
        Range screenX = Project(transformed[0], transformed[2], 160);
        return new(screenX.Low, screenX.High, 0, 0, Math.Clamp(transformed[2].Low, 0, ushort.MaxValue), Math.Clamp(transformed[2].High, 0, ushort.MaxValue));
    }

}

public sealed partial class WorldAssets
{
    private readonly Dictionary<int, byte[]> nativeTerrainVertexCounts = [];

    public WorldNativeTerrainOcclusionResult CertifyNativeTerrainCameraOcclusion(NativeWorldFootCameraState camera, short groundHeight, IReadOnlyDictionary<int, int> replacements, int entityType = 48, int? nativeCurveThreshold = null)
    {
        if (entityType is not (0 or 1 or 2 or 32 or 33 or 48 or 50))
        {
            throw new NotSupportedException("Native terrain camera occlusion requires the reached ordinary foot/car/Garden renderer.");
        }
        //5519B0 (ordinary50 fast blocks) never writes203FDC4.552380's
        //scripted50 fast blocks also exclude50. The shared direct5513A0
        //branch below remains the only possible latch producer for entity50.
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateTerrainFoot(camera, nativeCurveThreshold ?? (entityType == 50 ? 6144 : 5216));
        NativeTerrainScreenBounds? body = projection.ProjectTerrainOcclusion(new(camera.X, groundHeight, -camera.RawZ), groundReference: true);
        if (body is null)
        {
            return new(null, 0, 0, OcclusionReason("Native5472C0 ground-reference depth has no bounded projection.", camera, groundHeight, entityType));
        }
        int minimumThreshold = (body.Value.MinimumDepth >> 2) - 256;
        int threshold = (body.Value.MaximumDepth >> 2) - 256;
        if (threshold <= 0)
        {
            return new(false, 0, 0, null);
        }
        int centerCell = NativeCell(camera.X, camera.RawZ);
        int blocks = 0;
        int polygons = 0;
        string? uncertain = null;
        //544490's6144 displacement spans3 cells; renderer proximity spans5.
        //One extra cell covers the current signed-WORD movement at a cell edge.
        //This superset may refuse an irrelevant triangle, but cannot certify a missed latch.
        for (int dz = -9; dz <= 9; dz++)
        {
            for (int dx = -9; dx <= 9; dx++)
            {
                int cellX = (centerCell % 128 + dx + 128) % 128;
                int cellZ = (centerCell / 128 + dz + 96) % 96;
                int cell = cellZ * 128 + cellX;
                if (projection.DefinitelyExcludesTerrainCell(cell))
                {
                    continue;
                }
                int map = cellZ / 4 * 32 + cellX / 4;
                int block = cellZ % 4 * 4 + cellX % 4;
                WorldSegment segment = GetSegment(map, replacements);
                bool direct = NativeTerrainVertexCounts(segment.SourceId)[block] > 90;
                //552380 sets203FDC4 only for native foot0..9/128; car/Garden
                //can acquire it only in the direct5513A0 path (>90 vertices).
                if (!direct && entityType is not (0 or 1 or 2))
                {
                    continue;
                }
                blocks++;
                foreach (WorldTriangle triangle in segment.Triangles.Where(triangle => triangle.NativeBlockIndex == block))
                {
                    polygons++;
                    NativeTerrainScreenBounds?[] projected = new[] { triangle.A, triangle.B, triangle.C }.Select(position => projection.ProjectTerrainOcclusion(NativeTerrainPosition(position), fast: !direct)).ToArray();
                    if (projected.Any(point => point is null))
                    {
                        uncertain ??= $"Native terrain vertex projection exceeds the bounded rounding envelope at map{map}/block{block}/polygon{triangle.NativePolygonIndex}.";
                        continue;
                    }
                    NativeTerrainScreenBounds[] points = projected.Select(point => point!.Value).ToArray();
                    if (points.Max(point => point.MinimumDepth) >> 2 >= threshold)
                    {
                        continue;
                    }
                    if (direct && (points.All(point => MinimumUnsigned(point.MinimumX, point.MaximumX) > 0xA00) || points.All(point => MinimumUnsigned(point.MinimumY, point.MaximumY) > 0x780)))
                    {
                        continue;
                    }
                    if (!direct && (points.Any(point => point.MaximumDepth <= 0 || point.MinimumDepth >= 0x8000) || points.All(point => MinimumUnsignedDword(point.MinimumX, point.MaximumX) >= 0xA00) || points.All(point => MinimumUnsignedDword(point.MinimumY, point.MaximumY) >= 0x700)))
                    {
                        //4023D0 emits vertex admission bits consumed by552380:
                        //OR bit0/1 must both exist, AND bit2 must exist, and no
                        //vertex may carry the nonpositive-depth high byte.
                        continue;
                    }
                    bool? frontFace = FacesCamera(points);
                    if (frontFace == false)
                    {
                        //45EE10→45E3C0 rejects signed screen determinant<=0
                        //before either renderer is allowed to publish203FDC4.
                        continue;
                    }
                    bool? xCross = Straddles(points, direct ? 160 : 320, true, direct);
                    bool? yCross = Straddles(points, direct ? 180 : 360, false, direct);
                    bool definitelyCrosses = direct ? xCross == true && yCross == true : xCross == true || yCross == true;
                    bool cannotCross = direct ? xCross == false || yCross == false : xCross == false && yCross == false;
                    if (cannotCross)
                    {
                        continue;
                    }
                    bool depthAdmitted = direct ? points.Max(point => point.MaximumDepth) >> 2 < minimumThreshold : points.Max(point => point.MaximumDepth) >> 3 < minimumThreshold >> 1;
                    bool clippingAdmitted = direct
                        ? points.Any(point => MaximumUnsigned(point.MinimumX, point.MaximumX) <= 0xA00) && points.Any(point => MaximumUnsigned(point.MinimumY, point.MaximumY) <= 0x780)
                        : points.All(point => point.MinimumDepth > 0 && point.MaximumDepth < 0x8000) && points.Any(point => point.MinimumX >= 0 && point.MaximumX < 0xA00) && points.Any(point => point.MinimumY >= 0 && point.MaximumY < 0x700);
                    //552380 has original per-polygon exclusions for a finite
                    //set of railway cells; no positive claim bypasses those.
                    bool polygonFilterKnown = direct || !NativeFastTerrainHasPolygonFilter(cell);
                    if (depthAdmitted && clippingAdmitted && frontFace == true && definitelyCrosses && polygonFilterKnown && projection.DefinitelyRendersTerrainCell(cell, entityType))
                    {
                        return new(true, blocks, polygons, null);
                    }
                    uncertain ??= $"Native terrain camera occlusion is ambiguous at map{map}/block{block}/polygon{triangle.NativePolygonIndex}: depth={depthAdmitted}, clip={clippingAdmitted}, front={frontFace}, crossing={definitelyCrosses}, polygonFilter={polygonFilterKnown}, cell={projection.DefinitelyRendersTerrainCell(cell, entityType)}.";
                }
            }
        }
        return uncertain is null ? new(false, blocks, polygons, null) : new(null, blocks, polygons, OcclusionReason(uncertain, camera, groundHeight, entityType));
    }

    private byte[] NativeTerrainVertexCounts(int sourceId)
    {
        if (!nativeTerrainVertexCounts.TryGetValue(sourceId, out byte[]? counts))
        {
            using Stream source = terrain.OpenForRead();
            source.Seek((long)sourceId * SegmentBytes, SeekOrigin.Begin);
            byte[] bytes = new byte[SegmentBytes];
            source.ReadExactly(bytes);
            counts = new byte[16];
            for (int block = 0; block < 16; block++)
            {
                int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 + 4 * block, 4)));
                counts[block] = bytes[offset + 1];
            }
            nativeTerrainVertexCounts.Add(sourceId, counts);
        }
        return counts;
    }

    private static NativeWorldPathPosition NativeTerrainPosition(Vector3 position)
    {
        return new((int)position.X, -(int)position.Y, -(int)position.Z);
    }

    private static int MinimumUnsigned(int low, int high)
    {
        return high < 0 ? low + 65536 : low < 0 ? 0 : low;
    }

    private static long MinimumUnsignedDword(int low, int high)
    {
        return high < 0 ? unchecked((uint)low) : low < 0 ? 0 : low;
    }

    private static int MaximumUnsigned(int low, int high)
    {
        return high < 0 ? high + 65536 : low < 0 ? 65535 : high;
    }

    private static bool? FacesCamera(IReadOnlyList<NativeTerrainScreenBounds> points)
    {
        (int Low, int High)[] x = points.Select(point => SignedScreenWords(point.MinimumX, point.MaximumX)).ToArray();
        (int Low, int High)[] y = points.Select(point => SignedScreenWords(point.MinimumY, point.MaximumY)).ToArray();
        long low = 0;
        long high = 0;
        for (int index = 0; index < 3; index++)
        {
            (int Low, int High) first = x[index];
            (int Low, int High) next = y[(index + 1) % 3];
            (int Low, int High) last = y[(index + 2) % 3];
            long differenceLow = (long)next.Low - last.High;
            long differenceHigh = (long)next.High - last.Low;
            long[] products = [(long)first.Low * differenceLow, (long)first.Low * differenceHigh, (long)first.High * differenceLow, (long)first.High * differenceHigh];
            low += products.Min();
            high += products.Max();
        }
        //Native determinant arithmetic wraps DWORD. A wrapping interval remains
        //unknown rather than manufacturing a backface rejection.
        if (low < int.MinValue || high > int.MaxValue)
        {
            return null;
        }
        return low > 0 ? true : high <= 0 ? false : null;
    }

    private static (int Low, int High) SignedScreenWords(int low, int high)
    {
        short first = unchecked((short)low);
        short last = unchecked((short)high);
        return (long)high - low <= ushort.MaxValue && last - first == (long)high - low ? (first, last) : (short.MinValue, short.MaxValue);
    }

    private static bool? Straddles(IReadOnlyList<NativeTerrainScreenBounds> points, int threshold, bool horizontal, bool direct)
    {
        (int Low, int High)[] values = points.Select(point => horizontal ? SignedScreenWords(point.MinimumX, point.MaximumX) : SignedScreenWords(point.MinimumY, point.MaximumY)).ToArray();
        bool maySet = false;
        bool mayClear = false;
        for (int mask = 0; mask < 8; mask++)
        {
            bool allowed = true;
            for (int index = 0; index < 3; index++)
            {
                bool above = (mask & 1 << index) != 0;
                bool canAbove = direct ? MaximumUnsigned(values[index].Low, values[index].High) > threshold : values[index].High > threshold;
                bool canBelow = direct ? MinimumUnsigned(values[index].Low, values[index].High) <= threshold : values[index].Low <= threshold;
                allowed &= above ? canAbove : canBelow;
            }
            if (!allowed)
            {
                continue;
            }
            bool a = (mask & 1) != 0;
            bool b = (mask & 2) != 0;
            bool c = (mask & 4) != 0;
            bool set = direct ? a != b || a != c : a != b && a == c;
            maySet |= set;
            mayClear |= !set;
        }
        return maySet ? mayClear ? null : true : false;
    }

    private static bool NativeFastTerrainHasPolygonFilter(int cell)
    {
        return cell is >= 6108 and <= 6111 or >= 5724 and <= 5727 or >= 5852 and <= 5855 or >= 5980 and <= 5983 or 3660 or 3788 or 3916;
    }

    private static string OcclusionReason(string reason, NativeWorldFootCameraState camera, short groundHeight, int entityType)
    {
        return $"{reason} Input: entity={entityType}, groundHeight={groundHeight}, camera=({camera.X},{camera.RawZ},{camera.NativeHeight}; target={camera.TargetX},{camera.TargetRawZ},{camera.TargetNativeHeight}; yaw={camera.Yaw}, pitch={camera.Pitch}, offsets={camera.OffsetY},{camera.OffsetZ}).";
    }
}
