namespace Memoria.FF7.Resources.Tests;

public sealed class FieldProjectionResourcesTests
{
    [Fact]
    public void InstalledInitializerProvidesEveryNativeFieldNearPlaneIncludingTheReactor()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for field projection checks.");
        Ff7PcExecutableData data = Ff7PcExecutableData.Load(Ff7Installation.Discover(root!));
        Assert.Equal(50, data.NativePresentation.NearPlane(116));
        Assert.Equal(512, data.NativePresentation.NearPlane(120));
        Assert.Equal(16, data.NativePresentation.NearPlane(138));
        Assert.Equal(30, data.NativePresentation.NearPlane(139));
        Assert.Equal(56, data.NativePresentation.NearPlane(253));
        Assert.Equal(1, data.NativePresentation.NearPlane(466));
        Dictionary<float, int> distribution = Enumerable.Range(0, 1200).Select(data.NativePresentation.NearPlane).GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(1141, distribution[50]);
        Assert.Equal(55, distribution[512]);
        Assert.Equal(1, distribution[1]);
        Assert.Equal(1, distribution[16]);
        Assert.Equal(1, distribution[30]);
        Assert.Equal(1, distribution[56]);
        Assert.Throws<NotSupportedException>(() => data.NativePresentation.NearPlane(-1));
        Assert.Throws<NotSupportedException>(() => data.NativePresentation.NearPlane(1200));
    }
}
