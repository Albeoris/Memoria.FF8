using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldNativeTerrainOcclusionResult(bool? Occluded, int Blocks, int Polygons, string? Reason);

internal readonly record struct NativeTerrainScreenBounds(int MinimumX, int MaximumX, int MinimumY, int MaximumY, int MinimumDepth, int MaximumDepth);

public sealed partial class NativeWorldPathProjectionCertificate
{
    // Terrain certificates are created for one camera callback. Shared vertices reuse
    // their projection only within that certificate, including bounded refusals.
    private readonly Dictionary<(NativeWorldPathPosition Position, bool GroundReference, bool Fast), NativeTerrainScreenBounds?> terrainOcclusionProjections = [];

    internal NativeTerrainScreenBounds? ProjectTerrainOcclusion(NativeWorldPathPosition position, bool groundReference = false, bool fast = false)
    {
        var key = (position, groundReference, fast);
        if (!terrainOcclusionProjections.TryGetValue(key, out NativeTerrainScreenBounds? result))
        {
            result = ProjectTerrainOcclusionUncached(position, groundReference, fast);
            terrainOcclusionProjections.Add(key, result);
        }
        return result;
    }

    private NativeTerrainScreenBounds? ProjectTerrainOcclusionUncached(NativeWorldPathPosition position, bool groundReference, bool fast)
    {
        NativeWorldPathWords relative = Relative(position);
        Range[] point = [Word(Add(Range.Exact(relative.X), offset[0])), Word(Add(Range.Exact(relative.Y), offset[1])), Word(Add(Range.Exact(relative.Z), offset[2]))];
        Range initialDepth = Rotate(view, point)[2];
        int minimumHeight;
        int maximumHeight;
        if (groundReference)
        {
            NativeWorldPathNodeProjection? reference = ProjectNode(position);
            if (reference is null)
            {
                return null;
            }
            minimumHeight = reference.Words.Y;
            maximumHeight = minimumHeight;
        }
        else
        {
            int low = Math.Clamp(initialDepth.Low, 0, ushort.MaxValue);
            int high = Math.Clamp(initialDepth.High, 0, ushort.MaxValue);
            if (high - low > 512)
            {
                return null;
            }
            minimumHeight = short.MaxValue;
            maximumHeight = short.MinValue;
            for (int depth = low; depth <= high; depth++)
            {
                int curve = (depth >> 2) - curveThreshold;
                //4022D0/5513A0 use signed >>12 here, unlike5470E0's two divisions.
                int height = curve < 0 ? relative.Y : unchecked(((4096 - curve) * relative.Y) >> 12) + (curve >> 6) * (curve >> 6);
                short wrappedHeight = unchecked((short)height);
                minimumHeight = Math.Min(minimumHeight, wrappedHeight);
                maximumHeight = Math.Max(maximumHeight, wrappedHeight);
            }
        }
        point[1] = Word(Add(new Range(minimumHeight, maximumHeight), offset[1]));
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
        foreach (Range value in point)
        {
            if (!value.Unique)
            {
                return null;
            }
        }
        foreach (Range value in view)
        {
            if (!value.Unique)
            {
                return null;
            }
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
        if (roundedDepth > focal / 2)
        {
            double ratio = focal / depth;
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

    internal bool? RendersTerrainCell(int cell, int entityType)
    {
        bool? generated = GeneratesTerrainCell(cell);
        if (generated == false)
        {
            return false;
        }
        int bodyCell = Sector(Origin);
        int dx = SectorX(cell, bodyCell);
        int dz = SectorZ(cell, bodyCell);
        int radius = entityType == 50 || entityType is >= 64 and <= 66 ? 3 : 1;
        if (Math.Abs(dx) <= radius && Math.Abs(dz) <= radius)
        {
            return generated;
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
        bool uncertain = false;
        foreach ((int X, int Y, int Z)[] triangle in triangles)
        {
            NativeTerrainScreenBounds[] points = triangle.Select(point => ProjectTerrainCellBound(unchecked((short)(dx * 2048 + point.X)), checked((short)point.Y), unchecked((short)(-dz * 2048 + point.Z)))).ToArray();
            int maximumDepth = entityType == 50 ? 8191 : 6144;
            if (points.Any(point => point.MinimumX >= 0 && point.MaximumX <= 0xA00) && points[2].MaximumDepth >> 2 <= maximumDepth)
            {
                return generated;
            }
            //Only a proved failure of both native bound triangles excludes a cell.
            //Intervals crossing an admission boundary must remain unknown.
            bool excluded = points.All(point => point.MaximumX < 0 || point.MinimumX > 0xA00) || points[2].MinimumDepth >> 2 > maximumDepth;
            uncertain |= !excluded;
        }
        return uncertain ? null : false;
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
        if (entityType is not (0 or 1 or 2 or 32 or 33 or 35 or 48 or 50))
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
        NativeTerrainScreenBounds[] points = new NativeTerrainScreenBounds[3];
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
                bool? rendersCell = projection.RendersTerrainCell(cell, entityType);
                if (rendersCell == false)
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
                foreach (WorldTriangle triangle in segment.Triangles)
                {
                    if (triangle.NativeBlockIndex != block)
                    {
                        continue;
                    }
                    polygons++;
                    NativeTerrainScreenBounds? a = projection.ProjectTerrainOcclusion(NativeTerrainPosition(triangle.A), fast: !direct);
                    NativeTerrainScreenBounds? b = projection.ProjectTerrainOcclusion(NativeTerrainPosition(triangle.B), fast: !direct);
                    NativeTerrainScreenBounds? c = projection.ProjectTerrainOcclusion(NativeTerrainPosition(triangle.C), fast: !direct);
                    if (a is null || b is null || c is null)
                    {
                        uncertain ??= $"Native terrain vertex projection exceeds the bounded rounding envelope at map{map}/block{block}/polygon{triangle.NativePolygonIndex}.";
                        continue;
                    }
                    points[0] = a.Value;
                    points[1] = b.Value;
                    points[2] = c.Value;
                    //The fast renderer compares eighth-depth buckets against a
                    //halved quarter-depth threshold, including its lost low bit.
                    bool depthExcluded = direct ? points.Max(point => point.MinimumDepth) >> 2 >= threshold : points.Max(point => point.MinimumDepth) >> 3 >= threshold >> 1;
                    if (depthExcluded)
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
                    if (depthAdmitted && clippingAdmitted && frontFace == true && definitelyCrosses && polygonFilterKnown && rendersCell == true)
                    {
                        return new(true, blocks, polygons, null);
                    }
                    uncertain ??= $"Native terrain camera occlusion is ambiguous at map{map}/block{block}/polygon{triangle.NativePolygonIndex}: depth={depthAdmitted}, clip={clippingAdmitted}, front={frontFace}, crossing={definitelyCrosses}, polygonFilter={polygonFilterKnown}, cell={rendersCell}.";
                }
            }
        }
        return uncertain is null ? new(false, blocks, polygons, null) : new(null, blocks, polygons, OcclusionReason(uncertain, camera, groundHeight, entityType));
    }

    private byte[] NativeTerrainVertexCounts(int sourceId)
    {
        if (!nativeTerrainVertexCounts.TryGetValue(sourceId, out byte[]? counts))
        {
            using Stream source = terrain.OpenRead();
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
        Span<(int Low, int High)> x = stackalloc (int, int)[3];
        Span<(int Low, int High)> y = stackalloc (int, int)[3];
        for (int index = 0; index < 3; index++)
        {
            x[index] = SignedScreenWords(points[index].MinimumX, points[index].MaximumX);
            y[index] = SignedScreenWords(points[index].MinimumY, points[index].MaximumY);
        }
        long low = 0;
        long high = 0;
        for (int index = 0; index < 3; index++)
        {
            (int Low, int High) first = x[index];
            (int Low, int High) next = y[(index + 1) % 3];
            (int Low, int High) last = y[(index + 2) % 3];
            long differenceLow = (long)next.Low - last.High;
            long differenceHigh = (long)next.High - last.Low;
            long a = (long)first.Low * differenceLow;
            long b = (long)first.Low * differenceHigh;
            long c = (long)first.High * differenceLow;
            long d = (long)first.High * differenceHigh;
            low += Math.Min(Math.Min(a, b), Math.Min(c, d));
            high += Math.Max(Math.Max(a, b), Math.Max(c, d));
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
        Span<(int Low, int High)> values = stackalloc (int, int)[3];
        for (int index = 0; index < 3; index++)
        {
            values[index] = horizontal ? SignedScreenWords(points[index].MinimumX, points[index].MaximumX) : SignedScreenWords(points[index].MinimumY, points[index].MaximumY);
        }
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
