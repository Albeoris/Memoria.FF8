using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>548CD0 ordered dense-object scan for the actual Garden allocation, including549320's forced-circle transfer path.</summary>
public static class WorldGardenContactCertificate
{
    public static int Evaluate(WorldAssets assets, NativeGardenWorldAllocation allocation, IReadOnlyList<NativeGardenWorldObject> objects, int currentEntity, int selectedEntity, IReadOnlyList<WorldNativeGardenProbe> probes, NativeWorldPathProjectionCertificate projection, int padding = 0, bool forceCircle = false, bool trainContactsExcluded = false)
    {
        allocation.Validate();
        if (objects.Count != allocation.Objects.Length || objects.Where((item, index) => item.ShapeId != allocation.Objects[index].ShapeId).Any() || probes.Count != 5 || padding is not (0 or 200))
        {
            throw new InvalidDataException("Garden contact requires its actual ordered allocation, five native probes and original blocking/interaction padding.");
        }
        byte selectedShape = WorldAssets.NativeGardenEntityShape(selectedEntity);
        NativeWorldContactShape selected = assets.ReadNativeContactShape(selectedShape);
        NativeWorldPathWords[] points = probes.Select(probe => projection.RelativeWords(new NativeWorldPathPosition(probe.X, probe.Y, probe.GraphicsZ))).ToArray();
        int foot = allocation.FootObjectIndex;
        int chocobo = Find(2);
        int chocoboAlternate = Find(3);
        int ragnarok = Find(1);
        int car = objects.Select((item, index) => (item, index)).Where(pair => pair.item.EntityType is >= 32 and <= 40 or 132).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
        for (int index = 0; index < objects.Count; index++)
        {
            NativeGardenWorldObject item = objects[index];
            bool footFamily = selectedEntity is >= 0 and < 10 or 128 or 49;
            bool carFamily = selectedEntity is >= 32 and <= 40 or 132;
            if (footFamily && (index == foot || index == chocobo || index == chocoboAlternate) || selectedEntity == 50 && (index == foot || index == ragnarok || index == chocobo || index == chocoboAlternate) || carFamily && (index == foot || index == car || index == chocobo || index == chocoboAlternate) || selectedEntity == 48 && (index == foot || index == chocobo || index == chocoboAlternate || index == allocation.Garden64Index || index == allocation.Garden65Index))
            {
                continue;
            }
            if (item.ShapeId == 94 || currentEntity is >= 32 and <= 40 or 132 && index == car || currentEntity == 50 && index == ragnarok || currentEntity == 48 && (index == allocation.Garden64Index || index == allocation.Garden65Index))
            {
                continue;
            }
            if (!forceCircle && currentEntity == 48 && (index == car || index == ragnarok))
            {
                continue;
            }
            if (!projection.IsNear(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection anchor = projection.ProjectNode(item.Position) ?? throw new NotSupportedException(projection.UnsupportedReason);
            NativeWorldContactShape shape = assets.ReadNativeContactShape(item.ShapeId);
            bool ignoreHeight = currentEntity is >= 32 and <= 40 or 132 && selectedEntity is 48 or 50 || currentEntity == 49 && item.ShapeId is 93 or 96;
            if (EvaluateObject(shape, item.RotationY, item.ShapeId, selected, selectedEntity, points, anchor.Words, padding, forceCircle, ignoreHeight))
            {
                return index;
            }
        }
        if (currentEntity != 48 && selectedEntity is not (>= 16 and <= 22) && !trainContactsExcluded)
        {
            throw new NotSupportedException("Parked Garden foot contacts require actual rail-bank state or the existing camera-based train-route exclusion proof.");
        }
        return -1;

        int Find(byte shape) => objects.Select((item, index) => (item, index)).Where(pair => pair.item.ShapeId == shape).Select(pair => pair.index).DefaultIfEmpty(-1).Single();
    }

    public static bool EvaluateObject(NativeWorldContactShape target, short targetYaw, byte targetShape, NativeWorldContactShape selected, int selectedEntity, IReadOnlyList<NativeWorldPathWords> points, NativeWorldPathWords anchor, int padding, bool forceCircle, bool ignoreHeight = false)
    {
        if (points.Count != 5 || padding is not (0 or 200))
        {
            throw new InvalidDataException("Native Garden object contact requires five original points and represented padding.");
        }
        NativeWorldPathWords center = points[0];
        short dx = unchecked((short)(center.X - anchor.X));
        short dz = unchecked((short)(center.Z - anchor.Z));
        int square = unchecked(dx * dx + dz * dz);
        if (square >= 0x400000)
        {
            return false;
        }
        if (forceCircle)
        {
            // 548CD0's selected54BA30 already includes padding;549320
            // adds its own padding again in the forced-circle branch.
            short radius = unchecked((short)(Radius(selected) + Radius(target) + 2 * padding));
            return radius * radius > square;
        }
        if (!ignoreHeight && (center.Y - selected.Height > anchor.Y + padding || center.Y < anchor.Y - target.Height - padding))
        {
            return false;
        }
        if (target.Kind == 4)
        {
            short radius = unchecked((short)(Radius(target) + padding));
            int radiusSquare = radius * radius;
            foreach (NativeWorldPathWords point in points)
            {
                short pointX = unchecked((short)(point.X - anchor.X));
                short pointZ = unchecked((short)(point.Z - anchor.Z));
                if (unchecked(pointX * pointX + pointZ * pointZ) <= radiusSquare)
                {
                    return true;
                }
            }
            (short x, short z) = NativeGardenTransform.CircleBoundary(dx, dz, radius);
            NativeWorldPathWords boundary = new(unchecked((short)(anchor.X + x)), 0, unchecked((short)(anchor.Z + z)));
            return Contains(points.Skip(1).ToArray(), boundary);
        }
        int probeRadius = NativeWorldTerrain.ProbeRadius(selectedEntity);
        (int X, int Z)[] expected = [(0, 0), (-probeRadius, 0), (0, probeRadius), (probeRadius, 0), (0, -probeRadius)];
        for (int index = 0; index < expected.Length; index++)
        {
            if (points[index].X != center.X + expected[index].X || points[index].Z != center.Z + expected[index].Z)
            {
                throw new NotSupportedException("Native Garden polygon contact requires its represented canonical five-point footprint without WORD wrapping.");
            }
        }
        short yaw = unchecked((short)(targetYaw + WorldAssets.NativeGardenShapeYawCorrection(targetShape)));
        return WorldStationContactCertificate.EvaluatePolygon(target, yaw, center, anchor, padding, probeRadius);
    }

    private static short Radius(NativeWorldContactShape shape)
    {
        return shape.Kind switch
        {
            1 or 3 or 4 => unchecked((short)Math.Max(shape.Length >> 1, shape.Width >> 1)),
            2 => unchecked((short)Math.Max(shape.Length, shape.Width >> 1)),
            _ => throw new NotSupportedException("Native Garden contact requires original54BA30 shape dimensions.")
        };
    }

    private static bool Contains(IReadOnlyList<NativeWorldPathWords> polygon, NativeWorldPathWords point)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeWorldPathWords first = polygon[index];
            NativeWorldPathWords second = polygon[(index + 1) % 4];
            int edgeX = second.X - first.X;
            int edgeZ = second.Z - first.Z;
            int pointX = point.X - first.X;
            int pointZ = point.Z - first.Z;
            if (new[] { edgeX, edgeZ, pointX, pointZ }.Any(value => value is < -4096 or > 4096))
            {
                throw new NotSupportedException("Native Garden circle containment crosses its unrepresented packed-WORD boundary.");
            }
            if ((long)pointZ * edgeX - (long)pointX * edgeZ > 0)
            {
                return false;
            }
        }
        return true;
    }
}
