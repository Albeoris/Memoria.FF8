using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationReceiptTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void DifferentStartingTrianglesInTheSameComponentHaveTheSameNavigationKey(int first, int second)
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        AutoPlayNavigationReceipt left = Capture(walkmesh, first, []);
        AutoPlayNavigationReceipt right = Capture(walkmesh, second, []);

        Assert.Equal(1, left.Version);
        Assert.Equal(600, left.FieldId);
        Assert.Equal("program", left.ProgramHash);
        Assert.Equal("walkmesh", left.WalkmeshHash);
        Assert.Equal(left.ReachableTriangles, right.ReachableTriangles);
        Assert.Equal(left.Key("600:16"), right.Key("600:16"));
    }

    [Fact]
    public void DisconnectedFloorsHaveDistinctKeysWithoutFallingBackToAnotherComponent()
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        AutoPlayNavigationReceipt lower = Capture(walkmesh, 0, []);
        AutoPlayNavigationReceipt upper = Capture(walkmesh, 2, []);

        Assert.Equal(new[] { 0, 1 }, lower.ReachableTriangles);
        Assert.Equal(new[] { 2, 3 }, upper.ReachableTriangles);
        Assert.NotEqual(lower.Key("600:16"), upper.Key("600:16"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void AChangedLockSetChangesTheKeyIncludingLocksOutsideTheCurrentComponent(int lockedTriangle, int reachableCount)
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        AutoPlayNavigationReceipt before = Capture(walkmesh, 0, []);
        AutoPlayNavigationReceipt after = Capture(walkmesh, 0, [lockedTriangle]);

        Assert.Equal(reachableCount, after.ReachableTriangles.Count);
        Assert.Equal(new[] { lockedTriangle }, after.LockedTriangles);
        Assert.NotEqual(before.Key("600:16"), after.Key("600:16"));
    }

    [Theory]
    [InlineData("changed-program", "walkmesh")]
    [InlineData("program", "changed-walkmesh")]
    public void ChangedOriginalResourceIdentityCannotReuseThePreviousNavigationKey(string programHash, string walkmeshHash)
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        AutoPlayNavigationReceipt before = Capture(walkmesh, 0, []);
        AutoPlayNavigationReceipt after = GameWorld.CaptureAutoPlayNavigationReceipt(600, programHash, walkmeshHash, walkmesh, 0, new HashSet<int>());

        Assert.Equal(before.ReachableTriangles, after.ReachableTriangles);
        Assert.NotEqual(before.Key("600:16"), after.Key("600:16"));
    }

    [Fact]
    public void ALockedStartingTriangleRefusesInsteadOfChoosingItsUnlockedNeighborOrAnotherFloor()
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        HashSet<int> locked = [0];

        Assert.Throws<NotSupportedException>(() => GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, 0, locked));

        Assert.Equal(new[] { 0 }, locked);
    }

    [Fact]
    public void CapturingSortsItsOwnedSnapshotWithoutChangingOrRetainingTheCallersMutableLockSet()
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        HashSet<int> firstLocks = [3, 2];
        HashSet<int> secondLocks = [2, 3];
        int[] originalOrder = firstLocks.ToArray();

        AutoPlayNavigationReceipt first = GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, 0, firstLocks);
        AutoPlayNavigationReceipt second = GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, 1, secondLocks);
        string key = first.Key("600:16");

        Assert.Equal(originalOrder, firstLocks);
        Assert.Equal(new[] { 2, 3 }, first.LockedTriangles);
        Assert.Equal(new[] { 0, 1 }, first.ReachableTriangles);
        Assert.Equal(key, second.Key("600:16"));
        firstLocks.Clear();
        firstLocks.Add(1);
        Assert.Equal(new[] { 2, 3 }, first.LockedTriangles);
        Assert.Equal(new[] { 0, 1 }, first.ReachableTriangles);
        Assert.Equal(key, first.Key("600:16"));
    }

    private static AutoPlayNavigationReceipt Capture(FieldWalkmesh walkmesh, int start, int[] locked)
    {
        return GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, start, locked.ToHashSet());
    }

    private static FieldWalkmesh CreateWalkmesh()
    {
        WalkmeshTriangle first = new(Vector3.Zero, new Vector3(200, 0, 0), new Vector3(0, 200, 0), 1, -1, -1);
        WalkmeshTriangle neighbor = new(new Vector3(200, 0, 0), new Vector3(200, 200, 0), new Vector3(0, 200, 0), -1, 0, -1);
        return new([first, neighbor, first with { A = new Vector3(0, 0, 500), B = new Vector3(200, 0, 500), C = new Vector3(0, 200, 500), NeighborA = 3 }, neighbor with { A = new Vector3(200, 0, 500), B = new Vector3(200, 200, 500), C = new Vector3(0, 200, 500), NeighborB = 2 }]);
    }
}
