using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

[Collection("Installed Junction resources")]
public sealed class JunctionNativePackageInstalledTests
{
    [Fact]
    public void ConfiguredSelectionsPublishCoherentNativeDataInAnIsolatedInstance()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PACKAGE_FIXTURES");
        Assert.SkipUnless(File.Exists(path), "Configure an optional native resource package fixture catalog.");
        Fixture fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path!), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        string package = Path.GetFullPath(fixture.Package, Path.GetDirectoryName(Path.GetFullPath(path!))!);
        string fingerprint = FileFingerprint(package);
        using IsolatedFieldGame game = new();
        string root = Path.Combine(Path.GetTempPath(), "ff8-native-package-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(root, "Mods");
        string archive = Path.Combine(mods, "Junction", "native.iroj");
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        File.Copy(package, archive);
        try
        {
            for (int index = 0; index < fixture.Selections.Length; index++)
            {
                Dictionary<string, int> options = fixture.Selections[index];
                File.WriteAllLines(archive + ".ini", new[] { "enabled = 1" }.Concat(options.Select(pair => $"option.{pair.Key} = {pair.Value}")));
                JunctionModPackage imported = JunctionModImporter.Open(archive, options);
                ModFile[] files = imported.Files.Select(file => new ModFile("Junction/native.iroj", ModFormat.Junction, file.RelativePath, 1, () => Read(file.OpenRead()))).ToArray();
                Assert.True(JunctionEncodedResources.TryPrepare(new ModCatalogSnapshot(1, [], files), out var expected, out var errors), JsonSerializer.Serialize(errors));
                using GameResourceHost host = new(game.GameDirectory, Path.Combine(root, "Application"), watch: false, modsDirectory: mods, resourceLanguage: fixture.ResourceLanguage);
                WriteEvidence(index, host.Mods.Catalog.Packages, host.Mods.Diagnostics);
                ModPackage selected = Assert.Single(host.Mods.Catalog.Packages, item => item.Id == "Junction/native.iroj");
                Assert.Equal(ModEffectiveStatus.Active, selected.Status);
                Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Junction.InvalidEncodedResource");
                foreach (EncodedResource resource in expected.Where(resource => NativeDataResourceFiles.Classify(resource.Key) is not null))
                {
                    Assert.Equal(Read(resource.Content.OpenRead()), host.Resources.Read(resource.Key));
                }
                byte[] initial = host.Resources.Read(new ResourceKey("main/init.out"));
                byte[] kernel = host.Resources.Read(new ResourceKey("main/kernel.bin"));
                _ = NativeCharacterInitialization.Read(initial, kernel);
                _ = NativeEconomyInitialization.Read(initial);
                using GameAssets assets = new(game.GameDirectory, resources: host.Resources);
                var refinement = assets.ReadNativeRefinementDefinitions();
                MngrpArchive menuArchive = MngrpArchive.Read(host.Resources.Read(new ResourceKey("menu/mngrp.bin")), host.Resources.Read(new ResourceKey("menu/mngrphd.bin")), FF8Encoding.CreateEuropean());
                var expectedRecipes = NativeRefinementGroupReader.ReadTables(menuArchive);
                for (int kind = 0; kind < expectedRecipes.Length; kind++)
                {
                    Assert.Equal(expectedRecipes[kind].ToArray(), refinement.Tables[kind].ToArray());
                }
                TripleTriadCatalog cards = TripleTriadCatalog.Load(game.GameDirectory, host.Resources);
                Assert.Equal(NativeRefinementGroupReader.ReadCardRefinements(menuArchive, refinement.Abilities.Skip(5).Single(ability => ability.Kind == 4)).ToArray(), cards.Refinements.ToArray());
                LiveMenuCatalog menu = LiveMenuCatalog.ReadInstalled(assets);
                for (int card = 0; card < 110; card++)
                {
                    Assert.Equal(cards.Refinements.FirstOrDefault(recipe => recipe.CardId == card), menu.CardRefinement(card));
                }
                using WorldAssets world = new(game.GameDirectory, host.Resources);
                ResourceKey settings = NativeResourceNames.WorldSettings(host.Resources.ResourceLanguage);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(host.Resources.Read(settings))), world.AllocationProgramSha256);
                Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "resource-unused" && diagnostic.RelativePath?.EndsWith(Path.GetFileName(settings.Path), StringComparison.OrdinalIgnoreCase) == true);
                using BattleAssets battle = new(game.GameDirectory, host.Resources);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(host.Resources.Read(new ResourceKey("battle/scene.out")))), battle.EncounterSourceHash);
                for (int row = 0; row < 1024; row++)
                {
                    _ = battle.LoadEncounter(row);
                }
            }
            Assert.Equal(fingerprint, FileFingerprint(package));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static byte[] Read(Stream source)
    {
        using (source)
        {
            using MemoryStream result = new();
            source.CopyTo(result);
            return result.ToArray();
        }
    }

    private static string FileFingerprint(string path)
    {
        using Stream source = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(source));
    }

    private static void WriteEvidence(int selection, IEnumerable<ModPackage> packages, IEnumerable<ModDiagnostic> diagnostics)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_NATIVE_PACKAGE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"selection-{selection}.json"), JsonSerializer.Serialize(new { packages, diagnostics }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed record Fixture(string Package, Dictionary<string, int>[] Selections, string? ResourceLanguage = null);
}
