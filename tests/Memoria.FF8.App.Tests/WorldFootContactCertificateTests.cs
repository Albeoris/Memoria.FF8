using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Pure contact-proof tests; these neither create a world entry nor advance the ordinary player.</summary>
public sealed class WorldFootContactCertificateTests
{
    [Fact]
    public void OriginalSpawnAndCurrentStoryHaveTheConditionalContactCertificate()
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame();
        WorldFootContactCertificateResult result = Evaluate(state);
        Assert.True(result.Certified);
        Assert.Null(result.Refusal);
        Assert.Null(result.Reason);
        Assert.Equal(new WorldFootContactProof(1, 17, 24576, -29406, 24576, state.Origin), result.Certificate);
    }

    [Fact]
    public void UniformStoryClauseAdmitsRetiredCarAndInactiveBoatWithoutManufacturedContactState()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with
        {
            Slot4 = new NativeSavedWorldVehicleSlot(255, new NativeSavedWorldPose(12884, -26807, -304, 2699))
        };
        string before = JsonSerializer.Serialize(saved);
        WorldFootContactCertificateResult result = WorldFootContactCertificate.Evaluate(saved, 135, 0, 0, 13000, -26700, 13000, allowTravel: true, entityType: 1);
        Assert.True(result.Certified);
        Assert.Equal(3, result.Certificate!.RuleVersion);
        Assert.Equal(135, result.Certificate.StoryWord);
        Assert.Equal(before, JsonSerializer.Serialize(saved));
        AssertRefused(WorldFootContactCertificate.Evaluate(saved, 205, 0, 0, 13000, -26700, 13000, allowTravel: true, entityType: 1), WorldFootContactRefusal.UnsupportedStory);
        AssertRefused(WorldFootContactCertificate.Evaluate(saved, 33, 0, 0, 13000, -26700, 13000, allowTravel: true, entityType: 1), WorldFootContactRefusal.UnsupportedStory);
        AssertRefused(WorldFootContactCertificate.Evaluate(saved, 135, 0, 1, 13000, -26700, 13000, allowTravel: true, entityType: 1), WorldFootContactRefusal.ActiveObjectPairs);
    }

    [Fact]
    public void UniformClauseRefusesPotentialCarPresentationAndTrainProximity()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame() with
        {
            Slot4 = new NativeSavedWorldVehicleSlot(73, new NativeSavedWorldPose(12884, -26807, -304, 2699)),
            Slot5 = new NativeSavedWorldVehicleSlot(78, new NativeSavedWorldPose(0, 0, 0, 0))
        };
        AssertRefused(WorldFootContactCertificate.Evaluate(saved, 135, 0, 0, 13000, -26700, 13000, allowTravel: true, entityType: 1), WorldFootContactRefusal.UnsupportedSavedVehicle);
        Assert.True(WorldFootContactCertificate.Evaluate(saved, 135, 0, 0, 30000, -29406, 30000, allowTravel: true, entityType: 1).Certified);
        AssertRefused(WorldFootContactCertificate.Evaluate(saved, 135, 0, 0, -30000, -26700, -30000, allowTravel: true, entityType: 1), WorldFootContactRefusal.CameraEyeOutsideRegion);
    }

    [Theory]
    [InlineData(22528, -31454, 8192)]
    [InlineData(22528, -27358, 40960)]
    [InlineData(26624, -31454, 40960)]
    [InlineData(26624, -27358, 8192)]
    public void RegionAndCameraBoundsAreInclusive(int x, int rawZ, double eyeX)
    {
        WorldFootContactCertificateResult result = Evaluate(NativeSavedWorldState.CreateNewGame(), x: x, rawZ: rawZ, eyeX: eyeX);
        Assert.True(result.Certified);
        Assert.Equal((x, rawZ, eyeX), (result.Certificate!.PlayerX, result.Certificate.PlayerRawZ, result.Certificate.CameraEyeX));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(33, 0, true)]
    [InlineData(34, 0, false)]
    [InlineData(255, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(17, 255, false)]
    public void StoryWordUsesBothBytesAndExclusiveUpperBound(byte low, byte high, bool certified)
    {
        WorldFootContactCertificateResult result = Evaluate(NativeSavedWorldState.CreateNewGame(), low: low, high: high);
        Assert.Equal(certified, result.Certified);
        if (!certified)
        {
            Assert.Equal(WorldFootContactRefusal.UnsupportedStory, result.Refusal);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(7, false)]
    [InlineData(248, true)]
    public void OnlyNativeObjectPairBitsInvalidateThisProof(byte flags, bool certified)
    {
        WorldFootContactCertificateResult result = Evaluate(NativeSavedWorldState.CreateNewGame(), flags: flags);
        Assert.Equal(certified, result.Certified);
        if (!certified)
        {
            Assert.Equal(WorldFootContactRefusal.ActiveObjectPairs, result.Refusal);
        }
    }

    [Fact]
    public void MissingOrMalformedMetadataCannotReceiveACertificate()
    {
        AssertRefused(Evaluate(null), WorldFootContactRefusal.UnknownSavedWorld);
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame();
        AssertRefused(Evaluate(state with { SchemaVersion = 2 }), WorldFootContactRefusal.InvalidSavedWorld);
        AssertRefused(Evaluate(state with { Origin = null! }), WorldFootContactRefusal.InvalidSavedWorld);
        AssertRefused(Evaluate(state with { Origin = state.Origin with { ProofVersion = 0 } }), WorldFootContactRefusal.InvalidSavedWorld);
        AssertRefused(Evaluate(state with { Origin = state.Origin with { Kind = (NativeSavedWorldOriginKind)1 } }), WorldFootContactRefusal.InvalidSavedWorld);
        AssertRefused(Evaluate(state with { Slot4 = null! }), WorldFootContactRefusal.InvalidSavedWorld);
        AssertRefused(Evaluate(state with { Slot5 = state.Slot5 with { Pose = null! } }), WorldFootContactRefusal.InvalidSavedWorld);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(73, 0)]
    [InlineData(0, 255)]
    public void EitherNonzeroSavedShapeIsOutsideTheNarrowProof(byte slot4Shape, byte slot5Shape)
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame();
        state = state with { Slot4 = state.Slot4 with { ShapeId = slot4Shape }, Slot5 = state.Slot5 with { ShapeId = slot5Shape } };
        AssertRefused(Evaluate(state), WorldFootContactRefusal.UnsupportedSavedVehicle);
    }

    [Fact]
    public void ValidInactivePosesArePreservedWithoutSyntheticVehicleOrTrainState()
    {
        NativeSavedWorldState state = NativeSavedWorldState.CreateNewGame() with
        {
            Slot4 = new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(int.MinValue, int.MaxValue, short.MinValue, ushort.MaxValue)),
            Slot5 = new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(98765, -43210, short.MaxValue, 4097))
        };
        string before = JsonSerializer.Serialize(state);
        Assert.True(Evaluate(state).Certified);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData(22527, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(26625, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(24576, -31455, WorldFootContactRefusal.PlayerZOutsideRegion)]
    [InlineData(24576, -27357, WorldFootContactRefusal.PlayerZOutsideRegion)]
    [InlineData(90112, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(-40960, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(24576, 36130, WorldFootContactRefusal.PlayerZOutsideRegion)]
    [InlineData(int.MinValue, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(int.MaxValue, -29406, WorldFootContactRefusal.PlayerXOutsideRegion)]
    [InlineData(24576, int.MinValue, WorldFootContactRefusal.PlayerZOutsideRegion)]
    [InlineData(24576, int.MaxValue, WorldFootContactRefusal.PlayerZOutsideRegion)]
    public void OneUnitOutsideAndSignedWordAliasesAreRefusedWithoutIntegerOverflow(int x, int rawZ, WorldFootContactRefusal refusal)
    {
        AssertRefused(Evaluate(NativeSavedWorldState.CreateNewGame(), x: x, rawZ: rawZ), refusal);
    }

    [Theory]
    [InlineData(8191)]
    [InlineData(40961)]
    [InlineData(8191.999999)]
    [InlineData(40960.000001)]
    [InlineData(90112)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void ActualCameraEyeIsComparedWithoutIntegerTruncation(double eyeX)
    {
        AssertRefused(Evaluate(NativeSavedWorldState.CreateNewGame(), eyeX: eyeX), WorldFootContactRefusal.CameraEyeOutsideRegion);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteCameraEyeCannotBeCertified(double eyeX)
    {
        AssertRefused(Evaluate(NativeSavedWorldState.CreateNewGame(), eyeX: eyeX), WorldFootContactRefusal.InvalidCameraEye);
    }

    private static WorldFootContactCertificateResult Evaluate(NativeSavedWorldState? state, byte low = 17, byte high = 0, byte flags = 0, int x = 24576, int rawZ = -29406, double eyeX = 24576)
    {
        return WorldFootContactCertificate.Evaluate(state, low, high, flags, x, rawZ, eyeX);
    }

    private static void AssertRefused(WorldFootContactCertificateResult result, WorldFootContactRefusal refusal)
    {
        Assert.False(result.Certified);
        Assert.Null(result.Certificate);
        Assert.Equal(refusal, result.Refusal);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
