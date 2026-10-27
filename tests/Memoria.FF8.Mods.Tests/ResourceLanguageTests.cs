using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class ResourceLanguageTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("jp")]
    public void AutomaticChoiceRetainsTheProviderLanguage(string language)
    {
        using Source source = new(language);
        ResourceService resources = new(source);
        Assert.Equal(language, resources.ResourceLanguage);
        Assert.Equal(language, resources.Source.Language);
    }

    [Fact]
    public void ExplicitChoicePreservesStorageProfileAndExactResourceIdentities()
    {
        using Source source = new("en");
        ResourceService resources = new(source, " FR ");
        Assert.Equal("fr", resources.ResourceLanguage);
        Assert.Equal("en", resources.Source.Language);
        Assert.Equal(new byte[] { 1 }, resources.Read(new("world/wmsetus.obj")));
        Assert.Equal(new byte[] { 2 }, resources.Read(new("world/wmsetfr.obj")));
    }

    [Fact]
    public void CandidateViewsCarryTheChoiceWithoutChangingTheirSourceDescriptor()
    {
        using Source source = new("en");
        using ResourceSourceView view = new(source, [], "fr");
        ResourceService resources = new(view);
        Assert.Equal("fr", view.ResourceLanguage);
        Assert.Equal("fr", resources.ResourceLanguage);
        Assert.Equal("en", view.Descriptor.Language);
        Assert.Equal("en", resources.Source.Language);
    }

    [Fact]
    public void UnknownExplicitLanguageFailsBeforePublication()
    {
        using Source source = new("en");
        Assert.Throws<ArgumentException>(() => new ResourceService(source, "unknown"));
        Assert.Throws<ArgumentException>(() => new ResourceSourceView(source, [], "unknown"));
    }

    private sealed class Source(string language) : IResourceSource
    {
        public ResourceSourceDescriptor Descriptor { get; } = new("fixture", GameDataEdition.Classic, [], language);

        public ResourceContent? Find(ResourceKey key)
        {
            byte[]? bytes = key.Path switch
            {
                "world/wmsetus.obj" => [1],
                "world/wmsetfr.obj" => [2],
                _ => null
            };
            return bytes is null ? null : new EncodedResource(key, "obj", 1, "fixture", bytes).Content;
        }

        public void Dispose()
        {
        }
    }
}
