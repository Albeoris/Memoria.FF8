using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record WorldNativeTerrainOcclusionResult(bool? Occluded, int Blocks, int Polygons, string? Reason);

internal readonly record struct NativeTerrainScreenBounds(int MinimumX, int MaximumX, int MinimumY, int MaximumY, int MinimumDepth, int MaximumDepth);

public sealed partial class NativeWorldPathProjectionCertificate
{
    internal NativeTerrainScreenBounds? ProjectTerrainOcclusion(NativeWorldPathPosition position, bool groundReference = false)
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
                int curve = (depth >> 2) - CurveThreshold;
                //4022D0/5513A0 use signed >>12 here, unlike5470E0's two divisions.
                int height = curve < 0 ? relative.Y : unchecked(((4096 - curve) * relative.Y) >> 12) + (curve >> 6) * (curve >> 6);
                heights.Add(unchecked((short)height));
            }
        }
        point[1] = Word(Add(new Range(heights.Min(), heights.Max()), offset[1]));
        Range[] curved = Rotate(view, point);
        Range x = Project(curved[0], curved[2], 160);
        Range y = Project(curved[1], curved[2], 112);
        return new NativeTerrainScreenBounds(x.Low, x.High, y.Low, y.High, Math.Clamp(curved[2].Low, 0, ushort.MaxValue), Math.Clamp(curved[2].High, 0, ushort.MaxValue));
    }
}

public sealed partial class WorldAssets
{
    private readonly Dictionary<int, byte[]> nativeTerrainVertexCounts = [];

    public WorldNativeTerrainOcclusionResult CertifyNativeTerrainCameraOcclusion(NativeWorldFootCameraState camera, short groundHeight, IReadOnlyDictionary<int, int> replacements, int entityType = 48)
    {
        if (entityType is not (0 or 1 or 32 or 33 or 48))
        {
            throw new NotSupportedException("Native terrain camera occlusion requires the reached ordinary foot/car/Garden renderer.");
        }
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeTerrainScreenBounds? body = projection.ProjectTerrainOcclusion(new(camera.X, groundHeight, -camera.RawZ), groundReference: true);
        if (body is null)
        {
            return new(null, 0, 0, "Native5472C0 ground-reference depth has no bounded projection.");
        }
        int threshold = (body.Value.MaximumDepth >> 2) - 256;
        if (threshold <= 0)
        {
            return new(false, 0, 0, null);
        }
        int centerCell = NativeCell(camera.X, camera.RawZ);
        int blocks = 0;
        int polygons = 0;
        //544490's6144 displacement spans3 cells; renderer proximity spans5.
        //One extra cell covers the current signed-WORD movement at a cell edge.
        //This superset may refuse an irrelevant triangle, but cannot certify a missed latch.
        for (int dz = -9; dz <= 9; dz++)
        {
            for (int dx = -9; dx <= 9; dx++)
            {
                int cellX = (centerCell % 128 + dx + 128) % 128;
                int cellZ = (centerCell / 128 + dz + 96) % 96;
                int map = cellZ / 4 * 32 + cellX / 4;
                int block = cellZ % 4 * 4 + cellX % 4;
                WorldSegment segment = GetSegment(map, replacements);
                bool direct = NativeTerrainVertexCounts(segment.SourceId)[block] > 90;
                //552380 sets203FDC4 only for native foot0..9/128; car/Garden
                //can acquire it only in the direct5513A0 path (>90 vertices).
                if (!direct && entityType is not (0 or 1))
                {
                    continue;
                }
                blocks++;
                foreach (WorldTriangle triangle in segment.Triangles.Where(triangle => triangle.NativeBlockIndex == block))
                {
                    polygons++;
                    NativeTerrainScreenBounds?[] projected = new[] { triangle.A, triangle.B, triangle.C }.Select(position => projection.ProjectTerrainOcclusion(NativeTerrainPosition(position))).ToArray();
                    if (projected.Any(point => point is null))
                    {
                        return new(null, blocks, polygons, "Native terrain vertex projection exceeds the bounded rounding envelope.");
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
                    bool xCross = CouldStraddle(points, direct ? 160 : 320, true);
                    bool yCross = CouldStraddle(points, direct ? 180 : 360, false);
                    if (direct ? xCross && yCross : xCross || yCross)
                    {
                        return new(null, blocks, polygons, $"Native terrain camera occlusion may be set by map{map}/block{block}/polygon{triangle.NativePolygonIndex}; exact clipping/backface admission is required.");
                    }
                }
            }
        }
        return new(false, blocks, polygons, null);
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

    private static bool CouldStraddle(IReadOnlyList<NativeTerrainScreenBounds> points, int threshold, bool horizontal)
    {
        for (int first = 0; first < points.Count; first++)
        {
            int low = horizontal ? points[first].MinimumX : points[first].MinimumY;
            int high = horizontal ? points[first].MaximumX : points[first].MaximumY;
            bool mayAbove = low < 0 || high > threshold;
            if (!mayAbove)
            {
                continue;
            }
            for (int second = 0; second < points.Count; second++)
            {
                int secondLow = horizontal ? points[second].MinimumX : points[second].MinimumY;
                int secondHigh = horizontal ? points[second].MaximumX : points[second].MaximumY;
                if (first != second && secondHigh >= 0 && secondLow <= threshold)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
