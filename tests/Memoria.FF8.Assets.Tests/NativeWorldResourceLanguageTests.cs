using System.Buffers.Binary;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldResourceLanguageTests
{
    [Fact]
    public void ExternalFrenchWorldResourcesReachConsumersWithCanonicalTextureOwnership()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native world resources are required.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_LANGUAGE_FIXTURES");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Configure optional native world language fixtures.");
        string[] fixtures = Directory.GetDirectories(directory!).Where(path => File.Exists(Path.Combine(path, "world", "wmsetfr.obj"))).ToArray();
        Assert.NotEmpty(fixtures);
        ResourceKey key = NativeResourceNames.WorldSettings("fr");
        foreach (string fixture in fixtures)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(fixture, "world", "wmsetfr.obj"));
            using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
            CaptureResolver resolver = new();
            ResourceService resources = new(source, "fr");
            resources.Prepare(resolver, encodedResources: [new(key, "obj", 1, "fixture", bytes)]).Commit();
            using WorldAssets world = new(TestResources.GameDirectory, resources: resources);
            Assert.Equal(bytes, resources.Read(key));
            Assert.Equal("en", resources.Source.Language);
            Assert.Equal(new WorldMapLocations(bytes).GetPlaceId(0, 0), WorldMapLocations.Read(resources).GetPlaceId(0, 0));
            Assert.Equal(Message(bytes, 0), world.ReadWorldMessageBytes(0));
            _ = world.ReadNativeSectionTexture(38, 0);
            _ = world.ReadScriptedStaticModel(64);
            Assert.NotEmpty(resolver.Contexts);
            Assert.All(resolver.Contexts, context => Assert.Equal(key, context.Key.Owner));
            Assert.All(resolver.Contexts, context => Assert.Equal(key, context.EncodedSource!.Key));
            using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
            ModelCatalogEntry[] models = assets.EnumerateModelCatalog().Where(entry => entry.Category == "World static").ToArray();
            Assert.NotEmpty(models);
            Assert.All(models, model => Assert.Equal(key.Path, model.ResourcePath));
            Assert.All(models, model => Assert.Null(model.LoadError));
        }
    }

    private static byte[] Message(byte[] bytes, int index)
    {
        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(13 * 4));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14 * 4));
        int start = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + index * 4));
        int length = Array.IndexOf(bytes, (byte)0, start, end - start) - start + 1;
        Assert.True(length > 0);
        return bytes.AsSpan(start, length).ToArray();
    }

    private sealed class CaptureResolver : ITextureReplacementResolver
    {
        public List<TextureBuildContext> Contexts { get; } = [];

        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            Contexts.Add(context);
            return null;
        }
    }
}
