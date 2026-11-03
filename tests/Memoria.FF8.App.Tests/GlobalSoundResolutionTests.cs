using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class GlobalSoundResolutionTests
{
    [Fact]
    public void InstalledInfirmaryEffectsResolveThroughTheWorldProvider()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        FieldSoundEffectTable table = assets.LoadFieldSoundEffects(180);
        Assert.NotEmpty(table.EffectIds);
        foreach (uint effect in table.EffectIds.Distinct())
        {
            int sample = EventEngine.MapGlobalSoundSample(effect);
            GlobalSoundResource resource = GameWorld.ResolveGlobalSound(assets.Resources, sample);
            Assert.Equal(sample, resource.SampleIndex);
            Assert.Equal(GlobalSoundBackendStatus.Muted, resource.BackendStatus);
        }
    }

    [Fact]
    public void MissingResourceIdentifiesSampleAndLogicalResource()
    {
        using InMemoryResourceSource source = new(new("audio-test", GameDataEdition.Classic, []));
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => GameWorld.ResolveGlobalSound(new ResourceService(source), 69));
        Assert.Contains("sample 69", error.Message);
        Assert.Contains("files/sound/audio.dat", error.Message);
        Assert.IsType<FileNotFoundException>(error.InnerException);
    }

    [Fact]
    public void CorruptDescriptorRetainsItsReadFailure()
    {
        using InMemoryResourceSource source = new(new("audio-test", GameDataEdition.Classic, []));
        source.Set(new("files/sound/audio.dat"), [], "bin");
        source.Set(new("files/sound/audio.fmt"), [0], "bin");
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => GameWorld.ResolveGlobalSound(new ResourceService(source), 0));
        Assert.Contains("sample 0", error.Message);
        Assert.Contains("Truncated audio.fmt", error.Message);
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void DescriptorUsesSelectedProviderAndExplainsUnsupportedWaveFormat(ushort tag)
    {
        using MemoryStream format = new();
        using (BinaryWriter writer = new(format, System.Text.Encoding.UTF8, true))
        {
            writer.Write((ushort)0);
            writer.Write(4u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(tag);
            writer.Write((ushort)1);
            writer.Write(22050u);
            writer.Write(44100u);
            writer.Write((ushort)2);
            writer.Write((ushort)16);
            writer.Write((ushort)0);
        }
        using InMemoryResourceSource source = new(new("audio-test", GameDataEdition.Classic, []));
        source.Set(new("files/sound/audio.dat"), new byte[4], "bin");
        source.Set(new("files/sound/audio.fmt"), format.ToArray(), "bin");
        ResourceService resources = new(source);
        if (tag == 1)
        {
            GlobalSoundResource resource = GameWorld.ResolveGlobalSound(resources, 0);
            Assert.Equal(0, resource.SampleIndex);
            Assert.Equal(GlobalSoundBackendStatus.Muted, resource.BackendStatus);
            Assert.Equal(new GlobalSoundLoopPolicy(0, 0, 0, 22050, 16, 1, null), resource.LoopPolicy);
        }
        else
        {
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => GameWorld.ResolveGlobalSound(resources, 0));
            Assert.Contains("sample 0", error.Message);
            Assert.Contains("tag=99", error.Message);
        }
    }
}
