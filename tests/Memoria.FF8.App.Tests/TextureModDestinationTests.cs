namespace Memoria.FF8.App.Tests;

public sealed class TextureModDestinationTests
{
    [Fact]
    public void Existing_secondary_configured_root_is_editable_and_longest_root_wins()
    {
        string primary = Path.Combine(Path.GetTempPath(), "texture-primary");
        string secondary = Path.Combine(Path.GetTempPath(), "texture-secondary");
        string nested = Path.Combine(secondary, "Memoria", "nested");
        Assert.Equal(Path.Combine(secondary, "Memoria"), TextureModDestination.Resolve([primary, secondary], Path.Combine(secondary, "Memoria", "package")));
        Assert.Equal(Path.Combine(nested, "Memoria"), TextureModDestination.Resolve([secondary, nested], Path.Combine(nested, "Memoria", "package")));
    }

    [Fact]
    public void Unconfigured_and_prefix_sibling_destinations_are_rejected()
    {
        string root = Path.Combine(Path.GetTempPath(), "texture-root");
        Assert.Throws<InvalidDataException>(() => TextureModDestination.Resolve([root], Path.Combine(root, "Memoria-other", "package")));
        Assert.Throws<InvalidDataException>(() => TextureModDestination.Resolve([root], Path.Combine(root, "Memoria")));
    }
}
