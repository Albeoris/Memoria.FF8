using System.Security.Cryptography;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionResourceRejectionInstalledTests
{
    [Fact]
    public void SelectedInvalidResourcesKeepNativePixelsAndActiveSiblingsAcrossAcquisitionRefreshRepairAndDisable()
    {
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_RESOURCE_REJECTION_CASES");
        Assert.SkipUnless(File.Exists(catalog), "Set MEMORIA_FF8_RESOURCE_REJECTION_CASES to an optional JSON array of extracted package, option and native consumer cases.");
        InputCase[] cases = JsonSerializer.Deserialize<InputCase[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("Expected an external resource rejection case array.");
        Assert.NotEmpty(cases);
        List<object> results = [];
        foreach (InputCase input in cases)
        {
            using PackageFixture fixture = new(input);
            using Consumer original = new(input, null);
            TextureValue originalBad = original.Textures.GetTexture(input.RejectedTexture);
            TextureValue originalValid = original.Textures.GetTexture(input.ValidTexture);
            using GameResourceHost host = fixture.Open();
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "resource-rejected");
            using Consumer active = new(input, host.Resources);
            string validPixels = Hash(active.Textures.GetTexture(input.ValidTexture).Image);
            List<object> epochs = [];
            CheckRejected("initial-acquisition");
            for (int epoch = 0; epoch < 2; epoch++)
            {
                long generation = host.Resources.Generation;
                host.Mods.Refresh();
                Assert.True(host.Pump(true));
                Assert.True(host.Resources.Generation > generation);
                CheckRejected("resident-refresh-" + epoch);
            }
            long unchangedGeneration = host.Resources.Generation;
            host.Mods.RequestRefresh();
            Assert.False(host.Pump(true));
            Assert.Equal(unchangedGeneration, host.Resources.Generation);
            CheckRejected("unchanged-refresh");
            using (MagickImage repaired = new(MagickColors.DarkOrange, (uint)originalBad.Image.Width, (uint)originalBad.Image.Height))
            {
                repaired.Write(fixture.RejectedPath, MagickFormat.Dds);
            }
            host.Mods.Refresh();
            Assert.True(host.Pump(true));
            TextureValue fixedValue = active.Textures.GetTexture(input.RejectedTexture);
            Assert.True(fixedValue.IsReplacement);
            Assert.StartsWith(fixture.PackageId + "/", fixedValue.Provenance);
            Assert.NotEqual(Hash(originalBad.Image), Hash(fixedValue.Image));
            Assert.Equal(validPixels, Hash(active.Textures.GetTexture(input.ValidTexture).Image));
            Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code == "resource-rejected" && item.PackageId == fixture.PackageId);
            Assert.Equal(ModUpdateOutcome.Idle, host.Mods.View.Outcome);
            Assert.False(host.Mods.IsReloadPending);
            Assert.False(host.Pump());
            fixture.SetEnabled(false);
            using GameResourceHost disabledHost = fixture.Open();
            using Consumer disabled = new(input, disabledHost.Resources);
            Assert.Equal(Hash(originalBad.Image), Hash(disabled.Textures.GetTexture(input.RejectedTexture).Image));
            Assert.Equal(Hash(originalValid.Image), Hash(disabled.Textures.GetTexture(input.ValidTexture).Image));
            Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(disabledHost.Mods.Catalog.Packages, package => package.Id == fixture.PackageId).Status);
            Assert.DoesNotContain(disabledHost.Mods.Diagnostics, item => item.Code == "resource-rejected");
            Assert.False(disabledHost.Mods.IsReloadPending);
            Assert.Equal(fixture.SourceHash, FileHash(fixture.SourceRejectedPath));
            results.Add(new { input.Id, input.Consumer, input.Model, input.RejectedRelativePath, fixture.PackageId, originalWidth = originalBad.Image.Width, originalHeight = originalBad.Image.Height, nativePixels = Hash(originalBad.Image), validPixels, epochs, repaired = true, warningCleared = true, freshHostDisabledNativePixels = true, sourceUnchanged = true });

            void CheckRejected(string phase)
            {
                TextureValue rejected = active.Textures.GetTexture(input.RejectedTexture);
                TextureValue valid = active.Textures.GetTexture(input.ValidTexture);
                Assert.False(rejected.IsReplacement);
                Assert.Equal(Hash(originalBad.Image), Hash(rejected.Image));
                Assert.True(valid.IsReplacement);
                Assert.StartsWith(fixture.PackageId + "/", valid.Provenance);
                Assert.Equal(validPixels, Hash(valid.Image));
                Assert.NotEqual(Hash(originalValid.Image), Hash(valid.Image));
                ModDiagnostic diagnostic = Assert.Single(host.Mods.Diagnostics, item => item.Code == "resource-rejected" && item.PackageId == fixture.PackageId && string.Equals(item.RelativePath, input.RejectedRelativePath, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(host.Mods.Diagnostics, item => item.Code is "reload-failed" or "replacement-failed");
                ModServiceView view = host.Mods.View;
                Assert.Equal(ModUpdateOutcome.PublishedWithWarnings, view.Outcome);
                Assert.False(view.IsReloadPending);
                ModPackageView package = Assert.Single(view.Packages, package => package.Package.Id == fixture.PackageId);
                Assert.Equal(ModEffectiveStatus.Active, package.CommittedStatus);
                Assert.StartsWith("Active", package.StatusText);
                Assert.DoesNotContain("failed", package.StatusText, StringComparison.OrdinalIgnoreCase);
                Assert.True(package.NeedsAttention);
                Assert.NotNull(package.Problem);
                Assert.False(host.Pump());
                epochs.Add(new { phase, generation = host.Resources.Generation, outcome = view.Outcome.ToString(), package.StatusText, package.NeedsAttention, diagnostic.Code, diagnostic.PackageId, diagnostic.RelativePath, diagnostic.Message, rejectedPixels = Hash(rejected.Image), validPixels = Hash(valid.Image) });
            }
        }
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOD_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "resource-rejection-installed.json"), JsonSerializer.Serialize(new { caseCount = results.Count, results, scope = "Actual native field library and world foot-model texture consumers; per-resource rejection, active sibling publication and diagnostic lifecycle. No gameplay or binary overlay migration." }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Hash(PixelImage image) => Convert.ToHexString(SHA256.HashData(image.Pixels.SelectMany(pixel => new[] { pixel.R, pixel.G, pixel.B, pixel.A }).ToArray()));

    private sealed record InputCase(string Id, string PackageDirectory, Dictionary<string, int> Options, string Consumer, string Model, int RejectedTexture, int ValidTexture, string RejectedRelativePath);

    private sealed class Consumer : IDisposable
    {
        private readonly IDisposable assets;
        public ResourceTextureList Textures { get; }

        public Consumer(InputCase input, ResourceService? resources)
        {
            if (input.Consumer == "field-library")
            {
                GameAssets field = new(TestResources.GameDirectory, resources: resources);
                assets = field;
                Textures = Assert.IsType<ResourceTextureList>(field.LoadFieldMainCharacterTextures(input.Model));
            }
            else if (input.Consumer == "world-foot")
            {
                WorldAssets world = new(TestResources.GameDirectory, resources);
                assets = world;
                Textures = Assert.IsType<ResourceTextureList>(world.GetFootModel(byte.Parse(input.Model, System.Globalization.CultureInfo.InvariantCulture)).Textures);
            }
            else
            {
                throw new InvalidDataException("Unsupported optional resource rejection consumer.");
            }
        }

        public void Dispose() => assets.Dispose();
    }

    private sealed class PackageFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-resource-rejection-" + Guid.NewGuid().ToString("N"));
        private readonly string destination;
        private readonly Dictionary<string, int> options;
        public string PackageId { get; }
        public string RejectedPath { get; }
        public string SourceRejectedPath { get; }
        public string SourceHash { get; }

        public PackageFixture(InputCase input)
        {
            options = input.Options;
            JunctionModPackage imported = JunctionModImporter.Open(input.PackageDirectory, options);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(input.PackageDirectory));
            PackageId = "Junction/" + name;
            destination = Path.Combine(root, "Junction", name);
            Directory.CreateDirectory(destination);
            File.Copy(Path.Combine(input.PackageDirectory, "mod.xml"), Path.Combine(destination, "mod.xml"));
            foreach (JunctionReplacement file in imported.Files)
            {
                string copy = Path.Combine(destination, file.SourcePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                using FileStream output = File.Create(copy);
                file.CopyTo(output);
            }
            JunctionReplacement rejected = Assert.Single(imported.Files, file => file.RelativePath.Equals(input.RejectedRelativePath, StringComparison.OrdinalIgnoreCase));
            RejectedPath = Path.Combine(destination, rejected.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            SourceRejectedPath = Path.Combine(input.PackageDirectory, rejected.SourcePath.Replace('/', Path.DirectorySeparatorChar));
            SourceHash = FileHash(SourceRejectedPath);
            SetEnabled(true);
        }

        public void SetEnabled(bool enabled) => File.WriteAllLines(Path.Combine(destination, "mod.ini"), new[] { "enabled = " + (enabled ? "1" : "0") }.Concat(options.Select(option => $"option.{option.Key} = {option.Value}")));
        public GameResourceHost Open() => new(TestResources.GameDirectory, watch: false, modsDirectory: root);
        public void Dispose() => Directory.Delete(root, true);
    }
}
