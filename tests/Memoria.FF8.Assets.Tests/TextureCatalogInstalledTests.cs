using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureCatalogInstalledTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_GAME_DIR")]
    public void FullSourceDiscoveryAndRepresentativeRoundTrips(string variable)
    {
        string? gameDirectory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_CATALOG_FULL_SCAN") == "1" && Directory.Exists(gameDirectory), "Set an optional installation and enable its explicit full catalog scan.");
        CancellationToken token = TestContext.Current.CancellationToken;
        string? modelDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_MODEL_SOURCE");
        string reportName = variable + (string.IsNullOrWhiteSpace(modelDirectory) ? "" : "-mixed");
        string temporary = Path.Combine(Path.GetTempPath(), "memoria-texture-catalog-" + Guid.NewGuid().ToString("N"));
        try
        {
            using IResourceSource source = GameDataSourceFactory.OpenTextureAuthoring(gameDirectory!, modelDirectory);
            ResourceService resources = new(source);
            using GameAssets assets = new(gameDirectory!, resources: resources);
            Dictionary<string, int> counts = [];
            List<TextureCatalogDiagnostic> diagnostics = [];
            Dictionary<string, TextureCatalogEntry> representatives = [];
            int count = 0;
            long logicalPixels = 0;
            int initialResidency = assets.Resources.ResidentTextureCount;
            TextureBuildContext? retainedBackground = null;
            Stopwatch discovery = Stopwatch.StartNew();
            long peakSourceBytes = 0;
            long peakManagedBytes = 0;
            foreach (TextureCatalogEntry entry in assets.EnumerateTextureCatalog(token, diagnostics.Add, includeFieldTiles: Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_CATALOG_INCLUDE_FIELD_TILES") == "1"))
            {
                count++;
                logicalPixels += (long)entry.LogicalWidth * entry.LogicalHeight;
                counts[entry.Category] = counts.GetValueOrDefault(entry.Category) + 1;
                representatives.TryAdd(entry.Category, entry);
                if (retainedBackground is null && entry.Category == "Field background")
                {
                    retainedBackground = assets.LoadCatalogTextureUnit(entry)[0];
                }
                Assert.InRange(((ITextureAuthoringResourceSource)source).RetainedTextureAuthoringArchiveCount, 0, string.IsNullOrWhiteSpace(modelDirectory) ? 4 : 8);
                peakSourceBytes = Math.Max(peakSourceBytes, ((ITextureAuthoringResourceSource)source).RetainedTextureAuthoringArchiveBytes);
                if (count % 100 == 0)
                {
                    peakManagedBytes = Math.Max(peakManagedBytes, GC.GetTotalMemory(false));
                    if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_CATALOG_REPORT") is { Length: > 0 } progressDirectory)
                    {
                        Directory.CreateDirectory(progressDirectory);
                        File.WriteAllText(Path.Combine(progressDirectory, reportName + ".progress.json"), JsonSerializer.Serialize(new { Count = count, entry.Category, ManagedBytes = GC.GetTotalMemory(false), RetainedSourceBytes = ((ITextureAuthoringResourceSource)source).RetainedTextureAuthoringArchiveBytes }));
                    }
                }
            }
            discovery.Stop();
            ((ITextureAuthoringResourceSource)source).ReleaseTextureAuthoringArchives();
            if (retainedBackground is not null)
            {
                using Stream retainedSource = retainedBackground.EncodedSource!.OpenRead();
                Assert.NotEmpty(System.Security.Cryptography.SHA256.HashData(retainedSource));
                Assert.Equal(16, retainedBackground.Original.Width);
            }
            Assert.Equal(initialResidency, assets.Resources.ResidentTextureCount);
            Stopwatch roundTrips = Stopwatch.StartNew();
            foreach ((string category, TextureCatalogEntry entry) in representatives)
            {
                token.ThrowIfCancellationRequested();
                if (entry.Layout == "field-workspace")
                {
                    continue;
                }
                TextureBuildContext context = assets.LoadCatalogTexture(entry);
                Assert.Equal(entry.Target, TextureTarget.FromContext(context));
                TextureValue installedOriginal = resources.ResolveOriginalForExport(context);
                TextureSourceSnapshot snapshot = new(context, installedOriginal, 0) { InstalledOriginal = installedOriginal };
                TextureExportResult output = TextureExchange.Export([snapshot], temporary, cancellationToken: token);
                string path = Assert.Single(output.Files);
                Assert.Equal(entry.Target, TextureExchange.ReadMetadata(path)!.Target);
                Assert.NotEmpty(File.ReadAllBytes(path));
            }
            if (representatives.TryGetValue("Field background", out TextureCatalogEntry? background))
            {
                string field = background.Target.Owner.Split('/')[1];
                string workspace = Path.Combine(temporary, "field.ora");
                assets.ExportFieldTextureWorkspace(field, workspace, token);
                IReadOnlyList<TextureWorkspaceTile> tiles = assets.ImportFieldTextureWorkspace(workspace, token);
                Assert.NotEmpty(tiles);
                Dictionary<TextureTarget, TextureBuildContext> originals = assets.LoadCatalogTextureUnit(background).ToDictionary(context => TextureTarget.FromContext(context));
                foreach (TextureWorkspaceTile tile in tiles)
                {
                    Assert.Equal(originals[tile.Target].Original.Pixels, tile.Image.Pixels);
                }
            }
            roundTrips.Stop();
            using Process process = Process.GetCurrentProcess();
            object report = new { Edition = assets.Resources.Source.Edition.ToString(), ModelEdition = (source as SelectedModelResourceSource)?.ModelSourceDescriptor.Edition.ToString(), assets.Resources.Source.Language, Hardware = new { Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), Environment.ProcessorCount, OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription }, AssetCount = count, LogicalPixels = logicalPixels, Counts = counts, DiscoveryMilliseconds = discovery.ElapsedMilliseconds, RepresentativeRoundTripMilliseconds = roundTrips.ElapsedMilliseconds, ManagedBytes = GC.GetTotalMemory(false), PeakManagedBytes = peakManagedBytes, PeakRetainedSourceBytes = peakSourceBytes, PeakProcessBytes = process.PeakWorkingSet64, ResidentTextureCount = assets.Resources.ResidentTextureCount, Diagnostics = diagnostics };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXTURE_CATALOG_REPORT") is { Length: > 0 } destination)
            {
                Directory.CreateDirectory(destination);
                File.WriteAllText(Path.Combine(destination, reportName + ".json"), json);
            }
            Assert.True(count > 0);
            Assert.Contains("Field background", counts.Keys);
            Assert.Contains("Battle weapon", counts.Keys);
            Assert.Equal(initialResidency, assets.Resources.ResidentTextureCount);
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }
}
