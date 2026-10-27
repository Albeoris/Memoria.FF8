using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarForcedContactTests
{
    private static readonly NativeWorldContactShape Car = new(417, 176, 200, 1);
    private static readonly NativeWorldContactShape Foot = new(96, 96, 180, 1);

    [Theory]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void TransferUsesStrictCombinedShapeRadiusAndIgnoresHeight(int distance, bool expected)
    {
        Assert.Equal(expected, WorldStationContactCertificate.EvaluateForcedCircle(Car, Foot, new(checked((short)distance), -10000, 0), new(0, 10000, 0)));
    }

    [Fact]
    public void TransferCircleIncludesRegionsBeyondTheOrdinaryCarPolygon()
    {
        NativeWorldPathWords player = new(0, 0, 200);
        NativeWorldPathWords anchor = new(0, 0, 0);
        Assert.True(WorldStationContactCertificate.EvaluateForcedCircle(Car, Foot, player, anchor));
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(Car, 0, player, anchor));
    }

    [Fact]
    public void ForcedCircleUsesTheFullLengthForNativeKindTwo()
    {
        NativeWorldContactShape target = new(100, 40, 80, 2);
        Assert.True(WorldStationContactCertificate.EvaluateForcedCircle(target, Foot, new(147, 0, 0), new(0, 0, 0)));
        Assert.False(WorldStationContactCertificate.EvaluateForcedCircle(target, Foot, new(148, 0, 0), new(0, 0, 0)));
    }
}
