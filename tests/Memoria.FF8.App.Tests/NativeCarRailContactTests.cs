using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarRailContactTests
{
    [Theory]
    [InlineData(350, 0)]
    [InlineData(350, 1)]
    [InlineData(120, 0)]
    public void CarRailProcessRetainsActualAllocationAndLoaderWords(ushort story, byte flags)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(story, flags);
        NativeTrainRailBank? primary = (flags & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) : null;
        NativeTrainRailBank? alternate = (flags & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1) : null;
        var camera = new NativeWorldFootCameraState(0, 0, 0, 0, 0, -256, 0, -112, -341, -7168);
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, primary, alternate, camera, primary is null ? null : uint.MaxValue, alternate is null ? null : uint.MaxValue);
        process.ValidateCar();
        NativeNormalFootObjectProcess restored = JsonSerializer.Deserialize<NativeNormalFootObjectProcess>(JsonSerializer.Serialize(process))!;
        restored.ValidateCar();
        Assert.Equal(allocation.ClauseWords, restored.Allocation.ClauseWords);
        if (primary is not null)
        {
            Assert.Throws<InvalidDataException>(() => (process with { Primary = null, PrimaryDistanceSquared = null }).ValidateCar());
            Assert.Throws<InvalidDataException>(() => (process with { PrimaryDistanceSquared = 0 }).ValidateCar());
        }
        Assert.Throws<InvalidDataException>(() => (process with { CactuarLoadPosition = new(0, 0, 0) }).ValidateCar());
    }

    [Fact]
    public void RailBodyUsesTheSelectedCarFootprintInsteadOfTheFootRadius()
    {
        NativeWorldContactShape rail = new(400, 80, 180, 1);
        NativeWorldPathWords player = new(0, 0, 90);
        NativeWorldPathWords anchor = new(0, 0, 0);
        Assert.False(WorldStationContactCertificate.EvaluatePolygon(rail, 0, player, anchor, probeRadius: 32));
        Assert.True(WorldStationContactCertificate.EvaluatePolygon(rail, 0, player, anchor, probeRadius: 64));
    }

    [Fact]
    public void ForcedRailCircleTestsThePolygonInsteadOfCombinedObjectRadii()
    {
        NativeWorldContactShape rail = new(1000, 80, 180, 1);
        NativeWorldContactShape selected = new(96, 96, 180, 1);
        NativeWorldPathWords player = new(0, 0, 100);
        NativeWorldPathWords anchor = new(0, 0, 0);
        Assert.True(WorldStationContactCertificate.EvaluateForcedCircle(rail, selected, player, anchor));
        Assert.False(WorldStationContactCertificate.EvaluateTrainForcedCircle(rail, 0, selected, player, anchor));
        Assert.True(WorldStationContactCertificate.EvaluateTrainForcedCircle(rail, 0, selected, new(0, 0, 80), anchor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3072)]
    public void ForcedRailCircleIncludesTheRotatedPolygonInterior(short yaw)
    {
        Assert.True(WorldStationContactCertificate.EvaluateTrainForcedCircle(new(400, 100, 180, 1), yaw, new(96, 96, 180, 1), new(0, 0, 0), new(0, 0, 0)));
    }

    [Fact]
    public void TrainSpeedUsesTheSelectedShapeRadius()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainRailBank bank = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) with { Pause = 0, Speed = 40 };
        ushort foot = WorldStationContactCertificate.ContactRadius(new(96, 96, 180, 1));
        ushort car = WorldStationContactCertificate.ContactRadius(new(417, 176, 200, 1));
        Assert.Equal(48, foot);
        Assert.Equal(208, car);
        // Native integer division selects a stop for the larger car at this distance.
        Assert.Equal(0, NativeNormalFootObjectProcess.SelectSpeed(bank, 100000, car));
        Assert.True(NativeNormalFootObjectProcess.SelectSpeed(bank, 100000, foot) > 0);
    }
}
