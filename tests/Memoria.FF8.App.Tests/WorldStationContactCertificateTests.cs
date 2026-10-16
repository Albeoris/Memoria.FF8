using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldStationContactCertificateTests
{
    [Fact]
    public void OriginalContactRecordsRetainTheirCompleteNativeDimensionsAndKinds()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        Assert.Equal(new NativeWorldContactShape(811, 206, 233, 2), assets.ReadNativeContactShape(70));
        Assert.Equal(new NativeWorldContactShape(811, 206, 197, 2), assets.ReadNativeContactShape(71));
        Assert.Equal(new NativeWorldContactShape(323, 170, 209, 1), assets.ReadNativeContactShape(75));
        Assert.Equal(new NativeWorldContactShape(216, 248, 121, 1), assets.ReadNativeContactShape(95));
    }

    [Fact]
    public void NativeFivePointsAndReverseContainmentUseTheActualRotatedRectangle()
    {
        NativeWorldContactShape train = new(811, 206, 233, 2);
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(train, 0, new(-400, 0, 0), default));
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(train, 0, new(-400, 0, 140), default));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(train, 0, new(-400, 0, 135), default));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(train, 0, new(-400, 0, 300), default, 200));
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(train, 0, new(-400, 0, 340), default, 200));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(train, 1024, new(0, 0, 400), default));
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(train, 1024, new(140, 0, 400), default));
        // A small allocated shape wholly inside the foot fan is found by the reverse pass.
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(new(8, 8, 10, 1), 0, default, new(10, 0, 10)));
    }

    [Fact]
    public void BlockingAndInteractionRemainDifferentAtTheReachedStationTrainSeparation()
    {
        NativeWorldContactShape carriage = new(811, 206, 197, 2);
        NativeWorldPathWords player = new(334, -794, -829);
        NativeWorldPathWords carriageAnchor = new(569, -687, -37);
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(carriage, 3050, player, carriageAnchor));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(carriage, 3050, player, carriageAnchor, 200));
    }

    [Fact]
    public void UnrepresentedShapesAndWordPackingRefuseAtTheReachedConsumer()
    {
        Assert.Throws<NotSupportedException>(() => WorldStationContactCertificate.EvaluatePolygon(new(100, 100, 100, 4), 0, default, default));
        Assert.Throws<NotSupportedException>(() => WorldStationContactCertificate.EvaluatePolygon(new(100, 100, 100, 1), 0, new(short.MaxValue, 0, 0), new(short.MaxValue, 0, 0)));
        Assert.Throws<NotSupportedException>(() => WorldStationContactCertificate.EvaluatePolygon(new(100, 100, 100, 1), 0, default, default, 1));
    }
}
