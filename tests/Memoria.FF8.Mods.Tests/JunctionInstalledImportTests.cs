using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionInstalledImportTests
{
    [Theory]
    [InlineData("junction-video-base")]
    [InlineData("junction-video-layer")]
    [InlineData("junction-video-alternate")]
    [InlineData("junction-card-textures")]
    [InlineData("junction-world-textures")]
    [InlineData("junction-external-music")]
    public void OptionalExternalArchiveImportsItsManifestAndSelectedResourcePaths(string resource)
    {
        string? catalogPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURE_CATALOG");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(catalogPath), "Configure an optional external fixture catalog.");
        Dictionary<string, string> catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalogPath)) ?? throw new InvalidDataException("External fixture catalog must contain an object.");
        Assert.SkipUnless(catalog.ContainsKey(resource), $"External fixture catalog has no '{resource}' archive.");
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_FIXTURES") ?? Path.GetDirectoryName(Path.GetFullPath(catalogPath))!;
        string archive = Path.GetFullPath(catalog[resource], root);
        Assert.SkipUnless(File.Exists(archive), "Optional archive is unavailable.");
        JunctionModPackage package = JunctionModImporter.Open(archive);
        Assert.False(string.IsNullOrWhiteSpace(package.Name));
        Assert.NotEmpty(package.Files);
        Assert.All(package.Files, file =>
        {
            Assert.Equal(file.RelativePath, ModCatalogSnapshot.NormalizePath(file.RelativePath));
            Assert.DoesNotContain("..", file.SourcePath.Split('/'));
        });
        Console.WriteLine($"junction-import=Validated resource={resource} selected-files={package.Files.Count}");
    }
}
