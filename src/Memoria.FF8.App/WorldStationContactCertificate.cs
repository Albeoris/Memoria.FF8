using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

public sealed record WorldStationContactResult(int ContactIndex, int? BankIndex, int? NodeIndex)
{
    public bool Hit => ContactIndex >= 0;
}

/// <summary>548CD0/549B40 contact over the retained dense allocation and current train projections.</summary>
public static class WorldStationContactCertificate
{
    private readonly record struct Interval(long Low, long High);
    private readonly record struct Point(Interval X, Interval Z);

    public static WorldStationContactResult Evaluate(WorldAssets assets, NativeTrainWorldAllocation allocation, IReadOnlyList<NativeTrainRailBank> banks, NativeWorldPathWords player, int playerHeight, Func<NativeWorldPathPosition, NativeWorldPathNodeProjection> projectStatic, Func<NativeWorldPathPosition, bool> proximity, int padding = 0, int probeRadius = 32, int selectedHeight = 180, bool forceCircle = false, NativeWorldContactShape? selectedShape = null)
    {
        allocation.Validate();
        RequirePadding(padding);
        if (forceCircle && (selectedShape is null || banks.Count != 0))
        {
            throw new NotSupportedException("Forced native contact requires the selected original shape and an excluded rail bank.");
        }
        if (probeRadius is <= 0 or > 256 || selectedHeight is <= 0 or > short.MaxValue || banks.Count > 0 && (probeRadius != 32 || selectedHeight != 180))
        {
            throw new NotSupportedException("Native static contact requires represented selected-body dimensions; car/train overlap is unsupported.");
        }
        for (int index = 0; index < allocation.Objects.Length; index++)
        {
            NativeTrainWorldObject item = allocation.Objects[index];
            if (index == allocation.ControlledObjectIndex || item.ShapeId == 94 || !proximity(item.Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection projection = projectStatic(item.Position);
            NativeWorldContactShape shape = assets.ReadNativeContactShape(item.ShapeId);
            if (forceCircle)
            {
                if (EvaluateForcedCircle(shape, selectedShape!, player, projection.Words, padding))
                {
                    return new WorldStationContactResult(index, null, null);
                }
                continue;
            }
            if (player.Y - selectedHeight > projection.Words.Y + padding || player.Y < projection.Words.Y - shape.Height - padding || OutsideBroadPhase(player, projection.Words))
            {
                continue;
            }
            //549770 uses original WORD22 plus54A770's entity-class yaw correction; roll/pitch do not enter contact.
            short yaw = WorldStaticModelTransform.AllocationAngles(item).Yaw;
            if (EvaluatePolygon(shape, yaw, player, projection.Words, padding, probeRadius))
            {
                return new WorldStationContactResult(index, null, null);
            }
        }
        for (int bankIndex = 0; bankIndex < banks.Count; bankIndex++)
        {
            NativeTrainRailBank bank = banks[bankIndex];
            int node = EvaluateTrain(assets, bank, assets.ReadScriptedPath(bank.TrackIndex), player, playerHeight, proximity, padding);
            if (node >= 0)
            {
                return new WorldStationContactResult(allocation.Objects.Length, bankIndex, node);
            }
        }
        return new WorldStationContactResult(-1, null, null);
    }

    public static bool EvaluateForcedCircle(NativeWorldContactShape target, NativeWorldContactShape selected, NativeWorldPathWords player, NativeWorldPathWords anchor, int padding = 0)
    {
        // 549320's forced branch consumes only the center, omits height gates,
        // and uses the shared54BA30 radius contract for both shapes.
        NativeWorldPathWords[] points = [player, player, player, player, player];
        return WorldGardenContactCertificate.EvaluateObject(target, 0, 0, selected, 0, points, anchor, padding, true);
    }

    public static int EvaluateTrain(WorldAssets assets, NativeTrainRailBank bank, NativeWorldScriptedPathDefinition definition, NativeWorldPathWords player, int playerHeight, Func<NativeWorldPathPosition, bool> proximity, int padding = 0)
    {
        bank.Validate(definition);
        RequirePadding(padding);
        for (int index = 0; index < bank.Nodes.Length; index++)
        {
            int link = NativeTrainRailBank.Link(index);
            if (link == 0)
            {
                continue;
            }
            NativeWorldPathNodePresentation pose = bank.Presentation[index];
            if (pose.Projection is null && !proximity(bank.Nodes[index].Position))
            {
                continue;
            }
            NativeWorldPathNodeProjection current = pose.Projection ?? throw new NotSupportedException("Native train contact needs this callback's actual5470E0 words.");
            // 548CD0/549B40 consume retained proximity48 and transformed words after an endpoint reset.
            if (!proximity(current.Source))
            {
                continue;
            }
            NativeWorldPathNodeProjection linked = bank.Presentation[index + link].Projection ?? throw new NotSupportedException("Native train height contact needs its actual linked-node transformed words.");
            NativeWorldContactShape shape = assets.ReadNativeContactShape(bank.Shape(definition, index));
            if (playerHeight < Math.Min(current.Words.Y, linked.Words.Y) - shape.Height - padding || OutsideBroadPhase(player, current.Words))
            {
                continue;
            }
            short yaw = pose.Angles?.Yaw ?? throw new NotSupportedException("Native train contact requires its retained executed yaw WORD.");
            if (EvaluatePolygon(shape, yaw, player, current.Words, padding))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>54BB10/549770/5498E0/549DD0, including native five-point and reverse containment order.</summary>
    public static bool EvaluatePolygon(NativeWorldContactShape shape, short yaw, NativeWorldPathWords player, NativeWorldPathWords anchor, int padding = 0, int probeRadius = 32)
    {
        RequirePadding(padding);
        if (probeRadius is <= 0 or > 256)
        {
            throw new NotSupportedException("Native contact probe radius lies outside its represented movement footprint.");
        }
        if (shape.Kind is < 1 or > 3 || shape.Length < 0 || shape.Width < 0)
        {
            throw new NotSupportedException("Native contact reached an unrepresented circle or shape layout.");
        }
        if (OutsideBroadPhase(player, anchor))
        {
            return false;
        }
        int halfLength = shape.Length >> 1;
        int halfWidth = shape.Width >> 1;
        int x = padding + halfLength;
        int z = padding + halfWidth;
        (int X, int Z)[] corners = shape.Kind switch
        {
            1 => [(-x, z), (x, z), (x, -z), (-x, -z)],
            2 => [(-padding - shape.Length, z), (-padding, z), (-padding, -z), (-padding - shape.Length, -z)],
            3 => [(-padding - (shape.Length >> 2), -z), (-x, z), (x, z), (padding + (shape.Length >> 2), -z)],
            _ => throw new InvalidOperationException()
        };
        Interval cosine = Round(Math.Cos(yaw * 0.0015339808305725455) * 4096);
        Interval sine = Round(Math.Sin(yaw * 0.0015339808305725455) * 4096);
        List<Point> polygon = [];
        foreach ((int localX, int localZ) in corners)
        {
            Interval transformedX = Round(Add(Multiply(cosine, Exact(localX)), Multiply(sine, Exact(localZ))), 4096);
            Interval transformedZ = Round(Add(Multiply(Negate(sine), Exact(localX)), Multiply(cosine, Exact(localZ))), 4096);
            if (anchor.X + transformedX.Low < short.MinValue || anchor.X + transformedX.High > short.MaxValue || anchor.Z + transformedZ.Low < short.MinValue || anchor.Z + transformedZ.High > short.MaxValue || player.X < short.MinValue + probeRadius || player.X > short.MaxValue - probeRadius || player.Z < short.MinValue + probeRadius || player.Z > short.MaxValue - probeRadius)
            {
                throw new NotSupportedException("Native contact polygon crosses its signed-WORD packing boundary.");
            }
            polygon.Add(new Point(Add(transformedX, Exact(anchor.X - player.X)), Add(transformedZ, Exact(anchor.Z - player.Z))));
        }
        Point[] probes = [At(0, 0), At(-probeRadius, 0), At(0, probeRadius), At(probeRadius, 0), At(0, -probeRadius)];
        Point[] foot = [probes[1], probes[2], probes[3], probes[4]];
        bool uncertain = false;
        foreach (Point probe in probes)
        {
            bool? hit = Contains(polygon, probe);
            if (hit == true)
            {
                return true;
            }
            uncertain |= hit is null;
        }
        foreach (Point point in new[] { At(anchor.X - player.X, anchor.Z - player.Z) }.Concat(polygon))
        {
            bool? hit = Contains(foot, point);
            if (hit == true)
            {
                return true;
            }
            uncertain |= hit is null;
        }
        if (uncertain)
        {
            throw new NotSupportedException("Native fixed rotation rounding changes the reached polygon contact result.");
        }
        return false;
    }

    private static bool? Contains(IReadOnlyList<Point> polygon, Point point)
    {
        bool uncertain = false;
        for (int index = 0; index < 4; index++)
        {
            Point first = polygon[index];
            Point second = polygon[(index + 1) % 4];
            Interval edgeX = Subtract(second.X, first.X);
            Interval edgeZ = Subtract(second.Z, first.Z);
            Interval pointX = Subtract(point.X, first.X);
            Interval pointZ = Subtract(point.Z, first.Z);
            // Native vector Y is pointZ*edgeX - pointX*edgeZ; positive rejects, zero is inside.
            Interval cross = Subtract(Multiply(pointZ, edgeX), Multiply(pointX, edgeZ));
            if (cross.Low > 0)
            {
                return false;
            }
            uncertain |= cross.High > 0;
        }
        return uncertain ? null : true;
    }

    private static bool OutsideBroadPhase(NativeWorldPathWords player, NativeWorldPathWords anchor)
    {
        int dx = unchecked((short)(player.X - anchor.X));
        int dz = unchecked((short)(player.Z - anchor.Z));
        return (long)dx * dx + (long)dz * dz >= 0x400000;
    }

    private static void RequirePadding(int padding)
    {
        if (padding is not (0 or 200))
        {
            throw new NotSupportedException("Native station contacts require the reached blocking or interaction padding.");
        }
    }

    private static Point At(int x, int z) => new(Exact(x), Exact(z));
    private static Interval Exact(long value) => new(value, value);
    private static Interval Negate(Interval value) => new(-value.High, -value.Low);
    private static Interval Add(Interval first, Interval second) => new(first.Low + second.Low, first.High + second.High);
    private static Interval Subtract(Interval first, Interval second) => Add(first, Negate(second));
    private static Interval Multiply(Interval first, Interval second)
    {
        long[] products = [first.Low * second.Low, first.Low * second.High, first.High * second.Low, first.High * second.High];
        return new Interval(products.Min(), products.Max());
    }
    private static Interval Round(double value) => new((long)Math.Round(Math.BitDecrement(value) - 1d / 4096, MidpointRounding.ToEven), (long)Math.Round(Math.BitIncrement(value) + 1d / 4096, MidpointRounding.ToEven));
    private static Interval Round(Interval value, int divisor) => new(Round(value.Low / (double)divisor).Low, Round(value.High / (double)divisor).High);
}
