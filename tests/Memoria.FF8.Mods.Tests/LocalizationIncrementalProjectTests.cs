using System.Text.Json.Nodes;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationIncrementalProjectTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.LocalizationIncremental", Guid.NewGuid().ToString("N"));

    [Fact]
    public void UnchangedPayloadsAreReusedWhileSameMetadataEditsStillInvalidateTheirContent()
    {
        string package = Write("source");
        LocalizationProjectReadCache cache = new();
        LocalizationProject original = LocalizationProjectIO.Read(package, cache: cache);
        Assert.Equal(2, cache.ParsedPayloads);
        LocalizationProjectIO.Read(package, cache: cache);
        Assert.Equal(2, cache.ParsedPayloads);
        Assert.Equal(2, cache.CacheHits);
        string path = Path.Combine(package, "Localization", "System", "first.json");
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        string text = File.ReadAllText(path);
        File.WriteAllText(path, text.Replace("First", "Later", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, stamp);
        LocalizationProject edited = LocalizationProjectIO.Read(package, cache: cache);
        Assert.Equal(3, cache.ParsedPayloads);
        Assert.Equal("Later", edited.Entries.Single(row => row.File == "first").Target);
        Assert.Equal("First", original.Entries.Single(row => row.File == "first").Target);
    }

    [Fact]
    public void HeaderContractsInvalidInputsAndRemovedPayloadsCannotReuseStaleRows()
    {
        string package = Write("contracts");
        LocalizationProjectReadCache cache = new();
        LocalizationProjectIO.Read(package, cache: cache);
        string path = Path.Combine(package, "Localization", "System", "first.json");
        string original = File.ReadAllText(path);
        File.WriteAllText(path, "{invalid");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => LocalizationProjectIO.Read(package, cache: cache));
        File.WriteAllText(path, original);
        Assert.Equal("First", LocalizationProjectIO.Read(package, cache: cache).Entries.Single(row => row.File == "first").Target);
        string manifestPath = Path.Combine(package, "Localization", "localization.manifest.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["sourceColumn"] = "Source [another dataset]";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        LocalizationProject differentContract = LocalizationProjectIO.Read(package, cache: cache);
        Assert.All(differentContract.Entries, row => Assert.False(row.SourcePresent));
        Assert.Equal(4, cache.ParsedPayloads);
        File.Delete(path);
        Assert.Single(LocalizationProjectIO.Read(package, allowMissingPayloads: true, cache: cache).Entries);
    }

    [Fact]
    public void NewPatchInheritsTheBaseContractButStartsWithNoTranslatedContribution()
    {
        LocalizationProject source = LocalizationProjectIO.Read(Write("base"));
        LocalizationProject patch = LocalizationProjectExport.CreatePatch(source, "Correction patch");
        Assert.NotEqual(source.Manifest.ProjectId, patch.Manifest.ProjectId);
        Assert.Equal(source.Manifest.ProjectId, patch.Manifest.BaseProjectId);
        Assert.Equal(source.Manifest.TargetLanguage, patch.Manifest.TargetLanguage);
        Assert.Equal(source.Entries.Select(row => row.Key), patch.Entries.Select(row => row.Key));
        Assert.Equal(source.Entries.Select(row => row.Source), patch.Entries.Select(row => row.Source));
        Assert.All(patch.Entries, row => Assert.Equal("", row.Target));
        string output = Path.Combine(root, "patch");
        LocalizationProjectIO.WriteNew(patch, output, "xlsx", "single", TestContext.Current.CancellationToken);
        Assert.Equal(source.Manifest.ProjectId, LocalizationProjectIO.Read(output).Manifest.BaseProjectId);
        Assert.Contains("enabled=0", File.ReadAllText(Path.Combine(output, "mod.ini")));
        Assert.Equal("First", source.Entries.Single(row => row.File == "first").Target);
    }

    private string Write(string name)
    {
        string path = Path.Combine(root, name);
        LocalizationProject project = new()
        {
            Manifest = new() { TargetLanguage = "fixture translation" },
            Entries = [new() { Category = "System", File = "first", Key = "$en_first_0000", Source = "Source one", Target = "First" }, new() { Category = "System", File = "second", Key = "$en_second_0000", Source = "Source two", Target = "Second" }]
        };
        LocalizationProjectIO.WriteNew(project, path, "json", "split", TestContext.Current.CancellationToken);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
