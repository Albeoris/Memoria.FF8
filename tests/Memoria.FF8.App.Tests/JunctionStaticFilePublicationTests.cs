using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionStaticFilePublicationTests
{
    [Theory]
    [InlineData("en", false, true)]
    [InlineData("fr", false, false)]
    [InlineData("en", true, false)]
    [InlineData("fr", true, false)]
    public void LanguageVariantsKeepTheirIdentitiesAndWarnOnlyWhenThePackageLacksTheCurrentVariant(string language, bool includeEnglish, bool warning)
    {
        using IsolatedFieldGame game = new();
        string directory = Path.Combine(Path.GetTempPath(), "memoria-language-variants-" + Guid.NewGuid().ToString("N"));
        try
        {
            using IResourceSource source = GameDataSourceFactory.Open(game.GameDirectory);
            byte[] original = new ResourceService(source).Read(new("world/wmsetus.obj"));
            string package = Path.Combine(directory, "Junction", "World");
            Directory.CreateDirectory(Path.Combine(package, "assets", "world"));
            File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><Name>World</Name><ModFolder Folder=\"assets\" /></ModInfo>");
            File.WriteAllBytes(Path.Combine(package, "assets", "world", "wmsetfr.obj"), original);
            if (includeEnglish)
            {
                File.WriteAllBytes(Path.Combine(package, "assets", "world", "wmsetus.obj"), original);
            }

            using GameResourceHost host = new(game.GameDirectory, Path.Combine(directory, "application"), watch: false, modsDirectory: directory, resourceLanguage: language);
            Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(item => item.Id == "Junction/World").Status);
            Assert.Equal(warning, host.Mods.Diagnostics.Any(item => item.Code == "resource-language-variant-missing" && item.PackageId == "Junction/World"));
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "resource-unused");
            Assert.Equal(original, host.Resources.Read(new("world/wmsetfr.obj")));
            Assert.StartsWith("Junction/World:", host.Resources.Get(new("world/wmsetfr.obj")).Provenance);
            ResourceContent english = host.Resources.Get(new("world/wmsetus.obj"));
            Assert.Equal(includeEnglish, english.Provenance.StartsWith("Junction/World:", StringComparison.Ordinal));
            Assert.Equal(original, host.Resources.Read(new("world/wmsetus.obj")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void RegisteredFormatWithoutInstalledSourceIsRejectedWithOwningDiagnostic()
    {
        using IsolatedFieldGame game = new();
        string directory = Path.Combine(Path.GetTempPath(), "memoria-static-file-missing-" + Guid.NewGuid().ToString("N"));
        try
        {
            string package = Path.Combine(directory, "Junction", "Tables");
            Directory.CreateDirectory(Path.Combine(package, "assets"));
            File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><Name>Tables</Name><ModFolder Folder=\"assets\" /></ModInfo>");
            File.WriteAllBytes(Path.Combine(package, "assets", "absent_table.tdw"), [1, 2, 3]);

            using GameResourceHost host = new(game.GameDirectory, Path.Combine(directory, "application"), watch: false, modsDirectory: directory);

            Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Junction.InvalidEncodedResource" && item.PackageId == "Junction/Tables" && item.RelativePath == "absent_table.tdw");
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "resource-unused");
            Assert.Throws<FileNotFoundException>(() => host.Resources.Get(new("main/absent_table.tdw")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void RootStaticFilePublishesToMainArchiveWithUnusedConsumerDiagnosticAndReloadGuard()
    {
        using IsolatedFieldGame game = new();
        string directory = Path.Combine(Path.GetTempPath(), "memoria-static-file-" + Guid.NewGuid().ToString("N"));
        try
        {
            using IResourceSource source = GameDataSourceFactory.Open(game.GameDirectory);
            ResourceService original = new(source);
            ResourceKey main = new("main/sysfnt.tdw");
            ResourceKey menu = new("menu/sysfnt.tdw");
            byte[] sourceBytes = original.Read(main);
            byte[] menuBytes = original.Read(menu);
            byte[] replacement = sourceBytes.ToArray();
            replacement[^1] ^= 1;
            string package = Path.Combine(directory, "Junction", "Tables");
            Directory.CreateDirectory(Path.Combine(package, "assets"));
            File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><Name>Tables</Name><ModFolder Folder=\"assets\" /></ModInfo>");
            string file = Path.Combine(package, "assets", "sysfnt.tdw");
            File.WriteAllBytes(file, replacement);

            using GameResourceHost host = new(game.GameDirectory, Path.Combine(directory, "application"), watch: false, modsDirectory: directory);
            Assert.Equal(ModEffectiveStatus.Active, host.Mods.Catalog.Packages.Single(item => item.Id == "Junction/Tables").Status);
            Assert.Contains(host.Mods.Diagnostics, item => item.Code == "resource-unused" && item.PackageId == "Junction/Tables" && item.RelativePath == "sysfnt.tdw");
            Assert.Equal(replacement, host.Resources.Read(main));
            Assert.Equal(menuBytes, host.Resources.Read(menu));
            Assert.StartsWith("Junction/Tables:", host.Resources.Get(main).Provenance);
            long generation = host.Resources.Generation;
            File.WriteAllBytes(file, sourceBytes);
            host.Mods.Refresh();

            Assert.False(host.Pump(true));
            Assert.Equal(generation, host.Resources.Generation);
            Assert.Equal(replacement, host.Resources.Read(main));
            Assert.Contains(host.Mods.Diagnostics, item => item.Code == "resource-reload-required");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
