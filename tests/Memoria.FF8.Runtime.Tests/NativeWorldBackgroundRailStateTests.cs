using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldBackgroundRailStateTests
{
    [Fact]
    public void DisabledRailBanksRetainTheirActualLoaderFlagAndCallbackCameraAcrossSnapshots()
    {
        NativeWorldFootCameraState camera = new(100, -200, -300, 100, -200, -556, 512, -112, -341, -7168);
        NativeWorldBackgroundRailState rails = new(1, 1, 0, null, null, camera, null, null);
        rails.Validate();
        Assert.False(rails.HasBanks);
        NativeWorldBackgroundRailState restored = JsonSerializer.Deserialize<NativeWorldBackgroundRailState>(JsonSerializer.Serialize(rails))!;
        restored.Validate();
        Assert.Equal(rails, restored);
        Assert.Throws<InvalidDataException>(() => (rails with { LoadFlags265 = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (rails with { PrimaryDistanceSquared = uint.MaxValue }).Validate());
        Assert.Throws<InvalidDataException>(() => (rails with { DistancePosition = camera.Origin }).Validate());
    }

    [Fact]
    public void CompletedRailCallbackKeepsItsDistanceProducerAcrossASelectedBodyHandoff()
    {
        NativeWorldFootCameraState camera = new(100, -200, -300, 100, -200, -556, 512, -112, -341, -7168);
        NativeWorldPathPosition precedingFoot = new(110, -301, 205);
        NativeWorldBackgroundRailState rails = new(1, 1, 6, null, null, camera, null, null) { DistancePosition = precedingFoot, ProjectionCurveThreshold = 6144 };
        rails.Validate();
        NativeWorldBackgroundRailState restored = JsonSerializer.Deserialize<NativeWorldBackgroundRailState>(JsonSerializer.Serialize(rails))!;
        Assert.Equal(precedingFoot, restored.DistancePosition);
        Assert.Equal(6144, restored.ProjectionCurveThreshold);
        Assert.Equal(camera, restored.Camera);
    }
}
