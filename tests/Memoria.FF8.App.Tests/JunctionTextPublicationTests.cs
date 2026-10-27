using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionTextPublicationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.TextPublication.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ToggleReloadAndMalformedUpdateRetainOneCoherentPublication()
    {
        string packageRoot = Path.Combine(root, "Mods", "Junction", "Text");
        Directory.CreateDirectory(packageRoot);
        System.IO.File.WriteAllText(Path.Combine(packageRoot, "mod.xml"), "<ModInfo><Name>Text</Name><ModFolder Folder=\"assets\" /></ModInfo>");
        string tablePath = Path.Combine(packageRoot, "assets", "direct", "exe", "card_names.msd");
        Directory.CreateDirectory(Path.GetDirectoryName(tablePath)!);
        WriteNames(tablePath, "First");
        using GameResourceHost host = new(root, Path.Combine(root, "application"), watch: false);
        ResourceKey key = StaticTextResource.CardNames;
        Assert.Equal("First 0", host.Resources.ReadText(key, 0));
        long generation = host.Resources.Generation;
        Assert.True(host.Mods.SetEnabled("Junction/Text", false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal("Original", host.Resources.ReadText(key, 0, "Original"));
        Assert.True(host.Mods.SetEnabled("Junction/Text", true).Success);
        Assert.True(host.Pump(true));
        Assert.Equal("First 0", host.Resources.ReadText(key, 0));
        WriteNames(tablePath, "Changed");
        host.Mods.RequestRefresh();
        Assert.True(host.Pump(true));
        Assert.Equal("Changed 109", host.Resources.ReadText(key, 109));
        long validGeneration = host.Resources.Generation;
        System.IO.File.WriteAllBytes(tablePath, [1, 2, 3]);
        host.Mods.RequestRefresh();
        Assert.False(host.Pump(true));
        Assert.Equal(validGeneration, host.Resources.Generation);
        Assert.Equal("Changed 0", host.Resources.ReadText(key, 0));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidTextResource" && diagnostic.PackageId == "Junction/Text");
        Assert.True(validGeneration > generation);
    }

    private static void WriteNames(string path, string prefix)
    {
        MsdFile table = new(FF8Encoding.CreateEuropean(FF8TextLanguage.EN));
        table.Texts.AddRange(Enumerable.Range(0, 110).Select(id => $"{prefix} {id}{{End}}"));
        System.IO.File.WriteAllBytes(path, table.Write());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
