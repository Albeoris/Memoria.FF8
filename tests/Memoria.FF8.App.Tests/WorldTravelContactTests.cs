using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTravelContactTests
{
    [Theory]
    [InlineData(25655, -27357, 24576)]
    [InlineData(40000, -40000, 40960)]
    [InlineData(90112, 36130, 24576)]
    [InlineData(-40960, -29406, 24576)]
    public void CameraExclusionIsIndependentOfThePlayersSignedWordAliases(int x, int z, double eyeX)
    {
        WorldFootContactCertificateResult result = Evaluate(x, z, eyeX);
        Assert.True(result.Certified);
        Assert.Equal(2, result.Certificate!.RuleVersion);
    }

    [Theory]
    [InlineData(-28130)]
    [InlineData(-17477)]
    [InlineData(-61703)]
    [InlineData(-21000)]
    [InlineData(-4119)]
    [InlineData(-65536)]
    public void AnyPossibleTrainCameraCellIsRefusedWithoutInventingPhase(double eyeX)
    {
        Assert.False(Evaluate(24576, -29406, eyeX).Certified);
    }

    [Theory]
    [InlineData(-131073, 0)]
    [InlineData(131072, 0)]
    [InlineData(0, -98305)]
    [InlineData(0, 98304)]
    public void TravelDoesNotAcceptUnwrappedNativeCoordinates(int x, int z)
    {
        Assert.False(Evaluate(x, z, 24576).Certified);
    }

    [Fact]
    public void TravelStillRequiresOriginalStoryObjectAndVehicleProvenance()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        Assert.False(WorldFootContactCertificate.Evaluate(null, 17, 0, 0, 40000, -40000, 40960, true).Certified);
        Assert.False(WorldFootContactCertificate.Evaluate(saved, 34, 0, 0, 40000, -40000, 40960, true).Certified);
        Assert.False(WorldFootContactCertificate.Evaluate(saved, 17, 0, 1, 40000, -40000, 40960, true).Certified);
        Assert.False(WorldFootContactCertificate.Evaluate(saved with { Slot4 = saved.Slot4 with { ShapeId = 73 } }, 17, 0, 0, 40000, -40000, 40960, true).Certified);
    }

    private static WorldFootContactCertificateResult Evaluate(int x, int z, double eyeX)
    {
        return WorldFootContactCertificate.Evaluate(NativeSavedWorldState.CreateNewGame(), 17, 0, 0, x, z, eyeX, allowTravel: true);
    }
}
