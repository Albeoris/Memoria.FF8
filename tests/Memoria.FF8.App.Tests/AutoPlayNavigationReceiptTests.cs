using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayNavigationReceiptTests
{
    [Fact]
    public void PredicateStatesSeparateTheSamePhysicalComponentWithoutChangingLegacyKeys()
    {
        AutoPlayNavigationReceipt physical = Capture(CreateWalkmesh(), 0, []);
        AutoPlayNavigationReceipt closed = physical with { StateFingerprint = new string('A', 64) };
        AutoPlayNavigationReceipt open = physical with { StateFingerprint = new string('B', 64) };
        AutoPlayNavigationReceipt restored = System.Text.Json.JsonSerializer.Deserialize<AutoPlayNavigationReceipt>("{\"Version\":1,\"FieldId\":600,\"ProgramHash\":\"program\",\"WalkmeshHash\":\"walkmesh\",\"LockedTriangles\":[],\"ReachableTriangles\":[0,1]}")!;

        Assert.Null(restored.StateFingerprint);
        Assert.Equal(physical.Key("600:16"), restored.Key("600:16"));
        Assert.NotEqual(closed.Key("600:16"), open.Key("600:16"));
        Assert.StartsWith(physical.Key("600:16") + ":state:", closed.Key("600:16"));
        Assert.Equal(physical.ReachableTriangles, closed.ReachableTriangles);
        Assert.Equal(closed.Key("600:16"), (closed with { }).Key("600:16"));
    }

    [Fact]
    public void BranchEvidencePromotesOnlyFieldsWithDifferentPredicatesAtTheSamePhysicalSource()
    {
        AutoPlayNavigationReceipt physical = Capture(CreateWalkmesh(), 0, []);
        string source = physical.Key("600:16");
        AutoPlayPlannerState coverage = new();
        Dictionary<string, AutoPlayNavigationReceipt> receipts = new() { [source] = physical };
        AutoPlayEventRoute first = new("destination", "closed", false, []);
        coverage.NavigationRoutes.Add(new(source, "destination", "exit", first));
        Assert.Empty(AutoPlayController.FindStatefulNavigationFields(coverage, receipts));
        coverage.NavigationRoutes.Add(new(source, "destination", "other-exit", first with { ConditionSignature = "open" }));
        Assert.Empty(AutoPlayController.FindStatefulNavigationFields(coverage, receipts));
        coverage.NavigationRoutes.Add(new(source, "destination", "exit", first with { ConditionSignature = "open" }));
        Assert.Equal(new[] { 600 }, AutoPlayController.FindStatefulNavigationFields(coverage, receipts));
        receipts[source] = physical with { StateFingerprint = new string('A', 64) };
        coverage.NavigationRoutes.Clear();
        Assert.Equal(new[] { 600 }, AutoPlayController.FindStatefulNavigationFields(coverage, receipts));
    }

    [Fact]
    public void TransitSharesStateOnlyAcrossAnObservedRouteWithMatchingPersistentReads()
    {
        AutoPlayNavigationReceipt physical = Capture(CreateWalkmesh(), 0, []);
        AutoPlayNavigationReceipt stairs = physical with { StateFingerprint = new string('A', 64) };
        AutoPlayNavigationReceipt transit = physical with { FieldId = 601 };
        string source = stairs.Key("600:16");
        string destination = transit.Key("601:16");
        AutoPlayPlannerState coverage = new();
        coverage.NavigationRoutes.Add(new(source, destination, "exit", new("601:16", "open", false, [])));
        Dictionary<string, AutoPlayNavigationReceipt> receipts = new() { [source] = stairs, [destination] = transit };
        AutoPlayVisitGuard guard = new(AutoPlayVisitGuard.CurrentVersion, stairs.FieldId, stairs.ProgramHash, [new NativePersistentPredicateRead("PSHM_B", 300, 1)], "shared");
        Dictionary<string, AutoPlayVisitGuard> guards = new() { [source] = guard, [destination] = guard with { FieldId = transit.FieldId, Fingerprint = "different" } };

        Assert.Equal(new[] { 600 }, AutoPlayController.FindStatefulNavigationFields(coverage, receipts, guards));
        guards[destination] = guards[destination] with { Fingerprint = guard.Fingerprint };
        Assert.Equal(new[] { 600, 601 }, AutoPlayController.FindStatefulNavigationFields(coverage, receipts, guards).Order());
        coverage.NavigationRoutes.Clear();
        Assert.Equal(new[] { 600 }, AutoPlayController.FindStatefulNavigationFields(coverage, receipts, guards));
    }

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
    public void TheOccupiedLockedTriangleCanBeLeftButCannotBeEnteredAgainFromItsUnlockedNeighbor()
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();
        HashSet<int> locked = [0];

        AutoPlayNavigationReceipt occupied = GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, 0, locked);
        AutoPlayNavigationReceipt exited = GameWorld.CaptureAutoPlayNavigationReceipt(600, "program", "walkmesh", walkmesh, 1, locked);

        Assert.Equal(new[] { 0, 1 }, occupied.ReachableTriangles);
        Assert.Equal(new[] { 1 }, exited.ReachableTriangles);
        Assert.NotEqual(occupied.Key("600:16"), exited.Key("600:16"));
        Assert.Equal(new[] { 0 }, locked);
    }

    [Fact]
    public void AnUnoccupiedLockedTriangleCannotBridgeTwoOtherwiseDisconnectedReachableRegions()
    {
        FieldWalkmesh walkmesh = new(
        [
            new(Vector3.Zero, new Vector3(200, 0, 0), new Vector3(0, 200, 0), -1, 1, -1),
            new(new Vector3(200, 0, 0), new Vector3(200, 200, 0), new Vector3(0, 200, 0), 2, -1, 0),
            new(new Vector3(200, 0, 0), new Vector3(400, 0, 0), new Vector3(200, 200, 0), -1, -1, 1)
        ]);

        Assert.Equal(new[] { 0 }, Capture(walkmesh, 0, [1]).ReachableTriangles);
        Assert.Equal(new[] { 2 }, Capture(walkmesh, 2, [1]).ReachableTriangles);
        Assert.Equal(new[] { 0, 1, 2 }, Capture(walkmesh, 0, []).ReachableTriangles);
    }

    [Fact]
    public void ACompletelyEnclosedLockedStartRetainsOnlyTheActualOccupiedTriangle()
    {
        AutoPlayNavigationReceipt receipt = Capture(CreateWalkmesh(), 0, [0, 1]);

        Assert.Equal(new[] { 0 }, receipt.ReachableTriangles);
        Assert.Equal(new[] { 0, 1 }, receipt.LockedTriangles);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void AnInvalidStartingTriangleStillRefusesWithoutSelectingAnUnlockedComponent(int start)
    {
        FieldWalkmesh walkmesh = CreateWalkmesh();

        Assert.Empty(GameWorld.AutoPlayReachableTriangles(walkmesh, start, new HashSet<int>()));
        Assert.Throws<NotSupportedException>(() => Capture(walkmesh, start, []));
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
