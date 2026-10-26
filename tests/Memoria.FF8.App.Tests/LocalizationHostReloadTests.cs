using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationHostReloadTests(ITestOutputHelper output) : IDisposable
{
    private const int SourceRows = 30000;
    private const int SourceFiles = 1655;
    private const string TargetKey = "$en_operative_0000";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.LocalizationHost.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void InstalledClassicPublishesBackgroundReloadsAndRetainsLastGoodUntilStableRemoval()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(game) && Directory.Exists(game), "Configure MEMORIA_FF8_GAME_DIR with installed Classic resources.");
        using (IResourceSource source = GameDataSourceFactory.Open(game!))
        {
            Assert.SkipUnless(source.Descriptor.Edition == GameDataEdition.Classic, "Classic resources are required.");
        }

        string mods = Path.Combine(root, "Mods");
        string package = Path.Combine(mods, "Memoria", "HostAcceptance");
        List<LocalizationRecord> rows = Enumerable.Range(0, SourceRows).Select(index => new LocalizationRecord
        {
            Category = "System",
            File = "source_" + (index % SourceFiles).ToString("D4"),
            Key = "$en_source_" + (index % SourceFiles).ToString("D4") + "_" + index.ToString("D5"),
            Source = "Synthetic source " + index
        }).ToList();
        rows.Add(new() { Category = "System", File = "operative", Key = TargetKey, Source = "Original presentation", Target = "Initial translation" });
        LocalizationProject project = new()
        {
            Manifest = new() { TargetLanguage = "acceptance", MappingVersion = FieldCorrespondenceCatalog.Version },
            Entries = rows
        };
        LocalizationProjectIO.WriteNew(project, package, "json", "split", TestContext.Current.CancellationToken);
        Assert.Equal(SourceFiles + 1, Directory.GetFiles(Path.Combine(package, "Localization", "System"), "*.json").Length);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nname=Host acceptance\nenabled=1\n");
        File.WriteAllText(Path.Combine(mods, "localization-stack.json"), JsonSerializer.Serialize(new[] { "Memoria/HostAcceptance" }));

        using GameResourceHost host = new(game!, Path.Combine(root, "application"), watch: true, modsDirectory: mods);
        PumpUntil(host, () => host.CommittedLocalizations is not null, "initial activation");
        Assert.Equal("Initial translation", host.Resources.Localize(TargetKey, "Original presentation"));
        Assert.Single(host.Resources.Localization.Values);
        Assert.Equal(SourceRows + 1, Assert.Single(host.CommittedLocalizations!.Layers, layer => layer.PackageId == "Memoria/HostAcceptance").Project.Entries.Count);
        Report(host, "initial activation");

        string targetPath = Path.Combine(package, "Localization", "System", "operative.json");
        string initialPayload = File.ReadAllText(targetPath);
        File.WriteAllText(targetPath, initialPayload.Replace("Initial translation", "Reloaded translation", StringComparison.Ordinal));
        long originalGeneration = host.Resources.Generation;
        host.Mods.RequestRefresh();
        Assert.False(host.Pump(true));
        Assert.Equal(originalGeneration, host.Resources.Generation);
        Assert.Equal("Initial translation", host.Resources.Localize(TargetKey, "Original presentation"));
        PumpUntil(host, () => host.Resources.Localize(TargetKey, "Original presentation") == "Reloaded translation", "target reload");
        Assert.Equal(originalGeneration + 1, host.Resources.Generation);
        Assert.Equal(1, host.LastLocalizationChangedKeys);
        Report(host, "target reload");

        long goodGeneration = host.Resources.Generation;
        var goodCatalog = host.Resources.Localization;
        File.WriteAllText(targetPath, "{ invalid JSON");
        host.Mods.RequestRefresh();
        PumpUntil(host, () => host.Mods.Diagnostics.Any(diagnostic => diagnostic.Code == "localization-invalid"), "malformed update rejection");
        Assert.Equal(goodGeneration, host.Resources.Generation);
        Assert.Same(goodCatalog, host.Resources.Localization);
        Assert.Equal("Reloaded translation", host.Resources.Localize(TargetKey, "Original presentation"));

        File.WriteAllText(targetPath, initialPayload.Replace("Initial translation", "Reloaded translation", StringComparison.Ordinal));
        host.Mods.RequestRefresh();
        PumpUntil(host, () => !host.Mods.IsReloadPending && !host.Mods.Diagnostics.Any(diagnostic => diagnostic.Code == "localization-invalid"), "valid recovery");
        Assert.Equal("Reloaded translation", host.Resources.Localize(TargetKey, "Original presentation"));
        goodGeneration = host.Resources.Generation;
        goodCatalog = host.Resources.Localization;

        string sourcePath = Path.Combine(package, "Localization", "System", "source_0000.json");
        File.WriteAllText(sourcePath, File.ReadAllText(sourcePath).Replace("Synthetic source 0", "Edited source only 0", StringComparison.Ordinal));
        long catalogGeneration = host.Mods.Catalog.Generation;
        PumpUntil(host, () => host.Mods.Catalog.Generation > catalogGeneration, "source-only reload");
        Assert.Equal(goodGeneration, host.Resources.Generation);
        Assert.Same(goodCatalog, host.Resources.Localization);
        Assert.Equal(0, host.LastLocalizationChangedKeys);
        Assert.Contains(Assert.Single(host.CommittedLocalizations!.Layers, layer => layer.PackageId == "Memoria/HostAcceptance").Project.Entries, row => row.Key == "$en_source_0000_00000" && row.Source == "Edited source only 0");
        Report(host, "source-only reload");

        File.Delete(targetPath);
        host.Mods.RequestRefresh();
        Assert.False(host.Pump(true));
        Assert.Equal(goodGeneration, host.Resources.Generation);
        Assert.Equal("Reloaded translation", host.Resources.Localize(TargetKey, "Original presentation"));
        PumpUntil(host, () => host.Resources.Localize(TargetKey, "Original presentation") == "Original presentation", "stable payload removal");
        Assert.Equal(goodGeneration + 1, host.Resources.Generation);
        Assert.Empty(host.Resources.Localization.Values);
        Assert.Equal(1, host.LastLocalizationChangedKeys);
        Assert.Equal(SourceRows, Assert.Single(host.CommittedLocalizations!.Layers, layer => layer.PackageId == "Memoria/HostAcceptance").Project.Entries.Count);
        Report(host, "stable payload removal");
    }

    private void PumpUntil(GameResourceHost host, Func<bool> complete, string operation)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        double totalPumpMilliseconds = 0;
        double maximumPumpMilliseconds = 0;
        int pumpCount = 0;
        while (!complete() && elapsed.Elapsed < TimeSpan.FromSeconds(90))
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            Stopwatch pump = Stopwatch.StartNew();
            host.Pump();
            double milliseconds = pump.Elapsed.TotalMilliseconds;
            totalPumpMilliseconds += milliseconds;
            maximumPumpMilliseconds = Math.Max(maximumPumpMilliseconds, milliseconds);
            pumpCount++;
            if (!complete())
            {
                Thread.Sleep(25);
            }
        }
        Assert.True(complete(), operation + " did not complete: " + string.Join("; ", host.Mods.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
        output.WriteLine("{0}: observed after {1:F3} ms, pumps={2}, total pump={3:F3} ms, maximum pump={4:F3} ms", operation, elapsed.Elapsed.TotalMilliseconds, pumpCount, totalPumpMilliseconds, maximumPumpMilliseconds);
    }

    private void Report(GameResourceHost host, string operation)
    {
        output.WriteLine("{0}: preparation={1:F3} ms, owner publication={2:F3} ms, changed keys={3}", operation, host.CommittedLocalizations!.PreparationMilliseconds, host.LastLocalizationPublicationMilliseconds, host.LastLocalizationChangedKeys);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
