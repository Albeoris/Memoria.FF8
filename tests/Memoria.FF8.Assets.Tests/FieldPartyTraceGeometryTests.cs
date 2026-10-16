using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldPartyTraceGeometryTests
{
    [Fact]
    public void ExplicitPlaneEvaluationKeepsFullCoordinatesWithoutContainment()
    {
        WalkmeshTriangle slope = new(new Vector3(0, 0, -1), new Vector3(0, 10, -6), new Vector3(10, 0, -3), -1, -1, -1);
        FieldWalkmesh mesh = Mesh(slope, Flat(40));
        Assert.Equal(-13111, FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, 0, 65538, 5));
        Assert.Equal(40, FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, 1, -100, -100));
        Assert.Null(FieldPartyTraceGeometry.Resolve(mesh, -100, -100, 40));
        Assert.Null(FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, -1, 0, 0));
        Assert.Null(FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, 2, 0, 0));
    }

    [Fact]
    public void OriginalWindingAcceptsEdgesAndSignedWordQueryButRejectsOutside()
    {
        FieldWalkmesh mesh = Mesh(Flat(40));
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(mesh, 1, 1, 40));
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(mesh, 0, 5, 40));
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(mesh, 65537, 65537, 65576));
        Assert.Null(FieldPartyTraceGeometry.Resolve(mesh, 65535, 1, 40));
        Assert.Null(FieldPartyTraceGeometry.Resolve(mesh, 8, 8, 40));
        Assert.Null(FieldPartyTraceGeometry.Resolve(new FieldWalkmesh([]), 0, 0, 0));
        WalkmeshTriangle reverse = Flat(40);
        Assert.Null(FieldPartyTraceGeometry.Resolve(Mesh(reverse with { B = reverse.C, C = reverse.B }), 1, 1, 40));
    }

    [Fact]
    public void SlopedPlaneUsesTruncationTowardZeroAndKeepsFirstEqualHeight()
    {
        WalkmeshTriangle slope = new(new Vector3(0, 0, -1), new Vector3(0, 10, -6), new Vector3(10, 0, -3), -1, -1, -1);
        // Plane height is -3.9 at (2,5); original IDIV makes it -3, tying the second flat triangle.
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(Mesh(slope, Flat(-3)), 2, 5, -3));
        Assert.Equal(1, FieldPartyTraceGeometry.Resolve(Mesh(slope, Flat(-4)), 2, 5, -4));
    }

    [Fact]
    public void OverlappingTrianglesChooseClosestZThenFirstTieWithoutMovementLocks()
    {
        FieldWalkmesh mesh = Mesh(Flat(10), Flat(20));
        Assert.Equal(1, FieldPartyTraceGeometry.Resolve(mesh, 1, 1, 18));
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(mesh, 1, 1, 15));
        Assert.True(mesh.TryPosition(1, 1, out _, out int movementTriangle, new HashSet<int> { 0 }));
        Assert.Equal(1, movementTriangle);
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(mesh, 1, 1, 10));
    }

    [Fact]
    public void OriginalInt32OverflowIsPreservedInEdgesAndPlaneNumerator()
    {
        WalkmeshTriangle large = new(new Vector3(-32768, -32768, 0), new Vector3(-32768, 32767, 0), new Vector3(32767, -32768, 0), -1, -1, -1);
        FieldWalkmesh mesh = Mesh(large);
        Assert.True(mesh.TryPosition(-30000, -30000, out _, out _));
        Assert.Null(FieldPartyTraceGeometry.Resolve(mesh, -30000, -30000, 0));

        WalkmeshTriangle high = new(new Vector3(0, 0, 30000), new Vector3(0, 1000, 30000), new Vector3(1000, 0, 30000), -1, -1, -1);
        WalkmeshTriangle low = high with { A = high.A with { Z = 0 }, B = high.B with { Z = 0 }, C = high.C with { Z = 0 } };
        // 1,000,000 * 30,000 wraps to -64,771,072; native height is -64 after signed division.
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(Mesh(high, low), 50, 50, -64));
    }

    [Fact]
    public void DegeneratePlaneUsesNativeZeroHeightButNonoriginalVerticesAreRejected()
    {
        WalkmeshTriangle degenerate = new(Vector3.Zero, new Vector3(1, 0, 9), new Vector3(2, 0, 20), -1, -1, -1);
        Assert.Equal(0, FieldPartyTraceGeometry.Resolve(Mesh(degenerate), 1, 0, 0));
        Assert.Throws<InvalidDataException>(() => FieldPartyTraceGeometry.Resolve(Mesh(Flat(0) with { A = new Vector3(0.5f, 0, 0) }), 1, 1, 0));
    }

    [Fact]
    public void SelectedOriginalCorridorArrivalUniquelySelectsTriangle130()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldWalkmesh mesh = assets.LoadField(139).Walkmesh;
        Assert.Equal(263, mesh.Triangles.Count);
        Assert.Equal(new Vector3(-3165, -5622, 8341), mesh.Triangles[130].A);
        Assert.Equal(new Vector3(-3080, -6066, 8341), mesh.Triangles[130].B);
        Assert.Equal(new Vector3(-3329, -5951, 8341), mesh.Triangles[130].C);
        Assert.Equal(130, FieldPartyTraceGeometry.Resolve(mesh, -3172, -5705, 8341));
    }

    private static FieldWalkmesh Mesh(params WalkmeshTriangle[] triangles)
    {
        return new FieldWalkmesh(triangles);
    }

    private static WalkmeshTriangle Flat(int height)
    {
        return new WalkmeshTriangle(new Vector3(0, 0, height), new Vector3(0, 10, height), new Vector3(10, 0, height), -1, -1, -1);
    }
}
