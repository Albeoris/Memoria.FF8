using System.Buffers.Binary;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class LocalizationSourceExporterTests
{
    [Fact]
    public void ApprovedMappingRetainsAllSourceIdentitiesAndAmbiguousAliases()
    {
        Assert.Equal(134519, FieldCorrespondenceCatalog.SourceCount);
        Assert.Equal(["$it_bcport1a_0049", "$it_bcport1a_0051"], FieldCorrespondenceCatalog.GetEquivalents("$en_bcport1a_0050", "it"));
        Assert.False(FieldCorrespondenceCatalog.TryGetEquivalentKey("$en_bcport1a_0050", "it", out _));
        Assert.Equal(["$en_bcport1a_0050"], FieldCorrespondenceCatalog.GetEquivalents("$it_bcport1a_0051", "en"));
        Assert.True(FieldCorrespondenceCatalog.Contains("$it_bcport1a_0051"));
        Assert.Equal("$fr_bgroad_1_0012", FieldCorrespondenceCatalog.GetCanonicalKey("fr", "bgroad_1", 12));
        Assert.Empty(FieldCorrespondenceCatalog.GetEquivalents("$en_unknown_0000", "jp"));
        Assert.Equal("$jp_test_10000", FieldCorrespondenceCatalog.GetCanonicalKey("ja", "test", 10000));
    }

    [Fact]
    public void IndexedNamesKeepFamiliesAndRolesDistinct()
    {
        Assert.Equal("$fr_kernel_battleitems_name_0000", LocalizationSourceExporter.IndexedKey("fr", LocalizationSourceExporter.KernelTable("BattleItems", "Name"), 0));
        Assert.NotEqual(LocalizationSourceExporter.KernelTable("BattleItems", "Name"), LocalizationSourceExporter.KernelTable("FieldItems", "Name"));
    }

    [Fact]
    public void ReadsActualNamedicAndLocationsPreservingEmptySlotsAndTokens()
    {
        using InMemoryResourceSource source = Source();
        var encoding = FF8Encoding.CreateEuropean();
        string[] values = ["Named", "", "A{Line}B"];
        source.Set(new("menu/namedic.bin"), NamedicBin.WriteStrings(values, encoding), "bin");
        source.Set(new("menu/areames.dc1"), NamedicBin.WriteStrings(["Place"], encoding), "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "fr", TestContext.Current.CancellationToken);

        Assert.Equal(values, result.Entries.Where(row => row.File == "namedic_menu").Select(row => row.Text));
        Assert.Equal("$fr_namedic_menu_0001", result.Entries.Single(row => row.File == "namedic_menu" && row.Text.Length == 0).Key);
        Assert.Equal("Place", Assert.Single(result.Entries, row => row.Category == "Locations").Text);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(NamedicBin.WriteStrings(values, encoding)));
        Assert.All(result.Entries.Where(row => row.File == "namedic_menu"), row => Assert.Equal(expectedHash, row.Metadata["Source SHA256"]));
        Assert.Contains(result.Gaps, gap => gap.Resource == "main/kernel.bin");
    }

    [Fact]
    public void ScanDirectoryPreservesSelectorsAndSharedOffsets()
    {
        using InMemoryResourceSource source = Source();
        byte[] text = FF8Encoding.CreateEuropean().GetBytes("Description");
        byte[] bytes = new byte[320 + text.Length + 1];
        text.CopyTo(bytes, 320);
        source.Set(new("text/enemy-scan"), bytes, "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "en", TestContext.Current.CancellationToken);
        LocalizationSourceEntry[] scans = result.Entries.Where(row => row.File == "scan_monsters").ToArray();

        Assert.Equal(160, scans.Length);
        Assert.Equal("$en_scan_monsters_0159", scans[^1].Key);
        Assert.All(scans, row => Assert.Equal("Description", row.Text));
    }

    [Fact]
    public void JapaneseNonfieldResourcesAreReportedRatherThanDecodedWithEuropeanTables()
    {
        using InMemoryResourceSource source = Source();
        source.Set(new("menu/namedic.bin"), [1, 0, 4, 0, 0x1c, 0x20, 0], "bin");

        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(source, new MissingArchives(), "jp", TestContext.Current.CancellationToken);

        LocalizationSourceEntry missing = Assert.Single(result.Entries);
        Assert.Equal("$jp_namedic_menu_0000", missing.Key);
        Assert.Empty(missing.Text);
        Assert.Equal("false", missing.Metadata["Source Present"]);
        Assert.Contains(result.Gaps, gap => gap.Category == "Menus" && gap.Reason.Contains("Japanese"));
    }

    [Fact]
    public void CancellationPropagatesWithoutInventingAvailability()
    {
        using InMemoryResourceSource source = Source();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => LocalizationSourceExporter.Read(source, new MissingArchives(), "en", cancellation.Token));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("it")]
    [InlineData("jp")]
    public void OptionalInstalledResourcesExportEveryFieldSlotWithSourceIdentity(string language)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_LOCALIZATION_GAME_DIR for optional installed source export validation.");
        LocalizationSourceCatalog result = LocalizationSourceExporter.Read(directory, language, TestContext.Current.CancellationToken);
        LocalizationSourceEntry[] fields = result.Entries.Where(row => row.Category == "Fields" && !row.Metadata.ContainsKey("Context Variant")).ToArray();
        string? reportDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, language + "-source-summary.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Language = language,
                Entries = result.Entries.Count,
                Fields = fields.Length,
                Categories = result.Entries.GroupBy(row => row.Category).ToDictionary(group => group.Key, group => group.Count()),
                Missing = FieldCorrespondenceCatalog.GetKeys(language).Except(fields.Select(row => row.Key)).ToArray(),
                Gaps = result.Gaps
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.Equal(FieldCorrespondenceCatalog.SourceCountForLanguage(language), fields.Length);
        Assert.Equal(fields.Length, fields.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(fields, row =>
        {
            Assert.True(FieldCorrespondenceCatalog.Contains(row.Key), row.Key);
            Assert.StartsWith("$" + language + "_" + row.File + "_", row.Key);
            Assert.NotEmpty(row.Metadata["Source MSD SHA256"]!);
            Assert.True(row.Metadata.ContainsKey("JSM SHA256"));
        });
        Assert.Equal(result.Entries.Count, result.Entries.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());

    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("it")]
    public void OptionalInstalledNonfieldSectionsRetainIndependentTables(string language)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_LOCALIZATION_GAME_DIR for optional installed source export validation.");
        LocalizationSourceCatalog result = LocalizationSourceExporter.ReadNonFields(directory, language, TestContext.Current.CancellationToken);
        string? reportDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, language + "-nonfield-summary.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Language = language,
                Entries = result.Entries.Count,
                Categories = result.Entries.GroupBy(row => row.Category).ToDictionary(group => group.Key, group => group.Count()),
                Tables = result.Entries.GroupBy(row => row.File).ToDictionary(group => group.Key, group => group.Count()),
                Gaps = result.Gaps
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.All(result.Entries, row =>
        {
            Assert.True(row.Metadata.TryGetValue("Source SHA256", out string? hash) || row.Metadata.ContainsKey("Native Source Fingerprint"));
            if (hash is not null)
            {
                Assert.Equal(64, hash.Length);
            }
            if (row.Metadata["Resource"] == "menu/mngrp.bin")
            {
                Assert.Equal("menu/mngrphd.bin", row.Metadata["Companion Resource"]);
                Assert.Equal(64, row.Metadata["Companion SHA256"]!.Length);
            }
        });
        Assert.Contains(result.Entries, row => row.File == "names_cards");
        Assert.Contains(result.Entries, row => row.File == "mngrp_slot087_table");
        Assert.Contains(result.Entries, row => row.Category == "Information and SeeD exam");
        Assert.DoesNotContain(result.Entries, row => row.Category == "Fields");
        Assert.DoesNotContain(result.Entries, row => row.Metadata.TryGetValue("Menu Section", out string? section) && int.TryParse(section, out int slot) && slot is >= 128 and <= 133 && row.Metadata["Source Ordinal"] == "1");
        Assert.Equal(result.Entries.Count, result.Entries.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void OptionalClassicNonfieldSourcesRetainResourceAndNativeFingerprints()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_CLASSIC_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_LOCALIZATION_CLASSIC_GAME_DIR for optional Classic source export validation.");
        LocalizationSourceCatalog result = LocalizationSourceExporter.ReadNonFields(directory, "en", TestContext.Current.CancellationToken);
        string? reportDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOCALIZATION_REPORT_DIR");
        if (!string.IsNullOrWhiteSpace(reportDirectory))
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, "en-classic-nonfield-summary.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                Language = "en",
                Edition = "Classic",
                Entries = result.Entries.Count,
                Categories = result.Entries.GroupBy(row => row.Category).ToDictionary(group => group.Key, group => group.Count()),
                Tables = result.Entries.GroupBy(row => row.File).ToDictionary(group => group.Key, group => group.Count()),
                NativeBoundRows = result.Entries.Count(row => row.Metadata.ContainsKey("Native Source Fingerprint")),
                RowsWithoutFingerprint = result.Entries.Where(row => !row.Metadata.ContainsKey("Source SHA256") && !row.Metadata.ContainsKey("Native Source Fingerprint")).Select(row => row.Key).ToArray(),
                Gaps = result.Gaps
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        Assert.NotEmpty(result.Entries);
        Assert.Equal(110, result.Entries.Count(row => row.File == "names_cards"));
        Assert.Equal(160, result.Entries.Count(row => row.File == "scan_monsters"));
        Assert.DoesNotContain(result.Entries, row => row.Category == "Fields");
        Assert.All(result.Entries, row =>
        {
            string? fingerprint = row.Metadata.GetValueOrDefault("Source SHA256") ?? row.Metadata.GetValueOrDefault("Native Source Fingerprint");
            Assert.NotNull(fingerprint);
            Assert.Equal(64, fingerprint.Length);
            if (row.Metadata["Resource"] == "menu/mngrp.bin")
            {
                Assert.Equal("menu/mngrphd.bin", row.Metadata["Companion Resource"]);
                Assert.Equal(64, row.Metadata["Companion SHA256"]!.Length);
            }
        });
    }

    private static InMemoryResourceSource Source() => new(new("synthetic", GameDataEdition.Remastered, []));
    private sealed class MissingArchives : IGameArchiveSource
    {
        public IFlArchive OpenArchive(string family) => throw new FileNotFoundException("Synthetic archive unavailable.");
    }
}
