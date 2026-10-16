using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeMovementProbeTests
{
    [Theory]
    [InlineData(0, 56)]
    [InlineData(1, 72)]
    [InlineData(2, 60)]
    public void NonordinaryActorCollisionSteersSideEightOrStraightFourBeforeRetry(int collisionFan, int expectedDirection)
    {
        FieldNativeMovementRequest request = Request(0, 500, 500, 40, 64) with { IsLeader = false, ScriptControlled = true };
        int calls = 0;
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request, hasActorCollision: _ => calls++ == collisionFan);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(2, result.Proposals.Count);
        Assert.Equal(6, calls);
        Assert.Equal(expectedDirection, result.Direction);
        Assert.Equal(expectedDirection, result.Proposals[1].Direction);
        Assert.True(result.Proposals[0].FanProbes[collisionFan].ActorCollision);
        Assert.All(result.Proposals[1].FanProbes, probe => Assert.False(probe.ActorCollision));
    }

    [Fact]
    public void StraightGroundAndPlusActorContactApplyBothNativeSteeringAdjustments()
    {
        FieldNativeMovementRequest request = Request(0, 949, 500, 40, 64) with { IsLeader = false, ScriptControlled = true };
        int calls = 0;
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request, hasActorCollision: _ => calls++ == 0);
        Assert.True(result.Proposals[0].FanProbes[0].ActorCollision);
        Assert.Equal(8, result.Proposals[0].FanProbes[2].Steering);
        Assert.Equal(48, result.Proposals[1].Direction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OrdinaryLeaderCollisionOnAnyFanStopsWithoutSteeringButStillQueriesAllFansAndCenter(int contactFan)
    {
        FieldNativeMovementRequest request = Request(0, 949, 500, 40, 64);
        int count = 0;
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request, hasActorCollision: probe => count++ == contactFan);
        Assert.Equal(3, count);
        Assert.Equal(FieldNativeMovementStatus.Blocked, result.Status);
        Assert.Single(result.Proposals);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(request.Direction, result.Direction);
        Assert.Equal((request.FixedX, request.FixedY, request.FixedZ), (result.FixedX, result.FixedY, result.FixedZ));
        Assert.True(result.Proposals[0].FanProbes[contactFan].ActorCollision);
        Assert.Equal(1, result.Proposals[0].FanProbes.Count(probe => probe.ActorCollision));
        Assert.Equal(result.Proposals[0].FixedX, result.CenterProbe!.FixedX);
        Assert.Equal(0, result.EarlyRetryState);
        FieldNativeMovementResult scripted = FieldNativeMovementProbe.Resolve(Square(), request with { ScriptControlled = true }, hasActorCollision: _ => true);
        Assert.Equal(FieldNativeMovementStatus.Blocked, scripted.Status);
        Assert.Equal(16, scripted.Proposals.Count);
    }

    [Fact]
    public void SlopeUsesQuantizedNormalAndStagedAxisDivisionsBeforePlaneHeight()
    {
        WalkmeshTriangle slope = new(new Vector3(0, 0, 40), new Vector3(0, 100, 40), new Vector3(100, 0, 140), -1, -1, -1);
        FieldNativeMovementRequest request = Request(0, 20, 30, 60, 64, radius: 0);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(new FieldWalkmesh([slope]), request);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(new FieldNativeSlopeFactors(2896, 0, -2896, 2897, 4096), result.Factors);
        Assert.Equal(20 * 4096 + 2897 * 2696 / 256, result.FixedX);
        Assert.Equal(30 * 4096, result.FixedY);
        Assert.Equal(67 * 4096, result.FixedZ);
        Assert.Equal(67, result.CenterProbe!.NativeHeight);
        Assert.Single(result.Proposals);
    }

    [Theory]
    [InlineData(2696, 43136)]
    [InlineData(7929, 126864)]
    public void FlatWalkAndRunKeepOriginalFixedPointScale(int speed, int fixedDelta)
    {
        FieldNativeMovementRequest request = Request(0, 500, 500, 40, 0, speed: (short)speed);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(request.FixedX, result.FixedX);
        Assert.Equal(request.FixedY - fixedDelta, result.FixedY);
        Assert.Equal(request.FixedZ, result.FixedZ);
        Assert.Equal(4096, result.Factors!.Value.X);
        Assert.Equal(4096, result.Factors.Value.Y);
        Assert.Equal(3, result.Proposals[0].FanProbes.Count);
    }

    [Fact]
    public void RejectedFanEnvelopePreservesPositionButCenterChangesActorTriangle()
    {
        FieldNativeMovementRequest request = Request(0, 960, 960, 40, 96);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request);
        Assert.Equal(FieldNativeMovementStatus.Blocked, result.Status);
        Assert.Equal(request.FixedX, result.FixedX);
        Assert.Equal(request.FixedY, result.FixedY);
        Assert.Equal(request.FixedZ, result.FixedZ);
        Assert.Equal(1, result.Triangle);
        Assert.Equal(96, result.Direction);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(8, result.Proposals[0].FanProbes[0].Steering);
        Assert.Equal(8, result.Proposals[0].FanProbes[1].Steering);
        Assert.Equal(0, result.CenterProbe!.Steering);
    }

    [Theory]
    [InlineData(0, 16, 17)]
    [InlineData(1, 2, 3)]
    public void OrdinaryLeaderStraightWallRetriesWithoutTurningAndKeepsLastProposal(int earlyByte, int proposalCount, int retryCount)
    {
        FieldNativeMovementRequest request = Request(0, 949, 500, 40, 64) with { EarlyRetryState = (byte)earlyByte };
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request);
        Assert.Equal(FieldNativeMovementStatus.Blocked, result.Status);
        Assert.Equal(proposalCount, result.Proposals.Count);
        Assert.Equal(retryCount, result.RetryCount);
        Assert.Equal(64, result.Direction);
        Assert.Equal(0, result.EarlyRetryState);
        Assert.Equal(request.FixedX, result.FixedX);
        Assert.Equal(1, result.Triangle);
        Assert.All(result.Proposals, proposal =>
        {
            Assert.Equal(0, proposal.FanProbes[0].Steering);
            Assert.Equal(0, proposal.FanProbes[1].Steering);
            Assert.Equal(8, proposal.FanProbes[2].Steering);
        });
        Assert.Equal(1, result.Proposals[1].FanProbes[0].TraversedTriangles[0]);
        Assert.Equal(0, result.Proposals[1].FanProbes[1].TraversedTriangles[0]);
        Assert.Equal(0, result.Proposals[1].FanProbes[2].TraversedTriangles[0]);
        Assert.Equal(result.Proposals[^1].FixedX, result.CenterProbe!.FixedX);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NonordinaryControlSteersFromStraightThenSideWallWithByteDirections(bool isLeader, bool scripted)
    {
        FieldNativeMovementRequest request = Request(0, 949, 500, 40, 64) with { IsLeader = isLeader, ScriptControlled = scripted };
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(Square(), request);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(64, result.Proposals[0].Direction);
        Assert.Equal(56, result.Proposals[1].Direction);
        Assert.Equal(16, result.Direction);
        Assert.Equal(7, result.RetryCount);
        Assert.Equal(request.FixedX + FieldNativeWalkmeshProbe.Sine(16) * 2696 / 256, result.FixedX);
        Assert.Equal(request.FixedY - FieldNativeWalkmeshProbe.Sine(80) * 2696 / 256, result.FixedY);
    }

    [Fact]
    public void GroundProbeRetainsLockedEdgeSteeringAndExtrapolatedHeight()
    {
        FieldWalkmesh mesh = Square();
        FieldNativeGroundProbe negative = FieldNativeMovementProbe.Probe(mesh, 0, 800 * 4096, 800 * 4096, -4096, 0, new HashSet<int> { 1 });
        Assert.True(negative.Supported);
        Assert.Equal(-8, negative.Steering);
        Assert.Equal(0, negative.Triangle);
        Assert.Equal(40, negative.NativeHeight);
        FieldNativeGroundProbe positive = FieldNativeMovementProbe.Probe(mesh, 0, 800 * 4096, 800 * 4096, 4096, 0, new HashSet<int> { 1 });
        Assert.Equal(8, positive.Steering);
        FieldNativeGroundProbe crossed = FieldNativeMovementProbe.Probe(mesh, 0, 800 * 4096, 800 * 4096, 4096, 0, new HashSet<int> { 0 });
        Assert.Equal(1, crossed.Triangle);
        Assert.Equal(new[] { 0, 1 }, crossed.TraversedTriangles);
        Assert.Equal(0, crossed.Steering);
    }

    [Fact]
    public void SelectedPcPrecision53ResolvesTheFormerCrossModeRoundingBoundary()
    {
        // Normal cross/256 = (6,0,-413), normalized X =59.4997746399... .
        //The existing PC027F profile selects precision53, which rounds to59;
        //the unselected precision64 alternative would double-round to60.
        WalkmeshTriangle rounding = new(Vector3.Zero, new Vector3(0, 256, 0), new Vector3(413, 0, 6), -1, -1, -1);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(new FieldWalkmesh([rounding]), Request(0, 20, 20, 0, 64, radius: 0));
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(59, result.Factors!.Value.NormalX);
    }

    [Fact]
    public void DegenerateGeometryAndUnknownContactsRemainUnsupported()
    {
        WalkmeshTriangle tiny = new(Vector3.Zero, new Vector3(0, 10, 0), new Vector3(10, 0, 0), -1, -1, -1);
        Assert.Equal(FieldNativeMovementStatus.Unsupported, FieldNativeMovementProbe.Resolve(new FieldWalkmesh([tiny]), Request(0, 2, 2, 0, 0)).Status);
        Assert.Equal(FieldNativeMovementStatus.Unsupported, FieldNativeMovementProbe.Resolve(Square(), Request(0, 500, 500, 40, 0) with { ActorEnvelopesClear = false }).Status);
        FieldWalkmesh cycle = new([Square().Triangles[0] with { NeighborB = 0 }]);
        FieldNativeGroundProbe cyclic = FieldNativeMovementProbe.Probe(cycle, 0, 800 * 4096, 800 * 4096, 0, 0);
        Assert.False(cyclic.Supported);
        Assert.Contains("cycle", cyclic.UnsupportedReason!);
        FieldWalkmesh fractional = new([Square().Triangles[0] with { A = new Vector3(0.5f, 0, 40) }]);
        Assert.Equal(FieldNativeMovementStatus.Unsupported, FieldNativeMovementProbe.Resolve(fractional, Request(0, 500, 500, 40, 0)).Status);
    }

    [Fact]
    public void NativeMagicAdditionAdmitsACommonDoubleRoundedResultWithoutTheOldErrorBand()
    {
        // 341.500080116... rounds to342 directly and through the1/2048
        // magic-add grid. A symmetric error band incorrectly included341.
        WalkmeshTriangle slope = new(Vector3.Zero, new Vector3(0, 256, 0), new Vector3(251, 0, 21), -1, -1, -1);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(new FieldWalkmesh([slope]), Request(0, 20, 20, 0, 64, radius: 0));
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(342, result.Factors!.Value.NormalX);
        Assert.Equal(0, result.Factors.Value.NormalY);
        Assert.Equal(-4082, result.Factors.Value.NormalZ);
    }

    [Fact]
    public void OriginalDolletLandingExactStoredHalfNormalProducesTheNativeGroundProposal()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldWalkmesh mesh = assets.LoadField(339).Walkmesh;
        Assert.Equal(new Vector3(923, -1438, 218), mesh.Triangles[89].A);
        Assert.Equal(new Vector3(878, -1937, 207), mesh.Triangles[89].B);
        Assert.Equal(new Vector3(721, -1576, 186), mesh.Triangles[89].C);
        // Actual source93 pre-step body, with independently clear contact input
        // for this pure geometry regression. Root validates real actor contacts.
        FieldNativeMovementRequest request = new(89, 3203978, -7022544, 794624, 136, 10676, 48, false, true, 0, true);
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(mesh, request, hasActorCollision: _ => false);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(new FieldNativeSlopeFactors(615, 33, -4050, 4050, 4096), result.Factors);
        Assert.Equal((3171033, -6855023, 790528, 89, (byte)136), (result.FixedX, result.FixedY, result.FixedZ, result.Triangle, result.Direction));
        FieldNativeMovementProposal proposal = Assert.Single(result.Proposals);
        Assert.Equal(new[] { 189, 197, 192 }, proposal.FanProbes.Select(probe => probe.NativeHeight!.Value));
        Assert.All(proposal.FanProbes, probe => Assert.True(probe.Supported && probe.Steering == 0 && !probe.ActorCollision));
        Assert.Equal(193, result.CenterProbe!.NativeHeight);
        Assert.Equal(1, result.RetryCount);
        Assert.Null(result.UnsupportedReason);
    }

    [Fact]
    public void OriginalCorridorJoinGeometryMatchesPreviouslyProvedNativeStepAndFans()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldWalkmesh mesh = assets.LoadField(139).Walkmesh;
        FieldNativeMovementRequest request = Request(3, 30, -6414, 8341, 255) with { IsLeader = false };
        FieldNativeMovementResult result = FieldNativeMovementProbe.Resolve(mesh, request);
        Assert.Equal(FieldNativeMovementStatus.Accepted, result.Status);
        Assert.Equal(new FieldNativeWalkmeshPosition(121827, -26314858, 8341, 47), new FieldNativeWalkmeshPosition(result.FixedX, result.FixedY, result.FixedZ / 4096, result.Triangle));
        Assert.Equal(new[] { 48, 38, 49 }, result.Proposals[0].FanProbes.Select(probe => probe.Triangle));
        Assert.Equal(255, result.Direction);
        Assert.Null(result.UnsupportedReason);
    }

    private static FieldNativeMovementRequest Request(int triangle, int x, int y, int z, byte direction, short speed = 2696, ushort radius = 48)
    {
        return new FieldNativeMovementRequest(triangle, x * 4096, y * 4096, z * 4096, direction, speed, radius, true, false, 0, true);
    }

    private static FieldWalkmesh Square()
    {
        WalkmeshTriangle first = new(new Vector3(0, 0, 40), new Vector3(0, 1000, 40), new Vector3(1000, 0, 40), -1, 1, -1);
        WalkmeshTriangle second = new(new Vector3(0, 1000, 40), new Vector3(1000, 1000, 40), new Vector3(1000, 0, 40), -1, -1, 0);
        return new FieldWalkmesh([first, second]);
    }
}
