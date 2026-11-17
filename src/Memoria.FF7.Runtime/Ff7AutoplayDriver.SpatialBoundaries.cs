using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private long spatialBoundaryGeneration = -1;
    private HashSet<int> spatialBoundaryTriangles = [];
    private int? admittedSpatialTriangle;

    public void ConfigureSpatialBoundaries(IReadOnlyList<Ff7AutoPlayTarget> targets, Ff7AutoPlayTarget? selectedTarget = null)
    {
        HashSet<int> boundaries = ReadSpatialBoundaryTriangles(targets);
        if (spatialBoundaryGeneration != session.FieldGeneration || !spatialBoundaryTriangles.SetEquals(boundaries))
        {
            waypointGeneration = -1;
        }
        spatialBoundaryGeneration = session.FieldGeneration;
        spatialBoundaryTriangles = boundaries;
        admittedSpatialTriangle = selectedTarget is { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, TriangleOnly: true, Triangle: int triangle } && spatialBoundaryTriangles.Contains(triangle) ? triangle : null;
    }

    internal static HashSet<int> ReadSpatialBoundaryTriangles(IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        return targets.Where(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.TriangleOnly && target.Triangle is not null).Select(target => target.Triangle!.Value).ToHashSet();
    }

    internal static bool AllowsSpatialMove(Ff7Actor from, Ff7Actor to, IReadOnlySet<int> boundaryTriangles, int? admittedTriangle)
    {
        return from.Triangle == to.Triangle || !boundaryTriangles.Contains(to.Triangle) || to.Triangle == admittedTriangle;
    }

    public int[] CaptureSpatialBoundaryTriangles()
    {
        return spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles.Order().ToArray() : [];
    }

    private string WithSpatialBoundaryFingerprint(string fingerprint, IReadOnlySet<int>? sourceBoundaries = null)
    {
        return ReadSpatialBoundaryFingerprint(fingerprint, sourceBoundaries ?? (spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles : new HashSet<int>()));
    }

    internal static string ReadSpatialBoundaryFingerprint(string fingerprint, IReadOnlySet<int> boundaryTriangles)
    {
        string facts = $"{fingerprint}|automatic-triangle-boundaries:1:{string.Join(',', boundaryTriangles.Order())}|approach-admission:2|native-navigation-modes:run-walk:5|navigation-invariant-write-set:4|gateway-segment-contacts:2|forward-admission-blockers:1|navigation-predicate-ranges:1|floor-contact-escape:1";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
    }
}
