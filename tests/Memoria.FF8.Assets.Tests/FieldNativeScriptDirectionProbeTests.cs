using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldNativeScriptDirectionProbeTests
{
    [Fact]
    public void OriginalCavernDirectMoveIgnoresEveryPossiblePriorBodyAndMovementByte()
    {
        for (int prior = 0; prior <= byte.MaxValue; prior++)
        {
            FieldNativeScriptDirectionRequest request = new(45 * 4096, 4006 * 4096, -182 * 4096, 5022 * 4096, 7929, 0, 48, (byte)(255 - prior), (byte)prior, 0, 0);
            FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(request);
            Assert.Equal(FieldNativeScriptDirectionStatus.Moving, result.Status);
            Assert.Equal(137, result.Direction);
            Assert.Equal(0, result.TurnAccumulator);
        }
    }

    [Theory]
    [InlineData(0, -1000, 0)]
    [InlineData(1000, -1000, 32)]
    [InlineData(1000, 0, 64)]
    [InlineData(1000, 1000, 96)]
    [InlineData(0, 1000, 128)]
    [InlineData(-1000, 1000, 160)]
    [InlineData(-1000, 0, 192)]
    [InlineData(-1000, -1000, 224)]
    public void OriginalLookupRetainsAllEightOctants(int targetX, int targetY, byte desired)
    {
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(Request(targetX, targetY));
        Assert.Equal(FieldNativeScriptDirectionStatus.Moving, result.Status);
        Assert.Equal(desired, result.DesiredDirection);
        Assert.Equal(desired, result.Direction);
        Assert.Equal(0, result.TurnAccumulator);
    }

    [Theory]
    [InlineData(1000, 0, 4, 4)]
    [InlineData(-1000, 128, 124, -4)]
    public void HalfTurnTiesKeepTheirOriginalSignedArc(int targetY, byte facing, byte direction, short accumulator)
    {
        FieldNativeScriptDirectionRequest request = Request(0, targetY) with { FacingDirection = facing, DirectionLimit = 4 };
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(direction, result.Direction);
        Assert.Equal(accumulator, result.TurnAccumulator);
    }

    [Theory]
    [InlineData(124, 11)]
    [InlineData(125, 7)]
    [InlineData(128, 7)]
    [InlineData(131, 7)]
    [InlineData(132, 3)]
    public void ExactLimitChangesAccumulatorWhileStrictlySmallerDifferenceDoesNot(byte facing, short accumulator)
    {
        FieldNativeScriptDirectionRequest request = Request(0, 1000) with { FacingDirection = facing, DirectionLimit = 4, TurnAccumulator = 7 };
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(128, result.Direction);
        Assert.Equal(accumulator, result.TurnAccumulator);
    }

    [Fact]
    public void ByteDirectionWrapAndSignedAccumulatorEscapeRemainDistinct()
    {
        FieldNativeScriptDirectionRequest request = Request(0, -1000) with { FacingDirection = 252, DirectionLimit = 4, TurnAccumulator = 256 };
        FieldNativeScriptDirectionResult first = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(0, first.Direction);
        Assert.Equal(260, first.TurnAccumulator);
        FieldNativeScriptDirectionResult escaped = FieldNativeScriptDirectionProbe.Resolve(request with { FacingDirection = 128, TurnAccumulator = first.TurnAccumulator });
        Assert.Equal(0, escaped.Direction);
        Assert.Equal(260, escaped.TurnAccumulator);
        FieldNativeScriptDirectionResult negative = FieldNativeScriptDirectionProbe.Resolve(request with { FacingDirection = 4, TurnAccumulator = -256 });
        Assert.Equal(0, negative.Direction);
        Assert.Equal(-260, negative.TurnAccumulator);
        FieldNativeScriptDirectionResult signedWord = FieldNativeScriptDirectionProbe.Resolve(request with { FacingDirection = 4, TurnAccumulator = unchecked((short)65532) });
        Assert.Equal(-8, signedWord.TurnAccumulator);
    }

    [Theory]
    [InlineData(257)]
    [InlineData(-257)]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    public void OutsideSignedAccumulatorGateChoosesDirectDirectionAndPreservesWord(short accumulator)
    {
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(Request(0, 1000) with { TurnAccumulator = accumulator, DirectionLimit = 4 });
        Assert.Equal(128, result.Direction);
        Assert.Equal(accumulator, result.TurnAccumulator);
    }

    [Fact]
    public void DistanceRadiusGateIsStrictAndPrecedesTurnLimit()
    {
        FieldNativeScriptDirectionRequest request = Request(0, 100) with { Radius = 101, DirectionLimit = 4, TurnAccumulator = 7 };
        FieldNativeScriptDirectionResult inside = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(128, inside.Direction);
        Assert.Equal(7, inside.TurnAccumulator);
        FieldNativeScriptDirectionResult equal = FieldNativeScriptDirectionProbe.Resolve(request with { Radius = 100 });
        Assert.Equal(4, equal.Direction);
        Assert.Equal(11, equal.TurnAccumulator);
    }

    [Fact]
    public void StopRadiusIsInclusiveSignedAndDoesNotSnapOrChangeDirectionState()
    {
        FieldNativeScriptDirectionRequest request = Request(144, 64) with { Radius = 48, StopDistance = 96, MovementDirection = 17, TurnAccumulator = 7, DirectionLimit = 4 };
        FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(24832, result.DistanceSquared);
        Assert.Equal(FieldNativeScriptDirectionStatus.Stopped, result.Status);
        Assert.Equal(request.FixedX, result.FixedX);
        Assert.Equal(request.FixedY, result.FixedY);
        Assert.Equal(17, result.Direction);
        Assert.Equal(7, result.TurnAccumulator);
        Assert.Null(result.DesiredDirection);
        Assert.Equal(FieldNativeScriptDirectionStatus.Moving, FieldNativeScriptDirectionProbe.Resolve(request with { TargetY = 65 * 4096 }).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Stopped, FieldNativeScriptDirectionProbe.Resolve(Request(64, 0) with { Radius = 48, StopDistance = -48 }).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Moving, FieldNativeScriptDirectionProbe.Resolve(Request(64, 0)).Status);
    }

    [Fact]
    public void SpeedSnapUsesStrictIntegerThresholdAndPreservesOnlyItsXYContract()
    {
        FieldNativeScriptDirectionRequest request = Request(9, 3) with { MovementDirection = 17, TurnAccumulator = -7, DirectionLimit = 4, DirectionOffset = 255 };
        FieldNativeScriptDirectionResult snapped = FieldNativeScriptDirectionProbe.Resolve(request);
        Assert.Equal(FieldNativeScriptDirectionStatus.Snapped, snapped.Status);
        Assert.Equal(request.TargetX, snapped.FixedX);
        Assert.Equal(request.TargetY, snapped.FixedY);
        Assert.Equal(17, snapped.Direction);
        Assert.Equal(-7, snapped.TurnAccumulator);
        Assert.Equal(FieldNativeScriptDirectionStatus.Moving, FieldNativeScriptDirectionProbe.Resolve(Request(9, 4)).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Moving, FieldNativeScriptDirectionProbe.Resolve(Request(2, 0) with { Speed = 512 }).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Snapped, FieldNativeScriptDirectionProbe.Resolve(Request(1, 1) with { Speed = 0 }).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Snapped, FieldNativeScriptDirectionProbe.Resolve(Request(9, 3) with { Speed = -2500 }).Status);
        Assert.Equal(FieldNativeScriptDirectionStatus.Stopped, FieldNativeScriptDirectionProbe.Resolve(request with { StopDistance = 1 }).Status);
    }

    [Fact]
    public void DistanceUsesSignedArithmeticShiftAndLookupSupportsItsShortDiagonalTail()
    {
        FieldNativeScriptDirectionResult shifted = FieldNativeScriptDirectionProbe.Resolve(Request(2, 0) with { FixedX = -1, FixedY = -4097, Speed = 0 });
        Assert.Equal(13, shifted.DistanceSquared);
        Assert.Equal(3, shifted.Distance);
        Assert.Equal(94, shifted.Direction);
        FieldNativeScriptDirectionResult shortDiagonal = FieldNativeScriptDirectionProbe.Resolve(Request(2, 2) with { Speed = 0 });
        Assert.Equal(8, shortDiagonal.DistanceSquared);
        Assert.Equal(2, shortDiagonal.Distance);
        Assert.Equal(96, shortDiagonal.Direction);
    }

    [Fact]
    public void UnprovedOverflowAndNonzeroOffsetRefuseWithoutChangingState()
    {
        FieldNativeScriptDirectionRequest overflow = Request(0, 0) with { FixedX = int.MinValue, TargetX = int.MaxValue, MovementDirection = 17, TurnAccumulator = 7 };
        FieldNativeScriptDirectionResult refused = FieldNativeScriptDirectionProbe.Resolve(overflow);
        Assert.Equal(FieldNativeScriptDirectionStatus.Unsupported, refused.Status);
        Assert.Equal(overflow.FixedX, refused.FixedX);
        Assert.Equal(17, refused.Direction);
        Assert.Equal(7, refused.TurnAccumulator);
        Assert.Null(refused.DistanceSquared);
        Assert.Contains("overflow", refused.UnsupportedReason!);
        FieldNativeScriptDirectionResult offset = FieldNativeScriptDirectionProbe.Resolve(Request(0, 1000) with { DirectionOffset = 1 });
        Assert.Equal(FieldNativeScriptDirectionStatus.Unsupported, offset.Status);
        Assert.Contains("574", offset.UnsupportedReason!);
    }

    [Fact]
    public void OriginalLobbyConditionalFirstDirectionsAndTerrainProposalsKeepBlockedTurns()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(Directory.Exists(directory), "Installed selected field corpus is required.");
        using GameAssets assets = new(directory);
        FieldWalkmesh mesh = assets.LoadField(165).Walkmesh;
        (int X, int Y, int Triangle, int TargetX, int TargetY, int Distance, byte Desired, byte Direction, short Accumulator, int ProposalX, int ProposalY, FieldNativeMovementStatus Status, int[] FanTriangles, int[] Steering)[] cases =
        [
            (2407, -5740, 115, 1736, -5899, 689, 201, 252, -4, 9855156, -23550844, FieldNativeMovementStatus.Blocked, [115, 115, 115], [-8, 8, 8]),
            (410, -11448, 324, 413, -9146, 2302, 128, 4, 4, 1683276, -46930812, FieldNativeMovementStatus.Accepted, [253, 319, 319], [0, 0, 0]),
            (-2864, -5474, 212, 413, -9146, 4921, 30, 4, 4, -11727028, -22461308, FieldNativeMovementStatus.Blocked, [212, 212, 212], [-8, 8, -8])
        ];
        foreach (var value in cases)
        {
            FieldNativeScriptDirectionRequest request = Request(value.TargetX, value.TargetY) with { FixedX = value.X * 4096, FixedY = value.Y * 4096, Radius = 48, DirectionLimit = 4 };
            FieldNativeScriptDirectionResult direction = FieldNativeScriptDirectionProbe.Resolve(request);
            Assert.Equal(FieldNativeScriptDirectionStatus.Moving, direction.Status);
            Assert.Equal(value.Distance, direction.Distance);
            Assert.Equal(value.Desired, direction.DesiredDirection);
            Assert.Equal(value.Direction, direction.Direction);
            Assert.Equal(value.Accumulator, direction.TurnAccumulator);
            FieldNativeMovementRequest movement = new(value.Triangle, request.FixedX, request.FixedY, 567 * 4096, direction.Direction, request.Speed, request.Radius, false, true, 0, true);
            FieldNativeMovementResult ground = FieldNativeMovementProbe.Resolve(mesh, movement);
            Assert.Equal(value.Status, ground.Status);
            Assert.Single(ground.Proposals);
            Assert.Equal(value.ProposalX, ground.Proposals[0].FixedX);
            Assert.Equal(value.ProposalY, ground.Proposals[0].FixedY);
            Assert.Equal(value.FanTriangles, ground.Proposals[0].FanProbes.Select(probe => probe.Triangle));
            Assert.Equal(value.Steering, ground.Proposals[0].FanProbes.Select(probe => probe.Steering));
            Assert.All(ground.Proposals[0].FanProbes, probe => Assert.Equal(567, probe.NativeHeight));
            if (ground.Status == FieldNativeMovementStatus.Blocked)
            {
                Assert.Equal(request.FixedX, ground.FixedX);
                Assert.Equal(request.FixedY, ground.FixedY);
                Assert.Equal(value.Direction, ground.Direction);
            }
        }
    }

    private static FieldNativeScriptDirectionRequest Request(int targetX, int targetY)
    {
        return new FieldNativeScriptDirectionRequest(0, 0, targetX * 4096, targetY * 4096, 2500, 0, 0, 0, 0, 0, 0);
    }
}
