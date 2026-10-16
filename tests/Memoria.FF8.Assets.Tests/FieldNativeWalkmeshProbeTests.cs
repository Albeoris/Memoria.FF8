using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeWalkmeshProbeTests
{
    [Fact]
    public void AdjacencyCrossesOriginalEdgeAndChecksDestinationLockOnly()
    {
        FieldWalkmesh mesh = Square();
        FieldNativeWalkmeshPosition? result = FieldNativeWalkmeshProbe.Resolve(mesh, 0, 8 * 4096, 8 * 4096, new HashSet<int> { 0 });
        Assert.Equal(new FieldNativeWalkmeshPosition(8 * 4096, 8 * 4096, 40, 1), result);
        Assert.Null(FieldNativeWalkmeshProbe.Resolve(mesh, 0, 8 * 4096, 8 * 4096, new HashSet<int> { 1 }));
        Assert.Equal(0, FieldNativeWalkmeshProbe.Resolve(mesh, 0, 5 * 4096, 5 * 4096)?.Triangle);
        Assert.Null(FieldNativeWalkmeshProbe.Resolve(mesh, 0, -4096, 4096));
    }

    [Fact]
    public void FixedCoordinatesTruncateTowardZeroAndPlaneKeepsNativeArithmetic()
    {
        Assert.Equal(0, FieldNativeWalkmeshProbe.Resolve(Square(), 0, -4095, 4096)?.Triangle);
        WalkmeshTriangle slope = new(new Vector3(0, 0, -1), new Vector3(0, 10, -6), new Vector3(10, 0, -3), -1, -1, -1);
        FieldWalkmesh mesh = new([slope]);
        Assert.Equal(-3, FieldNativeWalkmeshProbe.Resolve(mesh, 0, 2 * 4096, 5 * 4096)?.NativeHeight);
        Assert.Null(FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 0, 2 * 4096, 5 * 4096, 0, 0));
    }

    [Fact]
    public void AllThreeRadiusProbesMustBeClearAndFlat()
    {
        FieldWalkmesh mesh = Square();
        Assert.Equal(1, FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 0, 8 * 4096, 8 * 4096, 2, 0)?.Triangle);
        Assert.Equal(0, FieldNativeWalkmeshProbe.Resolve(mesh, 0, 4096, 4096)?.Triangle);
        Assert.Null(FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 0, 4096, 4096, 2, 0));
        WalkmeshTriangle second = mesh.Triangles[1];
        FieldWalkmesh differentHeight = new([mesh.Triangles[0], second with { A = second.A with { Z = 41 }, B = second.B with { Z = 41 }, C = second.C with { Z = 41 } }]);
        Assert.Null(FieldNativeWalkmeshProbe.ResolveFlatStep(differentHeight, 0, 8 * 4096, 8 * 4096, 2, 0));
    }

    [Fact]
    public void OriginalTrigValuesAndMalformedAdjacencyStayBounded()
    {
        Assert.Equal(100, FieldNativeWalkmeshProbe.Sine(1));
        Assert.Equal(2896, FieldNativeWalkmeshProbe.Sine(32));
        Assert.Equal(4094, FieldNativeWalkmeshProbe.Sine(63));
        Assert.Equal(4096, FieldNativeWalkmeshProbe.Sine(64));
        Assert.Equal(-100, FieldNativeWalkmeshProbe.Sine(255));
        Assert.Equal(100, FieldNativeWalkmeshProbe.Sine(257));
        WalkmeshTriangle triangle = Square().Triangles[0] with { NeighborB = 0 };
        Assert.Null(FieldNativeWalkmeshProbe.Resolve(new FieldWalkmesh([triangle]), 0, 8 * 4096, 8 * 4096));
    }

    [Fact]
    public void SelectedCorridorJoinStepTraversesStaleTriangleAndOriginalFanNeighbors()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldWalkmesh mesh = assets.LoadField(139).Walkmesh;
        int x = 30 * 4096 + FieldNativeWalkmeshProbe.Sine(255) * 2696 / 256;
        int y = -6414 * 4096 - FieldNativeWalkmeshProbe.Sine(255 + 64) * 2696 / 256;
        Assert.Equal(121827, x);
        Assert.Equal(-26314858, y);
        Assert.Equal(new FieldNativeWalkmeshPosition(x, y, 8341, 47), FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 3, x, y, 48, 255));
        Assert.Equal(48, FieldNativeWalkmeshProbe.Resolve(mesh, 3, x + 48 * FieldNativeWalkmeshProbe.Sine(31), y - 48 * FieldNativeWalkmeshProbe.Sine(95))?.Triangle);
        Assert.Equal(38, FieldNativeWalkmeshProbe.Resolve(mesh, 3, x + 48 * FieldNativeWalkmeshProbe.Sine(223), y - 48 * FieldNativeWalkmeshProbe.Sine(287))?.Triangle);
        Assert.Equal(49, FieldNativeWalkmeshProbe.Resolve(mesh, 3, x + 48 * FieldNativeWalkmeshProbe.Sine(255), y - 48 * FieldNativeWalkmeshProbe.Sine(319))?.Triangle);
        Assert.Null(FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 3, x, y, 48, 255, new HashSet<int> { 48 }));
        int nextX = x + FieldNativeWalkmeshProbe.Sine(255) * 2696 / 256;
        int nextY = y - FieldNativeWalkmeshProbe.Sine(319) * 2696 / 256;
        Assert.Equal(new FieldNativeWalkmeshPosition(nextX, nextY, 8341, 47), FieldNativeWalkmeshProbe.ResolveFlatStep(mesh, 47, nextX, nextY, 48, 255));
    }

    private static FieldWalkmesh Square()
    {
        WalkmeshTriangle first = new(new Vector3(0, 0, 40), new Vector3(0, 10, 40), new Vector3(10, 0, 40), -1, 1, -1);
        WalkmeshTriangle second = new(new Vector3(0, 10, 40), new Vector3(10, 10, 40), new Vector3(10, 0, 40), -1, -1, 0);
        return new FieldWalkmesh([first, second]);
    }
}
