namespace Memoria.FF7.Resources.Tests;

public sealed class WorldTextureResourcesTests
{
    [Theory]
    [InlineData(0, 282)]
    [InlineData(2, 8)]
    [InlineData(3, 4)]
    public void InstalledWorldTablesResolveOriginalTextureNamesAndUvOrigins(int world, int count)
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the native world texture table check.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation, world);
        Ff7WorldTextureResources textures = new(installation, resources);
        Assert.Equal(count, textures.Descriptors.Count);
        if (world == 0)
        {
            Assert.Equal(new Ff7WorldTextureDescriptor("pond.tex", 0, 352), textures.Descriptors[0]);
            Assert.Equal(new Ff7WorldTextureDescriptor("riv_m2.tex", 128, 64), textures.Descriptors[1]);
        }
        for (int index = 0; index < count; index++)
        {
            var texture = textures.Read(index);
            Assert.True(texture.Width > 0 && texture.Height > 0);
            Assert.Same(texture, textures.Read(index));
            Assert.EndsWith(".tex", textures.Descriptors[index].ResourceName);
            if (textures.Descriptors[index].AnimationFrames is { } frames)
            {
                Assert.True(frames.Count is 4 or 8);
                for (int frame = 0; frame < frames.Count; frame++)
                {
                    Assert.True(textures.Read(index, frame).Width > 0);
                }
                Assert.Same(texture, textures.Read(index, frames.Count));
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => textures.Read(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => textures.Read(count));
    }
}
