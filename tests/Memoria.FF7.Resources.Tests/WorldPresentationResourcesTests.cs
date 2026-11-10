namespace Memoria.FF7.Resources.Tests;

public sealed class WorldPresentationResourcesTests
{
    [Theory]
    [InlineData(0, "bbe.hrc", 10)]
    [InlineData(1, "dlb.hrc", 11)]
    [InlineData(2, "ata.hrc", 10)]
    public void InstalledOriginalWorldLeaderTablesLoadCompleteGeometryAndClips(int leader, string hrc, int clips)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the world presentation check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldCharacterDescriptor descriptor = Ff7PcExecutableData.LoadWorldCharacterDescriptor(installation, leader);
        Assert.Equal(hrc, descriptor.HrcName, ignoreCase: true);
        Assert.Equal(clips, descriptor.Animations.Count);
        Assert.Equal(15.2f, descriptor.Scale);
        Ff7FieldModelResources model = descriptor.Load(resources);
        Assert.NotEmpty(model.Geometry);
        Assert.Equal(clips, model.Animations.Count);
        Assert.All(model.Animations.Values, animation => Assert.NotEmpty(animation.Frames));
        Ff7WorldPresentationData data = Ff7PcExecutableData.LoadWorldPresentation(installation, resources);
        Assert.Equal(new Ff7WorldCameraProfile(1500, 10000), data.CameraProfiles[0]);
        Assert.Equal(new Ff7WorldCameraProfile(1800, 7000), data.CameraProfiles[1]);
        Assert.Equal(512, data.ArcAngles.Count);
        Assert.Equal((short)0, data.ArcAngles[0]);
        Assert.Equal(360f / 4096, data.DegreesPerAngleUnit);
    }
}
